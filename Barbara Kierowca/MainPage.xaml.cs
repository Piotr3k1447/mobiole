namespace Barbara_Kierowca;

public partial class MainPage : ContentPage
{
    private string company = "";
    private string zone = "Europe/Warsaw";
    private int day;
    private string hour = "";
    private Trip? selected;
    private List<Trip> trips = [];
    private bool busy;
    private bool initialized;
    private VerticalStackLayout? passengerList;
    private Label? trackerStatus;

    public MainPage()
    {
        InitializeComponent();
        Dispatcher.StartTimer(TimeSpan.FromSeconds(15), () =>
        {
            if (selected != null && !busy) _ = RefreshPassengersAsync();
            return true;
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized) return;
        initialized = true;
        try
        {
            company = Preferences.Default.Get("company", "");
            zone = Preferences.Default.Get("zone", "Europe/Warsaw");
            if (string.IsNullOrEmpty(await SecureStorage.Default.GetAsync("token"))) ShowLogin();
            else if (!string.IsNullOrEmpty(Preferences.Default.Get("tracking_trip", "")))
            {
                company = Preferences.Default.Get("tracking_company", company);
                selected = new Trip { Id = Preferences.Default.Get("tracking_trip", ""), Code = "Aktywny kurs", Status = "departed" };
                ShowTrip();
                await RefreshPassengersAsync();
            }
            else if (string.IsNullOrEmpty(company)) ShowLogin();
            else await LoadTripsAsync();
        }
        catch (Exception e) { ShowLogin(); await ShowErrorAsync(e); }
    }

    private void Header(string title)
    {
        Root.Clear();
        Root.Add(new Label { Text = "BARBARA KIEROWCA", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB") });
        Root.Add(new Label { Text = title, FontSize = 28, FontAttributes = FontAttributes.Bold });
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        try { await action(); }
        catch (Exception e) { await ShowErrorAsync(e); }
        finally { busy = false; }
    }

    private async Task ShowErrorAsync(Exception e)
    {
        if (e is DriverApiException { Status: 401 })
        {
            LocationTracker.Stop();
            SecureStorage.Default.Remove("token");
            selected = null;
            ShowLogin();
        }
        await DisplayAlertAsync("Informacja", e.Message, "OK");
    }

    private void ShowLogin()
    {
        Header("Logowanie kierowcy");
        var server = new Entry { Placeholder = "Adres serwera HTTPS", Text = Preferences.Default.Get("server", "https://beta.barbara.net.pl"), Keyboard = Keyboard.Url };
        var email = new Entry { Placeholder = "Adres e-mail", Keyboard = Keyboard.Email };
        var password = new Entry { Placeholder = "Hasło", IsPassword = true };
        Root.Add(server); Root.Add(email); Root.Add(password);
        Root.Add(Button("Zaloguj", () => RunAsync(async () =>
        {
            Preferences.Default.Set("server", (server.Text ?? "").Trim().TrimEnd('/'));
            var result = await DriverApi.CallAsync<LoginReply>("login", new { email = (email.Text ?? "").Trim(), password = password.Text ?? "" });
            password.Text = "";
            await SecureStorage.Default.SetAsync("token", result.Token);
            ShowCompanies(result.Companies);
        })));
    }

    private void ShowCompanies(List<Company> companies)
    {
        Header("Wybierz firmę");
        if (companies.Count == 0) Root.Add(new Label { Text = "Brak dostępnych firm." });
        foreach (var item in companies)
            Root.Add(Button(item.Name, () => RunAsync(async () =>
            {
                company = item.Id; zone = item.Timezone;
                Preferences.Default.Set("company", company); Preferences.Default.Set("zone", zone);
                await LoadTripsCoreAsync();
            })));
    }

    private TimeZoneInfo TimeZone
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(zone); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
        }
    }
    private string TripHour(Trip trip) => trip.DepartureAt is { } date ? TimeZoneInfo.ConvertTime(date, TimeZone).ToString("HH:mm") : "";
    private string Date => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone).Date.AddDays(day).ToString("yyyy-MM-dd");
    private Task LoadTripsAsync() => RunAsync(LoadTripsCoreAsync);
    private async Task LoadTripsCoreAsync()
    {
        selected = null;
        trips = [];
        ShowTrips();
        var result = await DriverApi.CallAsync<TripsReply>("trips", query: new() { ["company_id"] = company, ["date"] = Date });
        trips = result.Trips;
        ShowTrips();
    }

    private void ShowTrips()
    {
        Header("Kursy");
        var days = new HorizontalStackLayout { Spacing = 8 };
        string[] names = ["Dziś", "Jutro", "Pojutrze"];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            days.Add(Button(names[i], async () => { day = index; hour = ""; await LoadTripsAsync(); }));
        }
        Root.Add(days);
        Root.Add(new Label { Text = Date, FontSize = 18 });
        var hours = trips.Select(TripHour).Where(x => x.Length > 0).Distinct().Order().ToList();
        var picker = new Picker { Title = "Godzina", ItemsSource = new[] { "Wszystkie godziny" }.Concat(hours).ToList() };
        picker.SelectedIndex = hour.Length == 0 ? 0 : Math.Max(0, hours.IndexOf(hour) + 1);
        var tripList = new VerticalStackLayout { Spacing = 8 };
        void RenderTrips()
        {
            tripList.Clear();
            var visible = trips.Where(t => hour.Length == 0 || TripHour(t) == hour).ToList();
            if (visible.Count == 0) tripList.Add(new Label { Text = "Brak kursów tego dnia." });
            foreach (var trip in visible)
                tripList.Add(Button($"{TripHour(trip)}  {trip.Code}\n{trip.RouteName}", async () =>
                {
                    selected = trip; ShowTrip(); await RefreshPassengersAsync();
                }));
        }
        picker.SelectedIndexChanged += (_, _) => { hour = picker.SelectedIndex <= 0 ? "" : hours[picker.SelectedIndex - 1]; RenderTrips(); };
        Root.Add(picker); Root.Add(tripList); RenderTrips();
        Root.Add(Button("Odśwież kursy", LoadTripsAsync));
        Root.Add(Button("Wyloguj", () => RunAsync(async () =>
        {
            await DriverApi.CallAsync<OkReply>("logout", new { });
            SecureStorage.Default.Remove("token");
            ShowLogin();
        })));
    }

    private bool IsActive => selected != null && Preferences.Default.Get("tracking_trip", "") == selected.Id;
    private void ShowTrip()
    {
        if (selected == null) return;
        Header(selected.Code);
        if (!string.IsNullOrEmpty(selected.RouteName)) Root.Add(new Label { Text = selected.RouteName, FontSize = 18 });
        trackerStatus = new Label();
        UpdateTrackerStatus();
        Root.Add(trackerStatus);
        if (IsActive)
        {
            Root.Add(Button("Wznów GPS", () => RunAsync(async () =>
            {
                await LocationTracker.PrepareAsync();
                await LocationTracker.StartAsync();
                UpdateTrackerStatus();
            })));
            Root.Add(Button("Zakończ kurs i GPS", () => RunAsync(async () =>
            {
                if (!await DisplayAlertAsync("Zakończyć kurs?", "Wysyłanie lokalizacji zostanie zatrzymane.", "Zakończ", "Anuluj")) return;
                await DriverApi.CallAsync<OkReply>("finish", new { company_id = company, trip_id = selected.Id, tracking_key = await SecureStorage.Default.GetAsync("tracking_key") });
                LocationTracker.Stop();
                Preferences.Default.Remove("tracking_trip"); Preferences.Default.Remove("tracking_company");
                SecureStorage.Default.Remove("tracking_key");
                await LoadTripsCoreAsync();
            })));
        }
        else if (selected.Status == "scheduled")
            Root.Add(Button("Rozpocznij kurs i GPS", () => RunAsync(StartTripAsync)));
        Root.Add(Button("Wróć do kursów", LoadTripsAsync));
        passengerList = new VerticalStackLayout { Spacing = 10 };
        Root.Add(passengerList);
    }

    private async Task StartTripAsync()
    {
        if (selected == null) return;
        if (!await DisplayAlertAsync("Lokalizacja kursu",
            "Po rozpoczęciu kursu Twoja lokalizacja będzie przekazywana firmie do jego zakończenia. Android i iOS mogą działać w tle; Windows wymaga uruchomionej aplikacji.",
            "Rozpocznij", "Anuluj")) return;
        await LocationTracker.PrepareAsync();
        await SecureStorage.Default.SetAsync("storage_check", "ok");
        SecureStorage.Default.Remove("storage_check");
        var reply = await DriverApi.CallAsync<StartReply>("start", new { company_id = company, trip_id = selected.Id });
        await SecureStorage.Default.SetAsync("tracking_key", reply.TrackingKey);
        Preferences.Default.Set("tracking_trip", selected.Id);
        Preferences.Default.Set("tracking_company", company);
        ShowTrip();
        await LocationTracker.StartAsync();
        UpdateTrackerStatus();
        await RefreshPassengersCoreAsync();
    }

    private void UpdateTrackerStatus()
    {
        if (trackerStatus != null)
            trackerStatus.Text = IsActive ? LocationTracker.Status : "";
    }

    private Task RefreshPassengersAsync() => RunAsync(RefreshPassengersCoreAsync);
    private async Task RefreshPassengersCoreAsync()
    {
        if (selected == null) return;
        var tripId = selected.Id;
        var result = await DriverApi.CallAsync<PassengersReply>("passengers", query: new() { ["company_id"] = company, ["trip_id"] = tripId });
        if (selected?.Id != tripId || passengerList == null) return;
        UpdateTrackerStatus();
        passengerList.Clear();
        passengerList.Add(new Label { Text = $"Pasażerowie: {result.Passengers.Count}", FontSize = 20, FontAttributes = FontAttributes.Bold });
        foreach (var p in result.Passengers)
            passengerList.Add(new Label { Text = $"{p.FirstName} {p.LastName}\n{p.FromLabel} → {p.ToLabel}\n{p.PaymentLabel}", Padding = new Thickness(0, 8) });
    }
}
