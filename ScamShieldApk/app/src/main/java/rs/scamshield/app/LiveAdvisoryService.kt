package rs.scamshield.app

import android.content.Context
import android.text.Html
import android.util.Xml
import org.json.JSONArray
import org.json.JSONObject
import org.xmlpull.v1.XmlPullParser
import java.io.ByteArrayInputStream
import java.net.HttpURLConnection
import java.net.URL
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.Callable
import java.util.concurrent.Executors

/** Read-only official CERT/CIRT advisory fetcher. No user-entered content is sent. */
class LiveAdvisoryService(context: Context) {
    private val prefs = context.getSharedPreferences("official_advisories", Context.MODE_PRIVATE)

    private data class Source(val id: String, val label: String, val url: String, val format: String)
    private data class Item(
        val title: String, val excerpt: String, val date: String, val link: String,
        val source: String, val sourceLabel: String, val audience: String
    ) {
        fun json() = JSONObject().apply {
            put("title", title); put("excerpt", excerpt); put("date", date)
            put("link", link); put("source", source); put("sourceLabel", sourceLabel)
            put("audience", audience)
        }
    }

    private val sources = listOf(
        Source("rs", "Serbia — National CERT", "https://www.cert.rs/rs/obavestenja.html", "html"),
        Source("ba-rs", "CERT RS — Republika Srpska", "https://cert.aikt.rs/feed/", "rss"),
        Source("me", "Montenegro — Government CIRT", "https://rss.gov.me/atom/me/gov-cirt", "atom"),
        Source("mk", "North Macedonia — MKD-CIRT", "https://mkd-cirt.mk/feed/", "rss")
    )

    @Synchronized
    fun cachedSnapshot(): JSONObject = snapshot(emptyMap(), false)

    @Synchronized
    fun refresh(): JSONObject {
        val executor = Executors.newFixedThreadPool(sources.size)
        val tasks = sources.map { source -> Callable { refreshSource(source) } }
        val statuses = mutableMapOf<String, JSONObject>()
        try {
            executor.invokeAll(tasks).forEach { future ->
                try {
                    val (id, status) = future.get()
                    statuses[id] = status
                } catch (_: Exception) { }
            }
        } finally {
            executor.shutdownNow()
        }
        return snapshot(statuses, true)
    }

    private fun refreshSource(source: Source): Pair<String, JSONObject> {
        return try {
            val downloaded = if (source.format == "html") fetchSerbia(source) else fetchXml(source)
            val old = readItems(source.id)
            // Do not erase useful cached content if a valid endpoint temporarily returns an empty page.
            if (downloaded.isNotEmpty()) {
                prefs.edit().putString("items_${source.id}", JSONArray().apply {
                    downloaded.forEach { put(it.json()) }
                }.toString()).putLong("updated_${source.id}", System.currentTimeMillis()).apply()
            }
            source.id to JSONObject().apply {
                put("status", if (downloaded.isEmpty()) "empty" else "online")
                put("count", if (downloaded.isEmpty()) old.size else downloaded.size)
                put("updatedAt", prefs.getLong("updated_${source.id}", 0L))
                put("message", if (downloaded.isEmpty()) "No new items; showing cached items if available." else "Updated")
            }
        } catch (_: Exception) {
            val old = readItems(source.id)
            source.id to JSONObject().apply {
                put("status", "unavailable")
                put("count", old.size)
                put("updatedAt", prefs.getLong("updated_${source.id}", 0L))
                put("message", if (old.isNotEmpty()) "Source unavailable; cached items shown." else "Source unavailable.")
            }
        }
    }

    private fun snapshot(statuses: Map<String, JSONObject>, justRefreshed: Boolean): JSONObject {
        val all = mutableListOf<JSONObject>()
        val sourceJson = JSONArray()
        for (s in sources) {
            val stored = readItems(s.id)
            stored.forEach { all.add(it) }
            val status = statuses[s.id] ?: JSONObject().apply {
                put("status", if (stored.isEmpty()) "not_checked" else "cached")
                put("count", stored.size)
                put("updatedAt", prefs.getLong("updated_${s.id}", 0L))
                put("message", if (stored.isEmpty()) "Not checked yet." else "Cached advisory data")
            }
            status.put("id", s.id).put("label", s.label).put("url", s.url)
            sourceJson.put(status)
        }
        val sorted = all.distinctBy {
            val titleKey = it.optString("title").lowercase(Locale.ROOT).replace(Regex("[^\\p{L}\\p{N}]+"), " ").trim()
            titleKey.ifBlank { it.optString("link") }
        }
            .sortedWith(compareBy<JSONObject> { audienceRank(it.optString("audience")) }
                .thenByDescending { it.optString("date") })
        return JSONObject().apply {
            put("sources", sourceJson)
            put("items", JSONArray(sorted))
            put("refreshedAt", System.currentTimeMillis())
            put("freshFetch", justRefreshed)
        }
    }

    private fun audienceRank(value: String) = when (value) {
        "ordinary" -> 0
        "mixed" -> 1
        else -> 2
    }

    private fun readItems(id: String): List<JSONObject> = try {
        val array = JSONArray(prefs.getString("items_$id", "[]") ?: "[]")
        (0 until array.length()).mapNotNull { array.optJSONObject(it) }
    } catch (_: Exception) { emptyList() }

    private fun open(url: String): HttpURLConnection {
        val connection = URL(url).openConnection() as HttpURLConnection
        connection.requestMethod = "GET"
        connection.connectTimeout = 10000
        connection.readTimeout = 12000
        connection.instanceFollowRedirects = true
        connection.setRequestProperty("User-Agent", "ScamShield-Android/1.0 (official advisory reader)")
        connection.setRequestProperty("Accept", "application/rss+xml, application/atom+xml, application/xml, text/xml, text/html;q=0.9, */*;q=0.8")
        return connection
    }

    private fun fetchXml(source: Source): List<Item> {
        val connection = open(source.url)
        try {
            val parser = Xml.newPullParser()
            parser.setFeature(XmlPullParser.FEATURE_PROCESS_NAMESPACES, true)
            connection.inputStream.use { parser.setInput(it, null); return parseFeed(parser, source) }
        } finally { connection.disconnect() }
    }

    private fun parseFeed(parser: XmlPullParser, source: Source): List<Item> {
        val result = mutableListOf<Item>()
        var inside = false
        var title = ""; var description = ""; var link = ""; var date = ""
        var event = parser.eventType
        while (event != XmlPullParser.END_DOCUMENT && result.size < 40) {
            when (event) {
                XmlPullParser.START_TAG -> {
                    val name = (parser.name ?: "").lowercase(Locale.ROOT)
                    if (name == "item" || name == "entry") {
                        inside = true; title = ""; description = ""; link = ""; date = ""
                    } else if (inside) {
                        when (name) {
                            "title" -> title = safeNextText(parser)
                            "description", "summary", "content", "encoded" -> description = safeNextText(parser)
                            "pubdate", "published", "updated", "date" -> date = safeNextText(parser)
                            "link" -> {
                                val href = parser.getAttributeValue(null, "href")
                                val rel = parser.getAttributeValue(null, "rel")
                                if (href != null) {
                                    if (link.isBlank() || rel.equals("alternate", ignoreCase = true)) link = href
                                } else if (link.isBlank()) {
                                    link = safeNextText(parser)
                                }
                            }
                        }
                    }
                }
                XmlPullParser.END_TAG -> {
                    val name = (parser.name ?: "").lowercase(Locale.ROOT)
                    if ((name == "item" || name == "entry") && inside) {
                        val item = makeItem(title, description, date, link, source)
                        if (item != null) result.add(item)
                        inside = false
                    }
                }
            }
            event = parser.next()
        }
        return result
    }

    private fun safeNextText(parser: XmlPullParser): String = try { parser.nextText() } catch (_: Exception) { "" }

    private fun fetchSerbia(source: Source): List<Item> {
        val connection = open(source.url)
        val html = try { connection.inputStream.bufferedReader(Charsets.UTF_8).use { it.readText() } }
        finally { connection.disconnect() }
        val pattern = Regex("(?is)<a\\b[^>]*href=[\\\"']([^\\\"']*/rs/obavestenje/[^\\\"']+)[\\\"'][^>]*>(.*?)</a>")
        val seen = linkedSetOf<String>()
        val result = mutableListOf<Item>()
        for (match in pattern.findAll(html)) {
            val path = match.groupValues[1]
            val title = plainText(match.groupValues[2]).take(240)
            val link = if (path.startsWith("http")) path else "https://www.cert.rs" + if (path.startsWith("/")) path else "/$path"
            if (title.length < 8 || !seen.add(link)) continue
            result.add(makeItem(title, "", "", link, source) ?: continue)
            if (result.size >= 30) break
        }
        if (result.isEmpty()) throw IllegalStateException("No advisory links could be parsed from the CERT page")
        return result
    }

    private fun makeItem(titleRaw: String, bodyRaw: String, dateRaw: String, linkRaw: String, source: Source): Item? {
        val title = plainText(titleRaw).take(240)
        if (title.length < 5) return null
        val link = linkRaw.trim().let {
            when {
                it.startsWith("http://") || it.startsWith("https://") -> it
                it.startsWith("/") -> URL(URL(source.url), it).toString()
                else -> source.url
            }
        }
        if (!link.startsWith("https://")) return null
        val body = plainText(bodyRaw)
        val excerpt = when {
            body.isBlank() -> ""
            body.equals(title, true) -> ""
            else -> body.take(260).let { if (body.length > 260) "$it…" else it }
        }
        return Item(title, excerpt, normalizeDate(dateRaw), link, source.id, source.label, classify("$title $body"))
    }

    @Suppress("DEPRECATION")
    private fun plainText(value: String): String = Html.fromHtml(value.replace(Regex("(?i)<br\\s*/?>"), " "))
        .toString().replace(Regex("\\s+"), " ").trim()

    private fun normalizeDate(raw: String): String {
        val value = raw.trim()
        if (value.isEmpty()) return ""
        val patterns = listOf(
            "EEE, dd MMM yyyy HH:mm:ss Z", "EEE, dd MMM yyyy HH:mm:ss zzz",
            "yyyy-MM-dd'T'HH:mm:ssXXX", "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "yyyy-MM-dd"
        )
        for (pattern in patterns) try {
            val parser = SimpleDateFormat(pattern, Locale.US).apply { isLenient = true }
            parser.timeZone = TimeZone.getTimeZone("UTC")
            val parsed = parser.parse(value) ?: continue
            return SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply { timeZone = TimeZone.getTimeZone("UTC") }.format(parsed)
        } catch (_: Exception) { }
        return value.take(80)
    }

    private fun classify(text: String): String {
        val s = text.lowercase(Locale.ROOT)
        val ordinary = listOf("phishing", "scam", "fraud", "prevara", "krađa identiteta", "kradja identiteta", "sms", "email", "e-mail", "fake", "lažni", "lazni", "password", "lozink", "bank", "parcel", "package", "qr code", "social engineering", "identity theft", "card details")
            .any { s.contains(it) }
        val admin = listOf("cve-", "php", "linux kernel", "apache", "nginx", "server", "firewall", "backdoor", "remote code execution", "rce", "privilege escalation", "firmware", "patch", "teamviewer", "fortinet", "vulnerability", "ranjivost", "exploit")
            .any { s.contains(it) }
        return when { ordinary && admin -> "mixed"; ordinary -> "ordinary"; else -> "administrators" }
    }
}
