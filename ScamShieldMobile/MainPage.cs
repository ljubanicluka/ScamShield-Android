using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ScamShieldMobile;

public sealed class MainPage : ContentPage
{
    private readonly Color Blue = Color.FromArgb("#2563EB");
    private readonly Color Yellow = Color.FromArgb("#FACC15");
    private readonly Color Ink = Color.FromArgb("#111827");
    private readonly Color Muted = Color.FromArgb("#64748B");
    private readonly Color LightCard = Colors.White;
    private readonly Color DarkBg = Color.FromArgb("#0B1220");
    private readonly Color DarkCard = Color.FromArgb("#151F31");
    private readonly Grid _shell = new();
    private readonly VerticalStackLayout _body = new() { Spacing = 14, Padding = new Thickness(16, 12) };
    private readonly ObservableCollection<CommunityPost> _posts = new();
    private CameraBarcodeReaderView? _camera;
    private Label? _scanValue;
    private Label? _scanAssessment;
    private bool _dark;
    private string _name = "korisnik";
    private string _country = "Srbija";
    private string _currentTab = "home";

    public MainPage()
    {
        Title = "ScamShield";
        NavigationPage.SetHasNavigationBar(this, false);
        _name = Preferences.Get("profile_name", "korisnik");
        _country = Preferences.Get("profile_country", "Srbija");
        _dark = Preferences.Get("dark_theme", false);
        SeedCommunity();

        if (Preferences.Get("setup_complete", false))
        {
            BuildShell();
            Navigate("home");
        }
        else
        {
            Content = BuildOnboarding();
        }
    }

    private void SeedCommunity()
    {
        _posts.Add(new CommunityPost("Meni je stigla poruka kao od poste", "trazili su da platim neku dostavu preko linka. adresa je bila bas cudna pa nisam otvorio. jel jos neko dobio ovo?", "SMS / phishing", 18, 7, "Novi Sad"));
        _posts.Add(new CommunityPost("poziv iz banke navodno", "lik zna moje ime i kaze da je sumnjiva transakcija. prekinuo sam i pozvao broj sa kartice. banka kaze da nisu zvali.", "Bankarske prevare", 31, 12, "Beograd"));
        _posts.Add(new CommunityPost("Marketplace uplata unapred", "pazite kad vam posalju kurira a onda traze podatke kartice da bi vam uplatili novac. to nije potrebno za prodaju.", "Online kupovina", 24, 5, "Subotica"));
    }

    private View BuildOnboarding()
    {
        var nameEntry = new Entry { Placeholder = "Kako da te zovemo?", Text = _name, BackgroundColor = CardColor(), TextColor = TextColor(), PlaceholderColor = Muted };
        var countryPicker = new Picker { Title = "Izaberi državu", TextColor = TextColor(), BackgroundColor = CardColor(), ItemsSource = new List<string> { "Srbija", "Bosna i Hercegovina", "Crna Gora", "Severna Makedonija", "Drugo" }, SelectedItem = _country };
        var agePicker = new Picker { Title = "Uzrast (opciono)", TextColor = TextColor(), BackgroundColor = CardColor(), ItemsSource = new List<string> { "Ispod 18", "18–24", "25–44", "45–64", "65+" } };
        var intro = new VerticalStackLayout { Spacing = 12, Padding = 24, VerticalOptions = LayoutOptions.Center };
        intro.Add(new Label { Text = "SCAMSHIELD", FontSize = 13, CharacterSpacing = 3, TextColor = _dark ? Yellow : Blue, FontAttributes = FontAttributes.Bold });
        intro.Add(new Label { Text = "Prepoznaj prevaru.\nPomozi drugima.", FontSize = 32, FontAttributes = FontAttributes.Bold, TextColor = TextColor(), LineBreakMode = LineBreakMode.WordWrap });
        intro.Add(new Label { Text = "Zajednica i bezbednosni alati na jednom mestu. Podešavanja možeš promeniti kasnije.", FontSize = 15, TextColor = Muted });
        intro.Add(SectionLabel("Kako da podesimo ScamShield"));
        intro.Add(nameEntry);
        intro.Add(countryPicker);
        intro.Add(agePicker);
        intro.Add(new Label { Text = "Podaci profila ostaju na ovom uređaju u demo verziji. Ne tražimo pol, datum rođenja ni adresu.", FontSize = 12, TextColor = Muted });
        intro.Add(ActionButton("Nastavi", () =>
        {
            _name = string.IsNullOrWhiteSpace(nameEntry.Text) ? "korisnik" : nameEntry.Text.Trim();
            _country = countryPicker.SelectedItem?.ToString() ?? "Srbija";
            Preferences.Set("profile_name", _name);
            Preferences.Set("profile_country", _country);
            Preferences.Set("age_band", agePicker.SelectedItem?.ToString() ?? "");
            Preferences.Set("setup_complete", true);
            BuildShell();
            Navigate("home");
        }, true));
        intro.Add(ActionButton("Preskoči podešavanje", () =>
        {
            Preferences.Set("setup_complete", true);
            BuildShell();
            Navigate("home");
        }, false));
        return new ScrollView { Content = intro, BackgroundColor = PageColor() };
    }

    private void BuildShell()
    {
        _shell.RowDefinitions.Clear();
        _shell.ColumnDefinitions.Clear();
        _shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Padding = new Thickness(17, 12), ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        var branding = new VerticalStackLayout { Spacing = 0 };
        branding.Add(new Label { Text = "SCAMSHIELD", FontSize = 12, CharacterSpacing = 2.2, FontAttributes = FontAttributes.Bold, TextColor = _dark ? Yellow : Blue });
        branding.Add(new Label { Text = "Zajedno protiv prevara", FontSize = 12, TextColor = Muted });
        header.Add(branding, 0, 0);
        var themeButton = new Button { Text = _dark ? "☀" : "☾", FontSize = 20, WidthRequest = 48, HeightRequest = 44, CornerRadius = 14, BackgroundColor = CardColor(), TextColor = TextColor(), Padding = 0 };
        themeButton.Clicked += (_, _) => ToggleTheme();
        header.Add(themeButton, 1, 0);
        _shell.Add(header, 0, 0);

        var nav = new HorizontalStackLayout { Padding = new Thickness(10, 7), Spacing = 7, HorizontalOptions = LayoutOptions.Fill };
        foreach (var tab in new[] { ("home", "Početna"), ("community", "Zajednica"), ("qr", "QR"), ("check", "Provera"), ("map", "Mapa"), ("family", "Porodica") })
        {
            var b = new Button { Text = tab.Item2, FontSize = 11, Padding = new Thickness(11, 9), CornerRadius = 12, BackgroundColor = _currentTab == tab.Item1 ? Blue : CardColor(), TextColor = _currentTab == tab.Item1 ? Colors.White : TextColor() };
            b.Clicked += (_, _) => Navigate(tab.Item1);
            nav.Add(b);
        }
        var navScroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = nav, HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
        _shell.Add(navScroll, 0, 1);

        var bodyScroll = new ScrollView { Content = _body, BackgroundColor = PageColor() };
        _shell.Add(bodyScroll, 0, 2);

        var footer = new Grid { Padding = new Thickness(16, 7), BackgroundColor = CardColor() };
        footer.Add(new Label { Text = "DEMO VERZIJA  •  Provera ne otvara linkove automatski", FontSize = 10, TextColor = Muted, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center });
        _shell.Add(footer, 0, 3);
        _shell.BackgroundColor = PageColor();
        Content = _shell;
    }

    private void Navigate(string tab)
    {
        _currentTab = tab;
        _body.Clear();
        _body.BackgroundColor = PageColor();
        switch (tab)
        {
            case "home": BuildHome(); break;
            case "community": BuildCommunity(); break;
            case "qr": BuildQr(); break;
            case "check": BuildCheck(); break;
            case "map": BuildMap(); break;
            case "family": BuildFamily(); break;
            case "settings": BuildSettings(); break;
            default: BuildHome(); break;
        }
        if (_shell.Children.Count > 1 && _shell.Children[1] is ScrollView sc && sc.Content is HorizontalStackLayout bar)
        {
            foreach (var child in bar.Children.OfType<Button>())
            {
                child.BackgroundColor = child.Text == TabLabel(tab) ? Blue : CardColor();
                child.TextColor = child.Text == TabLabel(tab) ? Colors.White : TextColor();
            }
        }
    }

    private string TabLabel(string tab) => tab switch { "home" => "Početna", "community" => "Zajednica", "qr" => "QR", "check" => "Provera", "map" => "Mapa", "family" => "Porodica", _ => "" };

    private void BuildHome()
    {
        _body.Add(SectionLabel($"Zdravo, {_name}"));
        _body.Add(new Label { Text = "Pre nego što klikneš — proveri.", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Proveri sumnjiv link, poruku ili QR kod. Iskustva zajednice pomažu drugima da ne naprave istu grešku.", FontSize = 14, TextColor = Muted });
        var hero = Card();
        hero.Add(new Label { Text = "Tvoj bezbednosni pregled", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        hero.Add(new Label { Text = "Nema aktivnih upozorenja sačuvanih na ovom uređaju.", FontSize = 13, TextColor = Muted });
        hero.Add(new Label { Text = $"Region: {_country}  •  lokalni demo", FontSize = 11, TextColor = Muted });
        _body.Add(hero);
        var quick = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
        var qr = ActionButton("▦  Skeniraj QR", () => Navigate("qr"), true);
        var check = ActionButton("⌕  Proveri link", () => Navigate("check"), false);
        quick.Add(qr, 0, 0); quick.Add(check, 1, 0); _body.Add(quick);
        _body.Add(SectionLabel("Šta se dešava u zajednici"));
        _body.Add(MakePostCard(_posts[0], 0));
        var sources = Card();
        sources.Add(new Label { Text = "Zvanična upozorenja", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        sources.Add(new Label { Text = "Regionalna upozorenja se razlikuju po državi i načinu objavljivanja. Otvori izvor i proveri datum objave.", FontSize = 12, TextColor = Muted });
        sources.Add(LinkButton("Nacionalni CERT Srbije", "https://cert.rs/"));
        sources.Add(LinkButton("CIRT Crne Gore", "https://www.gov.me/cirt"));
        sources.Add(LinkButton("CERT Republika Srpska", "https://cert.rs.ba/"));
        _body.Add(sources);
        _body.Add(ActionButton("Podešavanja", () => Navigate("settings"), false));
    }

    private void BuildCommunity()
    {
        _body.Add(SectionLabel("ZAJEDNICA"));
        _body.Add(new Label { Text = "Ljudi pomažu ljudima.", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Objave ispod su početni primeri za demonstraciju. U ovoj verziji nisu vidljive na drugim telefonima.", FontSize = 12, TextColor = Muted });
        var editor = new Editor { Placeholder = "šta se desilo? napiši kako jeste...", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 85, BackgroundColor = CardColor(), TextColor = TextColor() };
        var reportType = new Picker { Title = "Kategorija", ItemsSource = new List<string> { "SMS / phishing", "Bankarske prevare", "Online kupovina", "Pozivi", "Društvene mreže", "Drugo" }, SelectedIndex = 0, TextColor = TextColor() };
        _body.Add(Card(new Label { Text = "Podeli iskustvo", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = TextColor() }, editor, reportType, ActionButton("Objavi lokalni primer", () =>
        {
            if (string.IsNullOrWhiteSpace(editor.Text)) return;
            _posts.Insert(0, new CommunityPost("Novo iskustvo", editor.Text.Trim(), reportType.SelectedItem?.ToString() ?? "Ostalo", 1, 0, _country));
            Navigate("community");
        }, true)));
        for (int i = 0; i < _posts.Count; i++) _body.Add(MakePostCard(_posts[i], i));
    }

    private View MakePostCard(CommunityPost post, int index)
    {
        var card = Card();
        card.Add(new Label { Text = post.Category.ToUpperInvariant(), FontSize = 10, CharacterSpacing = 1, FontAttributes = FontAttributes.Bold, TextColor = _dark ? Yellow : Blue });
        card.Add(new Label { Text = post.Title, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        card.Add(new Label { Text = post.Body, FontSize = 14, TextColor = TextColor() });
        card.Add(new Label { Text = $"u/{post.Author}  •  {post.City}  •  primer objave", FontSize = 10, TextColor = Muted });
        var actions = new HorizontalStackLayout { Spacing = 16 };
        var vote = new Button { Text = $"▲  {post.Votes}", FontSize = 12, BackgroundColor = CardColor(), TextColor = TextColor(), CornerRadius = 12 };
        vote.Clicked += (_, _) => { post.Votes++; vote.Text = $"▲  {post.Votes}"; };
        var comments = new Button { Text = $"Komentari ({post.Comments})", FontSize = 12, BackgroundColor = CardColor(), TextColor = TextColor(), CornerRadius = 12 };
        comments.Clicked += async (_, _) => await DisplayAlert("Komentari", "Komentari su lokalni demonstracioni podaci. Online sinhronizacija još nije povezana.", "U redu");
        actions.Add(vote); actions.Add(comments); card.Add(actions);
        return card;
    }

    private void BuildQr()
    {
        _body.Add(SectionLabel("QR ZAŠTITA"));
        _body.Add(new Label { Text = "Skeniraj pre nego što otvoriš.", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Usmeri kameru ka QR kodu. Prikazaćemo sadržaj i proveriti osnovne znakove rizika. Ništa se ne otvara automatski.", FontSize = 13, TextColor = Muted });
        var cameraBox = Card();
        _camera = new CameraBarcodeReaderView { HeightRequest = 300, IsDetecting = false, Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false } };
        _camera.BarcodesDetected += OnBarcodesDetected;
        cameraBox.Add(_camera);
        cameraBox.Add(ActionButton("Pokreni kameru", async () =>
        {
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission == PermissionStatus.Granted && _camera is not null)
            {
                _camera.IsDetecting = true;
                await DisplayAlert("Kamera aktivna", "Drži QR kod unutar okvira. Link se neće otvoriti.", "U redu");
            }
            else await DisplayAlert("Dozvola potrebna", "Dozvoli pristup kameri u Android podešavanjima da bi skener radio.", "U redu");
        }, true));
        cameraBox.Add(ActionButton("Uvezi QR sa slike", ImportQrImage, false));
        _body.Add(cameraBox);
        _scanValue = new Label { Text = "Sadržaj QR koda pojaviće se ovde.", FontSize = 13, TextColor = Muted };
        _scanAssessment = new Label { Text = "Čeka se skeniranje.", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = TextColor() };
        var result = Card(new Label { Text = "Rezultat analize", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() }, _scanAssessment, _scanValue);
        _body.Add(result);
        _body.Add(new Label { Text = "Napomena: ova verzija radi lokalnu heurističku proveru. Nije izolovana virtuelna mašina niti zamena za proveru domena kod pouzdanog servisa.", FontSize = 11, TextColor = Muted });
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_camera is not null) _camera.IsDetecting = false;
            if (_scanValue is not null) _scanValue.Text = value;
            if (_scanAssessment is not null) _scanAssessment.Text = Assess(value);
        });
    }

    private async void ImportQrImage()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Izaberi QR kod ili screenshot",
                FileTypes = FilePickerFileType.Images
            });
            if (file is null) return;
            await using var stream = await file.OpenReadAsync();
            var result = await BarcodeReader.DecodeAsync(stream);
            var value = result?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                await DisplayAlert("QR nije pronađen", "Na izabranoj slici nije pronađen čitljiv QR kod.", "U redu");
                return;
            }
            if (_scanValue is not null) _scanValue.Text = value;
            if (_scanAssessment is not null) _scanAssessment.Text = Assess(value);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Uvoz nije uspeo", "Nije moguće pročitati sliku. Pokušaj drugi screenshot ili kameru.\n" + ex.Message, "U redu");
        }
    }

    private void BuildCheck()
    {
        _body.Add(SectionLabel("BRZA PROVERA"));
        _body.Add(new Label { Text = "Ne veruj poruci na prvi pogled.", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Nalepi link, broj ili tekst poruke. Analiza se radi lokalno u ovoj demo verziji.", FontSize = 13, TextColor = Muted });
        var input = new Editor { Placeholder = "Nalepi sumnjiv link ili poruku ovde...", MinimumHeightRequest = 120, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = CardColor(), TextColor = TextColor() };
        var result = new Label { Text = "Rezultat će se pojaviti nakon provere.", FontSize = 14, TextColor = Muted };
        _body.Add(Card(input, ActionButton("Proveri sadržaj", () => result.Text = Assess(input.Text ?? ""), true), result));
    }

    private void BuildMap()
    {
        _body.Add(SectionLabel("REGIONALNI PREGLED"));
        _body.Add(new Label { Text = "Gde se prevare prijavljuju", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Ovo je demonstracioni prikaz, a ne mapa potvrđenih prijava uživo. Brojevi nisu predstavljeni kao stvarna statistika.", FontSize = 12, TextColor = Muted });
        foreach (var row in new[] { ("Srbija", "SMS i lažne dostave"), ("Bosna i Hercegovina", "Lažni bankarski pozivi"), ("Crna Gora", "Phishing i lažne nagrade"), ("Severna Makedonija", "Dodatni regionalni pregled") })
        {
            var card = Card(new Label { Text = row.Item1, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() }, new Label { Text = row.Item2, FontSize = 13, TextColor = Muted }, new Label { Text = "Izvor: prikaz primera • nema potvrđenog broja prijava", FontSize = 10, TextColor = Muted });
            _body.Add(card);
        }
    }

    private void BuildFamily()
    {
        _body.Add(SectionLabel("PORODIČNA ZAŠTITA"));
        _body.Add(new Label { Text = "Pazimo jedni na druge.", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "ScamShield porodični krug je zamišljen kao dobrovoljno deljenje upozorenja i saveta između članova porodice.", FontSize = 13, TextColor = Muted });
        _body.Add(Card(new Label { Text = "Kako bi trebalo da radi", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() },
            Bullet("Član porodice sam prihvata poziv u krug."),
            Bullet("Može da podeli upozorenje ili zatraži pomoć."),
            Bullet("Članovi vide samo ono što je osoba izabrala da podeli."),
            Bullet("Nema tajnog praćenja pretrage, poruka ili aktivnosti na telefonu."),
            new Label { Text = "Status: lokalni koncept — cloud povezivanje još nije uključeno.", FontSize = 11, TextColor = Muted }));
        _body.Add(ActionButton("Pogledaj podešavanja", () => Navigate("settings"), false));
    }

    private void BuildSettings()
    {
        _body.Add(SectionLabel("PODEŠAVANJA"));
        _body.Add(new Label { Text = "Podesi svoj ScamShield", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(Card(new Label { Text = $"Profil: {_name}", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = TextColor() }, new Label { Text = $"Država: {_country}", FontSize = 13, TextColor = Muted }));
        _body.Add(ActionButton(_dark ? "Uključi svetlu temu" : "Uključi tamnu temu", ToggleTheme, false));
        _body.Add(ActionButton("Promeni jezik (srpski / ćirilica / English)", async () => await DisplayAlert("Jezik", "Promena jezika biće uključena u sledećoj iteraciji. Trenutni interfejs je na srpskom latinicom.", "U redu"), false));
        _body.Add(ActionButton("Ponovi početno podešavanje", () =>
        {
            Preferences.Remove("setup_complete");
            Content = BuildOnboarding();
        }, false));
        _body.Add(new Label { Text = "Privatnost: profil i demo objave ostaju lokalno na uređaju. Nema naloga, sinhronizacije ni daljinskog nadzora u ovom APK-u.", FontSize = 11, TextColor = Muted));
    }

    private string Assess(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Nema sadržaja za proveru.";
        var s = value.Trim();
        var score = 0;
        var reasons = new List<string>();
        if (s.Contains("xn--", StringComparison.OrdinalIgnoreCase)) { score += 2; reasons.Add("domen koristi punycode zapis"); }
        if (s.Contains("@")) { score += 2; reasons.Add("znak @ u linku može da sakrije pravi domen"); }
        if (s.Contains("bit.ly", StringComparison.OrdinalIgnoreCase) || s.Contains("tinyurl", StringComparison.OrdinalIgnoreCase) || s.Contains("t.co/", StringComparison.OrdinalIgnoreCase)) { score += 2; reasons.Add("skraćeni link skriva krajnju adresu"); }
        if (s.Contains("password", StringComparison.OrdinalIgnoreCase) || s.Contains("lozink", StringComparison.OrdinalIgnoreCase) || s.Contains("pin", StringComparison.OrdinalIgnoreCase) || s.Contains("otp", StringComparison.OrdinalIgnoreCase)) { score += 2; reasons.Add("traže se osetljivi podaci"); }
        if (s.Contains("urgent", StringComparison.OrdinalIgnoreCase) || s.Contains("hitno", StringComparison.OrdinalIgnoreCase) || s.Contains("odmah", StringComparison.OrdinalIgnoreCase) || s.Contains("suspended", StringComparison.OrdinalIgnoreCase)) { score++; reasons.Add("poruka stvara pritisak da reaguješ odmah"); }
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) { score++; reasons.Add("koristi se nezaštićen HTTP"); }
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"https?://\d{1,3}(\.\d{1,3}){3}", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { score += 2; reasons.Add("adresa koristi IP umesto uobičajenog domena"); }
        var verdict = score >= 4 ? "Povišen rizik — ne otvaraj link" : score >= 2 ? "Sumnjivo — proveri dodatno" : "Nema očiglednih znakova — to nije garancija bezbednosti";
        return verdict + (reasons.Count > 0 ? "\n\nRazlozi: " + string.Join("; ", reasons) : "\n\nNisu pronađeni osnovni znaci iz lokalne liste. Ovo nije potvrda da je sadržaj bezbedan.");
    }

    private void ToggleTheme()
    {
        _dark = !_dark;
        Preferences.Set("dark_theme", _dark);
        BuildShell();
        Navigate(_currentTab);
    }

    private Color PageColor() => _dark ? DarkBg : Color.FromArgb("#F3F6FB");
    private Color CardColor() => _dark ? DarkCard : LightCard;
    private Color TextColor() => _dark ? Color.FromArgb("#F8FAFC") : Ink;

    private Label SectionLabel(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10, CharacterSpacing = 1.4, FontAttributes = FontAttributes.Bold, TextColor = _dark ? Yellow : Blue };
    private Label Bullet(string text) => new() { Text = "•  " + text, FontSize = 13, TextColor = TextColor() };
    private Frame Card(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 10 };
        foreach (var child in children) stack.Add(child);
        return new Frame { Content = stack, Padding = 16, CornerRadius = 18, HasShadow = false, BackgroundColor = CardColor(), BorderColor = _dark ? Color.FromArgb("#26364D") : Color.FromArgb("#E2E8F0") };
    }
    private Button ActionButton(string text, Action action, bool primary)
    {
        var button = new Button { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, Padding = new Thickness(15, 12), CornerRadius = 13, BackgroundColor = primary ? Blue : CardColor(), TextColor = primary ? Colors.White : TextColor(), HorizontalOptions = LayoutOptions.Fill };
        button.Clicked += (_, _) => action();
        return button;
    }
    private Button LinkButton(string text, string url) => ActionButton(text, async () => await Launcher.OpenAsync(url), false);
    private static Label MakeLabel(string text, double size, Color color) => new() { Text = text, FontSize = size, TextColor = color };
}

public sealed class CommunityPost
{
    public CommunityPost(string title, string body, string category, int votes, int comments, string city)
    {
        Title = title; Body = body; Category = category; Votes = votes; Comments = comments; City = city;
    }
    public string Title { get; }
    public string Body { get; }
    public string Category { get; }
    public int Votes { get; set; }
    public int Comments { get; }
    public string City { get; }
    public string Author => "korisnik";
}
