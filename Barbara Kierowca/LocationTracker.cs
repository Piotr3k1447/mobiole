namespace Barbara_Kierowca;

public static partial class LocationTracker
{
    private static int sending;
    private static DateTimeOffset lastSent = DateTimeOffset.MinValue;
    private static string status = "Kurs aktywny. Naciśnij „Wznów GPS”, aby rozpocząć wysyłanie lokalizacji.";
    public static string Status
    {
        get => status;
        private set { status = value; PlatformStatusChanged(value); }
    }
    static partial void PlatformStatusChanged(string message);

    public static async Task PrepareAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Wymagana jest zgoda na dostęp do lokalizacji.");
        await PreparePlatformAsync();
    }

    private static async Task SendAsync(double latitude, double longitude, double accuracy, DateTimeOffset recordedAt,
        double? speed, double? heading)
    {
        if (accuracy < 0 || Math.Abs((DateTimeOffset.UtcNow - recordedAt).TotalSeconds) >= 60 ||
            DateTimeOffset.UtcNow - lastSent < TimeSpan.FromSeconds(5) ||
            Interlocked.CompareExchange(ref sending, 1, 0) != 0) return;
        if (speed is not { } s || !double.IsFinite(s) || s < 0 || s > 200) speed = null;
        if (heading is not { } h || !double.IsFinite(h) || h < 0 || h >= 360) heading = null;
        lastSent = DateTimeOffset.UtcNow;
        try
        {
            var key = await SecureStorage.Default.GetAsync("tracking_key");
            var company = Preferences.Default.Get("tracking_company", "");
            var trip = Preferences.Default.Get("tracking_trip", "");
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(company) || string.IsNullOrEmpty(trip))
                throw new InvalidOperationException("Brak danych aktywnego kursu.");
            await DriverApi.CallAsync<OkReply>("location", new
            {
                company_id = company, trip_id = trip, tracking_key = key,
                latitude, longitude, accuracy, speed, heading, recorded_at = recordedAt.ToUnixTimeSeconds()
            });
            Status = $"GPS wysłany: {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception e)
        {
            Status = $"GPS: {e.Message}";
            if (e is DriverApiException { Status: 401 or 403 or 409 }) StopPlatform();
        }
        finally { Interlocked.Exchange(ref sending, 0); }
    }

    public static async Task StartAsync()
    {
        if (string.IsNullOrEmpty(Preferences.Default.Get("tracking_trip", "")) ||
            string.IsNullOrEmpty(await SecureStorage.Default.GetAsync("tracking_key")))
            throw new InvalidOperationException("Brak danych aktywnego kursu.");
        await StartPlatformAsync();
        Status = "GPS uruchomiony. Oczekiwanie na lokalizację.";
    }

    public static void Stop()
    {
        StopPlatform();
        Status = "GPS zatrzymany.";
    }
}
