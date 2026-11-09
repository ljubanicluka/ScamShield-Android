using System.Collections.ObjectModel;
using System.Globalization;
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
    private Grid _shell = new();
    private VerticalStackLayout _body = new() { Spacing = 14, Padding = new Thickness(16, 12) };
    private readonly LiveFeedService _feedService = new();
    private LiveFeedSnapshot? _liveFeeds;
    private bool _isRefreshingFeeds;
    private CameraBarcodeReaderView? _camera;
    private Label? _scanValue;
    private Label? _scanAssessment;
    private Label? _cameraStatus;
    private bool _useCyrillic;
    private string _birthYear = "";
    private bool _dark;
    private string _name = "korisnik";
    private string _country = "Srbija";
    private string _currentTab = "home";

    public MainPage()
    {
        Title = "ScamShield";
        NavigationPage.SetHasNavigationBar(this, false);
        _name = NormalizeUsername(Preferences.Get("profile_name", "korisnik"));
        _country = Preferences.Get("profile_country", "Srbija");
        _dark = Preferences.Get("dark_theme", false);
        _useCyrillic = Preferences.Get("use_cyrillic", false);
        _birthYear = Preferences.Get("birth_year", "");
        if (Preferences.Get("setup_complete", false))
        {
            BuildShell();
            Navigate("home");
        }
        else
        {
            Content = BuildOnboarding();
        }

        // Read-only public advisory requests; no profile or scanned content is uploaded.
        _ = RefreshLiveFeedsAsync();
    }

    private View BuildOnboarding()
    {
        var nameEntry = new Entry { Placeholder = "Korisničko ime (npr. luka)", Text = _name, BackgroundColor = CardColor(), TextColor = TextColor(), PlaceholderColor = Muted, MaxLength = 32 };
        nameEntry.TextChanged += (_, args) =>
        {
            var normalized = (args.NewTextValue ?? "").ToLowerInvariant();
            if (normalized != args.NewTextValue) nameEntry.Text = normalized;
        };
        var countryPicker = new Picker { Title = "Izaberi državu", TextColor = TextColor(), BackgroundColor = CardColor(), ItemsSource = new List<string> { "Srbija", "Bosna i Hercegovina", "Crna Gora", "Severna Makedonija", "Drugo" }, SelectedItem = Ui(_country) };
        var birthYearEntry = new Entry { Placeholder = "Godina rođenja (npr. 2009)", Text = _birthYear, Keyboard = Keyboard.Numeric, BackgroundColor = CardColor(), TextColor = TextColor(), PlaceholderColor = Muted, MaxLength = 4 };
        var intro = new VerticalStackLayout { Spacing = 12, Padding = 24, VerticalOptions = LayoutOptions.Center };
        intro.Add(new Label { Text = "SCAMSHIELD", FontSize = 13, CharacterSpacing = 3, TextColor = _dark ? Yellow : Blue, FontAttributes = FontAttributes.Bold });
        intro.Add(new Label { Text = "Prepoznaj prevaru.\nPomozi drugima.", FontSize = 32, FontAttributes = FontAttributes.Bold, TextColor = TextColor(), LineBreakMode = LineBreakMode.WordWrap });
        intro.Add(new Label { Text = "Zajednica i bezbednosni alati na jednom mestu. Podešavanja možeš promeniti kasnije.", FontSize = 15, TextColor = Muted });
        intro.Add(SectionLabel("Kako da podesimo ScamShield"));
        intro.Add(new Label { Text = "Korisničko ime", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        intro.Add(nameEntry);
        intro.Add(new Label { Text = "Država", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        intro.Add(countryPicker);
        intro.Add(new Label { Text = "Godina rođenja (opciono)", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        intro.Add(birthYearEntry);
        intro.Add(new Label { Text = "Koristi se samo za prikaz približne uzrasne grupe. Ne prikazuje se javno i čuva se lokalno na ovom uređaju.", FontSize = 12, TextColor = Muted });
        intro.Add(ActionButton("Nastavi", () =>
        {
            _name = NormalizeUsername(nameEntry.Text);
            _country = SerbianCyrillicToLatin(countryPicker.SelectedItem?.ToString() ?? "Srbija");
            var yearText = (birthYearEntry.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(yearText) &&
                (!int.TryParse(yearText, out var parsedYear) || parsedYear < 1900 || parsedYear > DateTime.Now.Year))
            {
                _ = DisplayAlertAsync("Proveri godinu", $"Unesi godinu između 1900. i {DateTime.Now.Year}, ili ostavi polje prazno.", "U redu");
                return;
            }
            _birthYear = yearText;
            Preferences.Set("profile_name", _name);
            Preferences.Set("profile_country", _country);
            Preferences.Set("birth_year", _birthYear);
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
        var onboardingScroll = new ScrollView { Content = intro, BackgroundColor = PageColor() };
        ApplyScriptToView(onboardingScroll);
        return onboardingScroll;
    }

    private void BuildShell()
    {
        // Recreate the root and body instead of reparenting controls from an attached native view tree.
        _shell = new Grid();
        _body = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(16, 12) };
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
        foreach (var tab in new[] { ("home", "Početna"), ("alerts", "Upozorenja"), ("community", "Zajednica"), ("qr", "QR"), ("check", "Provera"), ("map", "Mapa"), ("family", "Porodica") })
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
        footer.Add(new Label { Text = Ui("Zvanični izvori • QR linkovi se ne otvaraju automatski"), FontSize = 10, TextColor = Muted, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center });
        _shell.Add(footer, 0, 3);
        _shell.BackgroundColor = PageColor();
        Content = _shell;
    }

    private void Navigate(string tab)
    {
        if (_camera is not null) _camera.IsDetecting = false;
        _currentTab = tab;
        _body.Clear();
        _body.BackgroundColor = PageColor();
        switch (tab)
        {
            case "home": BuildHome(); break;
            case "alerts": BuildAlerts(); break;
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
        ApplyScriptToView(_shell);
    }

    private string TabLabel(string tab) => tab switch { "home" => "Početna", "alerts" => "Upozorenja", "community" => "Zajednica", "qr" => "QR", "check" => "Provera", "map" => "Mapa", "family" => "Porodica", _ => "" };

    private async Task RefreshLiveFeedsAsync()
    {
        if (_isRefreshingFeeds) return;
        _isRefreshingFeeds = true;
        try
        {
            _liveFeeds = await _feedService.RefreshAsync();
        }
        catch
        {
            // Per-source failures are represented by LiveFeedService.
        }
        finally
        {
            _isRefreshingFeeds = false;
        }

        if (Preferences.Get("setup_complete", false))
            MainThread.BeginInvokeOnMainThread(() => Navigate(_currentTab));
    }

    private View BuildLiveAlertCard(LiveAlertItem item)
    {
        var card = Card();
        card.Add(new Label
        {
            Text = Ui(item.SourceName + " • " + item.Coverage),
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = _dark ? Yellow : Blue
        });
        card.Add(new Label
        {
            Text = Ui(item.Title),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor()
        });
        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            card.Add(new Label { Text = Ui(item.Summary), FontSize = 13, TextColor = TextColor() });
        }

        var dateText = item.PublishedAt.HasValue
            ? item.PublishedAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("sr-Latn-RS"))
            : "Datum nije naveden u listi izvora";
        card.Add(new Label { Text = Ui(dateText), FontSize = 10, TextColor = Muted });
        card.Add(LinkButton(Ui("Otvori originalno obaveštenje"), item.Url));
        return card;
    }

    private void BuildAlerts()
    {
        _body.Add(SectionLabel("ZVANIČNA UPOZORENJA"));
        _body.Add(new Label { Text = Ui("Aktuelne objave regionalnih CERT/CIRT izvora"), FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label
        {
            Text = Ui("Vesti dolaze direktno sa navedenih izvora. To nisu prijave korisnika niti potvrda da je određeni broj ili link prevara."),
            FontSize = 12,
            TextColor = Muted
        });
        _body.Add(ActionButton(
            _isRefreshingFeeds ? Ui("Osvežavanje...") : Ui("Osveži izvore"),
            () => _ = RefreshLiveFeedsAsync(),
            true));

        if (_liveFeeds is null)
        {
            _body.Add(Card(new Label { Text = Ui("Učitavaju se javni izvori..."), FontSize = 14, TextColor = TextColor() }));
            return;
        }

        if (_liveFeeds.IsUsingCachedData)
        {
            _body.Add(new Label
            {
                Text = Ui("Prikazuju se sačuvane objave jer trenutno osvežavanje nije vratilo podatke."),
                FontSize = 12,
                TextColor = Muted
            });
        }

        _body.Add(SectionLabel("STATUS IZVORA"));
        foreach (var source in _liveFeeds.Sources)
        {
            var sourceCard = Card();
            sourceCard.Add(new Label { Text = Ui(source.SourceName), FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
            sourceCard.Add(new Label { Text = Ui(source.Coverage), FontSize = 11, TextColor = Muted });
            var statusText = source.IsAvailable
                ? "Dostupan • " + source.ItemCount.ToString(CultureInfo.InvariantCulture) + " objava"
                : source.Message;
            sourceCard.Add(new Label { Text = Ui(statusText), FontSize = 11, TextColor = source.IsAvailable ? (_dark ? Yellow : Blue) : Muted });
            sourceCard.Add(LinkButton(Ui("Zvanični izvor"), source.Url));
            _body.Add(sourceCard);
        }

        _body.Add(SectionLabel("OBJAVE"));
        if (_liveFeeds.Items.Count == 0)
        {
            _body.Add(Card(new Label
            {
                Text = Ui("Trenutno nema dostupnih objava. Proveri status izvora ili otvori njihov zvanični sajt."),
                FontSize = 13,
                TextColor = Muted
            }));
            return;
        }

        foreach (var item in _liveFeeds.Items)
            _body.Add(BuildLiveAlertCard(item));
    }

    private void BuildHome()
    {
        _body.Add(new Label { Text = $"Zdravo, {_name}", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = _dark ? Yellow : Blue });
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
        _body.Add(SectionLabel("Najnovija zvanična upozorenja"));
        if (_liveFeeds?.Items.FirstOrDefault() is LiveAlertItem latest)
            _body.Add(BuildLiveAlertCard(latest));
        else
            _body.Add(Card(new Label { Text = Ui("Učitavaju se javni izvori upozorenja."), FontSize = 13, TextColor = Muted }));
        _body.Add(ActionButton(Ui("Sva zvanična upozorenja"), () => Navigate("alerts"), false));
        var sources = Card();
        sources.Add(new Label { Text = "Zvanična upozorenja", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        sources.Add(new Label { Text = "Regionalna upozorenja se razlikuju po državi i načinu objavljivanja. Otvori izvor i proveri datum objave.", FontSize = 12, TextColor = Muted });
        sources.Add(LinkButton("Nacionalni CERT Srbije", "https://www.cert.rs/rs/obavestenja.html"));
        sources.Add(LinkButton("CIRT Crne Gore", "https://www.gov.me/gov-cirt"));
        sources.Add(LinkButton("CERT Republike Srpske", "https://cert.aikt.rs/"));
        sources.Add(LinkButton("MKD-CIRT", "https://mkd-cirt.mk/"));
        _body.Add(sources);
        _body.Add(ActionButton("Podešavanja", () => Navigate("settings"), false));
    }

    private void BuildCommunity()
    {
        _body.Add(SectionLabel("ZAJEDNICA"));
        _body.Add(new Label { Text = "Ljudi pomažu ljudima.", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label
        {
            Text = Ui("Ovde će se prikazivati stvarne objave korisnika kada bude povezan zajednički server. Ne prikazujemo izmišljene profile, lažne glasove ni probne prijave."),
            FontSize = 13,
            TextColor = Muted
        });
        _body.Add(Card(
            new Label { Text = Ui("Zajednički feed trenutno nije povezan."), FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = TextColor() },
            new Label { Text = Ui("Tvoje objave se ne šalju nikome iz ove verzije aplikacije. Ne unosi podatke kartice, lozinke, kodove ili druge osetljive podatke."), FontSize = 12, TextColor = Muted },
            ActionButton(Ui("Otvori zvanična upozorenja"), () => Navigate("alerts"), true)));
    }

    private void BuildQr()
    {
        _body.Add(SectionLabel("QR ZAŠTITA"));
        _body.Add(new Label { Text = "Skeniraj pre nego što otvoriš.", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Usmeri kameru ka QR kodu. Rezultat se prikazuje ovde; nijedan link se ne otvara automatski.", FontSize = 13, TextColor = Muted });

        var cameraBox = Card();
        _camera = new CameraBarcodeReaderView
        {
            HeightRequest = 280,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            CameraLocation = CameraLocation.Rear,
            IsDetecting = false,
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                TryHarder = true,
                Multiple = false,
                DelayBetweenAnalyzingFrames = 100,
                InitialDelayBeforeAnalyzingFrames = 250,
                DelayBetweenContinuousScans = 900
            }
        };
        _camera.BarcodesDetected += OnBarcodesDetected;
        cameraBox.Add(_camera);
        _cameraStatus = new Label { Text = "Kamera nije pokrenuta.", FontSize = 12, TextColor = Muted };
        cameraBox.Add(_cameraStatus);
        cameraBox.Add(ActionButton("Pokreni / nastavi skeniranje", () => _ = StartCameraAsync(), true));
        var cameraActions = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        cameraActions.Add(ActionButton("Zaustavi kameru", StopCamera, false), 0, 0);
        cameraActions.Add(ActionButton("Blic", ToggleTorch, false), 1, 0);
        cameraBox.Add(cameraActions);
        cameraBox.Add(ActionButton("Uvezi QR sa slike", () => _ = ImportQrImageAsync(), false));
        _body.Add(cameraBox);

        _scanValue = new Label { Text = "Sadržaj QR koda pojaviće se ovde.", FontSize = 13, TextColor = Muted };
        _scanAssessment = new Label { Text = "Čeka se skeniranje.", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = TextColor() };
        _body.Add(Card(
            new Label { Text = "Rezultat analize", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = TextColor() },
            _scanAssessment,
            _scanValue));
        _body.Add(new Label { Text = "Napomena: ovo je lokalna heuristička provera, ne udaljena reputaciona baza niti izolovana virtuelna mašina.", FontSize = 11, TextColor = Muted });
    }

    private async Task StartCameraAsync()
    {
        try
        {
            if (!BarcodeScanning.IsSupported)
            {
                if (_cameraStatus is not null) _cameraStatus.Text = Ui("Ovaj uređaj ne prijavljuje dostupnu kameru.");
                await DisplayAlertAsync(Ui("Kamera nije dostupna"), Ui("Uređaj ili sistem trenutno ne podržava pristup kameri."), Ui("U redu"));
                return;
            }

            var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
                permission = await Permissions.RequestAsync<Permissions.Camera>();

            if (permission != PermissionStatus.Granted)
            {
                if (_cameraStatus is not null) _cameraStatus.Text = Ui("Dozvola za kameru nije odobrena.");
                await DisplayAlertAsync(Ui("Dozvola potrebna"), Ui("Dozvoli ScamShield-u pristup kameri u Android podešavanjima, pa pokušaj ponovo."), Ui("U redu"));
                return;
            }

            if (_camera is null) return;
            _camera.CameraLocation = CameraLocation.Rear;
            _camera.IsDetecting = true;
            if (_cameraStatus is not null) _cameraStatus.Text = Ui("Kamera je aktivna. Drži QR kod mirno unutar kadra.");
        }
        catch (Exception ex)
        {
            if (_cameraStatus is not null) _cameraStatus.Text = Ui("Kamera nije mogla da se pokrene.");
            await DisplayAlertAsync(Ui("Greška kamere"), ex.Message, Ui("U redu"));
        }
    }

    private void StopCamera()
    {
        if (_camera is not null) _camera.IsDetecting = false;
        if (_cameraStatus is not null) _cameraStatus.Text = Ui("Kamera je zaustavljena.");
    }

    private void ToggleTorch()
    {
        if (_camera is null || !_camera.IsDetecting)
        {
            if (_cameraStatus is not null) _cameraStatus.Text = Ui("Pokreni kameru pre uključivanja blica.");
            return;
        }
        _camera.IsTorchOn = !_camera.IsTorchOn;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Value))?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_camera is not null) _camera.IsDetecting = false;
            if (_cameraStatus is not null) _cameraStatus.Text = Ui("QR kod je prepoznat. Kamera je pauzirana.");
            if (_scanValue is not null) _scanValue.Text = value;
            if (_scanAssessment is not null) _scanAssessment.Text = Ui(Assess(value));
        });
    }

    private async Task ImportQrImageAsync()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Ui("Izaberi QR kod ili screenshot"),
                FileTypes = FilePickerFileType.Images
            });
            if (file is null) return;

            await using var stream = await file.OpenReadAsync();
            var results = await BarcodeReader.DecodeAsync(
                stream,
                new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.All,
                    AutoRotate = true,
                    TryHarder = true,
                    Multiple = true
                });

            var decoded = results?.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result.Value));
            if (decoded is null)
            {
                await DisplayAlertAsync(Ui("QR nije pronađen"), Ui("Na slici nije pronađen čitljiv QR/bar-kod. Probaj originalnu sliku, ne zamućen screenshot ili isečak."), Ui("U redu"));
                return;
            }

            var value = decoded.Value;
            if (_scanValue is not null) _scanValue.Text = value;
            if (_scanAssessment is not null) _scanAssessment.Text = Ui(Assess(value));
            if (_cameraStatus is not null) _cameraStatus.Text = Ui("Slika je obrađena.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Ui("Uvoz nije uspeo"), Ui("Nije moguće obraditi izabranu sliku: ") + ex.Message, Ui("U redu"));
        }
    }

    private void BuildCheck()
    {
        _body.Add(SectionLabel("BRZA PROVERA"));
        _body.Add(new Label { Text = "Ne veruj poruci na prvi pogled.", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = TextColor() });
        _body.Add(new Label { Text = "Nalepi link, broj ili tekst poruke. Analiza se radi lokalno u ovoj demo verziji.", FontSize = 13, TextColor = Muted });
        var input = new Editor { Placeholder = "Nalepi sumnjiv link ili poruku ovde...", MinimumHeightRequest = 120, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = CardColor(), TextColor = TextColor() };
        var result = new Label { Text = "Rezultat će se pojaviti nakon provere.", FontSize = 14, TextColor = Muted };
        _body.Add(Card(input, ActionButton("Proveri sadržaj", () => result.Text = Ui(Assess(input.Text ?? "")), true), result));
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
        _body.Add(ActionButton(_useCyrillic ? "Prikaži latinicu" : "Prikaži ćirilicu", ToggleScript, false));
        _body.Add(new Label { Text = "Jezik interfejsa: srpski. Engleski prevod još nije uključen.", FontSize = 11, TextColor = Muted });
        _body.Add(ActionButton("Ponovi početno podešavanje", () =>
        {
            Preferences.Remove("setup_complete");
            Content = BuildOnboarding();
        }, false));
        _body.Add(new Label { Text = "Privatnost: profil i demo objave ostaju lokalno na uređaju. Nema naloga, sinhronizacije ni daljinskog nadzora u ovom APK-u.", FontSize = 11, TextColor = Muted });
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
        if (_camera is not null) _camera.IsDetecting = false;
        _dark = !_dark;
        Preferences.Set("dark_theme", _dark);
        BuildShell();
        Navigate(_currentTab);
    }

    private void ToggleScript()
    {
        if (_camera is not null) _camera.IsDetecting = false;
        _useCyrillic = !_useCyrillic;
        Preferences.Set("use_cyrillic", _useCyrillic);
        BuildShell();
        Navigate(_currentTab);
    }

    private static string NormalizeUsername(string? value)
    {
        var username = (value ?? "").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(username) ? "korisnik" : username;
    }

    private string Ui(string value) => _useCyrillic ? SerbianLatinToCyrillic(value) : value;

    private static string SerbianLatinToCyrillic(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("DŽ", "Џ", StringComparison.Ordinal).Replace("Dž", "Џ", StringComparison.Ordinal).Replace("dž", "џ", StringComparison.Ordinal)
            .Replace("LJ", "Љ", StringComparison.Ordinal).Replace("Lj", "Љ", StringComparison.Ordinal).Replace("lj", "љ", StringComparison.Ordinal)
            .Replace("NJ", "Њ", StringComparison.Ordinal).Replace("Nj", "Њ", StringComparison.Ordinal).Replace("nj", "њ", StringComparison.Ordinal)
            .Replace('A', 'А').Replace('B', 'Б').Replace('C', 'Ц').Replace('Č', 'Ч').Replace('Ć', 'Ћ')
            .Replace('D', 'Д').Replace('Đ', 'Ђ').Replace('E', 'Е').Replace('F', 'Ф').Replace('G', 'Г')
            .Replace('H', 'Х').Replace('I', 'И').Replace('J', 'Ј').Replace('K', 'К').Replace('L', 'Л')
            .Replace('M', 'М').Replace('N', 'Н').Replace('O', 'О').Replace('P', 'П').Replace('R', 'Р')
            .Replace('S', 'С').Replace('Š', 'Ш').Replace('T', 'Т').Replace('U', 'У').Replace('V', 'В')
            .Replace('Z', 'З').Replace('Ž', 'Ж')
            .Replace('a', 'а').Replace('b', 'б').Replace('c', 'ц').Replace('č', 'ч').Replace('ć', 'ћ')
            .Replace('d', 'д').Replace('đ', 'ђ').Replace('e', 'е').Replace('f', 'ф').Replace('g', 'г')
            .Replace('h', 'х').Replace('i', 'и').Replace('j', 'ј').Replace('k', 'к').Replace('l', 'л')
            .Replace('m', 'м').Replace('n', 'н').Replace('o', 'о').Replace('p', 'п').Replace('r', 'р')
            .Replace('s', 'с').Replace('š', 'ш').Replace('t', 'т').Replace('u', 'у').Replace('v', 'в')
            .Replace('z', 'з').Replace('ž', 'ж');
    }

    private static string SerbianCyrillicToLatin(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("Џ", "Dž", StringComparison.Ordinal).Replace("џ", "dž", StringComparison.Ordinal)
            .Replace("Љ", "Lj", StringComparison.Ordinal).Replace("љ", "lj", StringComparison.Ordinal)
            .Replace("Њ", "Nj", StringComparison.Ordinal).Replace("њ", "nj", StringComparison.Ordinal)
            .Replace('А', 'A').Replace('Б', 'B').Replace('В', 'V').Replace('Г', 'G').Replace('Д', 'D')
            .Replace('Ђ', 'Đ').Replace('Е', 'E').Replace('Ж', 'Ž').Replace('З', 'Z').Replace('И', 'I')
            .Replace('Ј', 'J').Replace('К', 'K').Replace('Л', 'L').Replace('М', 'M').Replace('Н', 'N')
            .Replace('О', 'O').Replace('П', 'P').Replace('Р', 'R').Replace('С', 'S').Replace('Т', 'T')
            .Replace('Ћ', 'Ć').Replace('У', 'U').Replace('Ф', 'F').Replace('Х', 'H').Replace('Ц', 'C')
            .Replace('Ч', 'Č').Replace('Ш', 'Š')
            .Replace('а', 'a').Replace('б', 'b').Replace('в', 'v').Replace('г', 'g').Replace('д', 'd')
            .Replace('ђ', 'đ').Replace('е', 'e').Replace('ж', 'ž').Replace('з', 'z').Replace('и', 'i')
            .Replace('ј', 'j').Replace('к', 'k').Replace('л', 'l').Replace('м', 'm').Replace('н', 'n')
            .Replace('о', 'o').Replace('п', 'p').Replace('р', 'r').Replace('с', 's').Replace('т', 't')
            .Replace('ћ', 'ć').Replace('у', 'u').Replace('ф', 'f').Replace('х', 'h').Replace('ц', 'c')
            .Replace('ч', 'č').Replace('ш', 'š');
    }

    private void ApplyScriptToView(View? view)
    {
        if (!_useCyrillic || view is null) return;
        switch (view)
        {
            case Label label:
                label.Text = SerbianLatinToCyrillic(label.Text ?? "");
                break;
            case Button button:
                button.Text = SerbianLatinToCyrillic(button.Text ?? "");
                break;
            case Entry entry:
                entry.Placeholder = SerbianLatinToCyrillic(entry.Placeholder ?? "");
                break;
            case Editor editor:
                editor.Placeholder = SerbianLatinToCyrillic(editor.Placeholder ?? "");
                break;
            case Picker picker:
                picker.Title = SerbianLatinToCyrillic(picker.Title ?? "");
                if (picker.ItemsSource is IEnumerable<string> items)
                {
                    picker.ItemsSource = items.Select(SerbianLatinToCyrillic).ToList();
                    if (picker.SelectedItem is string selected)
                        picker.SelectedItem = SerbianLatinToCyrillic(selected);
                }
                break;
        }

        if (view is ScrollView scroll && scroll.Content is View scrollContent)
            ApplyScriptToView(scrollContent);
        else if (view is ContentView contentView && contentView.Content is View content)
            ApplyScriptToView(content);

        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
                if (child is View childView) ApplyScriptToView(childView);
        }
    }

    private Color PageColor() => _dark ? DarkBg : Color.FromArgb("#F3F6FB");
    private Color CardColor() => _dark ? DarkCard : LightCard;
    private Color TextColor() => _dark ? Color.FromArgb("#F8FAFC") : Ink;

    private Label SectionLabel(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10, CharacterSpacing = 1.4, FontAttributes = FontAttributes.Bold, TextColor = _dark ? Yellow : Blue };
    private Label Bullet(string text) => new() { Text = "•  " + text, FontSize = 13, TextColor = TextColor() };
    private VerticalStackLayout Card(params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 10, Padding = 16, BackgroundColor = CardColor() };
        foreach (var child in children) stack.Add(child);
        return stack;
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
