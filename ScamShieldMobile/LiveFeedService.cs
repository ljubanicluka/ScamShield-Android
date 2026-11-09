using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ScamShieldMobile;

public sealed record LiveAlertItem(
    string Id,
    string CountryCode,
    string SourceName,
    string Coverage,
    string Title,
    string Summary,
    string Url,
    DateTimeOffset? PublishedAt);

public sealed record LiveFeedSourceStatus(
    string CountryCode,
    string SourceName,
    string Coverage,
    string Url,
    bool IsAvailable,
    int ItemCount,
    string Message,
    DateTimeOffset CheckedAt);

public sealed record LiveFeedSnapshot(
    IReadOnlyList<LiveAlertItem> Items,
    IReadOnlyList<LiveFeedSourceStatus> Sources,
    DateTimeOffset RefreshedAt,
    bool IsUsingCachedData);

/// <summary>
/// Read-only public advisory ingestion. It never uploads checked URLs, messages,
/// profile data, or evidence to any provider.
/// </summary>
public sealed class LiveFeedService
{
    private enum SourceFormat { RssOrAtom, Html }

    private sealed record FeedSource(
        string CountryCode,
        string Name,
        string Coverage,
        string Url,
        SourceFormat Format);

    private sealed record CacheEnvelope(
        List<LiveAlertItem> Items,
        DateTimeOffset SavedAt);

    private static readonly FeedSource[] Sources =
    [
        new("rs", "Nacionalni CERT Srbije", "Srbija", "https://www.cert.rs/rs/obavestenja.html", SourceFormat.Html),
        new("ba", "CERT Republike Srpske", "Samo Republika Srpska; ne predstavlja celu BiH", "https://cert.aikt.rs/feed/", SourceFormat.RssOrAtom),
        new("me", "CIRT državne uprave Crne Gore", "Crna Gora — državni organi", "https://rss.gov.me/atom/me/gov-cirt", SourceFormat.RssOrAtom),
        new("mk", "MKD-CIRT", "Severna Makedonija", "https://mkd-cirt.mk/feed/", SourceFormat.RssOrAtom)
    ];

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(18)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ScamShield", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    public async Task<LiveFeedSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var tasks = Sources.Select(source => FetchSourceAsync(source, checkedAt, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var items = results
            .SelectMany(result => result.Items)
            .Where(item => Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();

        var usingCache = false;
        if (items.Count > 0)
        {
            await SaveCacheAsync(items, checkedAt, cancellationToken);
        }
        else
        {
            var cached = await LoadCacheAsync(cancellationToken);
            if (cached.Count > 0)
            {
                items = cached;
                usingCache = true;
            }
        }

        return new LiveFeedSnapshot(
            items,
            results.Select(result => result.Status).ToList(),
            checkedAt,
            usingCache);
    }

    private static async Task<(List<LiveAlertItem> Items, LiveFeedSourceStatus Status)> FetchSourceAsync(
        FeedSource source,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = source.Format == SourceFormat.Html
                ? ParseSerbianCertPage(source, body)
                : ParseRssOrAtom(source, body);

            var status = new LiveFeedSourceStatus(
                source.CountryCode,
                source.Name,
                source.Coverage,
                source.Url,
                true,
                items.Count,
                items.Count > 0 ? "Izvor je dostupan." : "Izvor je dostupan, ali trenutno nema pronađenih stavki.",
                checkedAt);

            return (items, status);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Xml.XmlException)
        {
            var safeMessage = ex is TaskCanceledException
                ? "Isteklo je vreme za povezivanje sa izvorom."
                : "Izvor trenutno nije dostupan ili je promenio format.";
            return ([], new LiveFeedSourceStatus(
                source.CountryCode,
                source.Name,
                source.Coverage,
                source.Url,
                false,
                0,
                safeMessage,
                checkedAt));
        }
    }

    private static List<LiveAlertItem> ParseRssOrAtom(FeedSource source, string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var entries = document.Descendants()
            .Where(element => element.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                              element.Name.LocalName.Equals("entry", StringComparison.OrdinalIgnoreCase))
            .Take(40);

        var results = new List<LiveAlertItem>();
        foreach (var entry in entries)
        {
            var title = ChildValue(entry, "title");
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var linkElement = entry.Elements().FirstOrDefault(element =>
                element.Name.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase));
            var link = linkElement?.Attribute("href")?.Value;
            if (string.IsNullOrWhiteSpace(link))
                link = linkElement?.Value;

            if (string.IsNullOrWhiteSpace(link) ||
                !Uri.TryCreate(new Uri(source.Url), link, out var absoluteUri) ||
                (absoluteUri.Scheme != Uri.UriSchemeHttps && absoluteUri.Scheme != Uri.UriSchemeHttp))
                continue;

            var summary = ChildValue(entry, "summary");
            if (string.IsNullOrWhiteSpace(summary))
                summary = ChildValue(entry, "description");
            if (string.IsNullOrWhiteSpace(summary))
                summary = ChildValue(entry, "encoded");

            var dateText = ChildValue(entry, "published");
            if (string.IsNullOrWhiteSpace(dateText))
                dateText = ChildValue(entry, "pubDate");
            if (string.IsNullOrWhiteSpace(dateText))
                dateText = ChildValue(entry, "updated");

            results.Add(new LiveAlertItem(
                absoluteUri.AbsoluteUri,
                source.CountryCode,
                source.Name,
                source.Coverage,
                CleanText(title),
                CleanText(summary),
                absoluteUri.AbsoluteUri,
                ParseDate(dateText)));
        }

        return results;
    }

    private static List<LiveAlertItem> ParseSerbianCertPage(FeedSource source, string html)
    {
        // The National CERT publishes its notifications as a paginated HTML list,
        // not a documented public machine-readable feed. Extract only its official
        // /obavestenje/ article links; keep each article's own page as the source.
        var matches = Regex.Matches(
            html,
            "<a\\b[^>]*href=[\\\"'](?<href>[^\\\"']*/obavestenje/[^\\\"']+)[\\\"'][^>]*>(?<title>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var items = new List<LiveAlertItem>();
        foreach (Match match in matches)
        {
            var title = CleanText(match.Groups["title"].Value);
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            if (string.IsNullOrWhiteSpace(title) ||
                !Uri.TryCreate(new Uri(source.Url), href, out var absoluteUri))
                continue;

            var id = absoluteUri.AbsoluteUri;
            if (items.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                continue;

            items.Add(new LiveAlertItem(
                id,
                source.CountryCode,
                source.Name,
                source.Coverage,
                title,
                string.Empty,
                absoluteUri.AbsoluteUri,
                null));
        }

        return items.Take(30).ToList();
    }

    private static string ChildValue(XElement parent, string localName)
    {
        var element = parent.Elements().FirstOrDefault(child =>
            child.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        return element?.Value?.Trim() ?? string.Empty;
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var withoutTags = Regex.Replace(value, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\\s+", " ").Trim();
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string CachePath => Path.Combine(FileSystem.AppDataDirectory, "official-alerts-cache.json");

    private static async Task SaveCacheAsync(
        List<LiveAlertItem> items,
        DateTimeOffset savedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var envelope = new CacheEnvelope(items, savedAt);
            var json = JsonSerializer.Serialize(envelope, JsonOptions);
            await File.WriteAllTextAsync(CachePath, json, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Live feed display still works when caching is unavailable.
        }
    }

    private static async Task<List<LiveAlertItem>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CachePath))
                return [];

            var json = await File.ReadAllTextAsync(CachePath, cancellationToken);
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(json, JsonOptions);
            return envelope?.Items ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }
}
