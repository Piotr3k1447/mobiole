namespace Barbara_Kierowca;

public static partial class LocationTracker
{
    private static CancellationTokenSource? cancellation;

    private static Task PreparePlatformAsync() => Task.CompletedTask;

    private static Task StartPlatformAsync()
    {
        cancellation?.Cancel();
        cancellation = new CancellationTokenSource();
        _ = TrackAsync(cancellation.Token);
        return Task.CompletedTask;
    }

    private static async Task TrackAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var location = await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10)), token);
                if (location != null && location.Accuracy is { } accuracy && !token.IsCancellationRequested)
                    await SendAsync(location.Latitude, location.Longitude, accuracy, location.Timestamp, location.Speed, location.Course);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e) { Status = $"GPS: {e.Message}"; }
            try { await Task.Delay(TimeSpan.FromSeconds(5), token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void StopPlatform()
    {
        cancellation?.Cancel();
        cancellation = null;
    }
}
