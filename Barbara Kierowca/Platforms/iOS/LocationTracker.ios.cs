using CoreLocation;
using Foundation;

namespace Barbara_Kierowca;

public static partial class LocationTracker
{
    private static readonly CLLocationManager Manager = new()
    {
        DesiredAccuracy = CLLocation.AccuracyBest,
        DistanceFilter = 0,
        PausesLocationUpdatesAutomatically = false,
        ActivityType = CLActivityType.AutomotiveNavigation
    };
    private static readonly LocationDelegate Delegate = new();

    private static Task PreparePlatformAsync()
    {
        if (!CLLocationManager.LocationServicesEnabled)
            throw new InvalidOperationException("Włącz usługi lokalizacji w ustawieniach iPhone'a.");
        if (Manager.AccuracyAuthorization != CLAccuracyAuthorization.FullAccuracy)
            throw new InvalidOperationException("Włącz dokładną lokalizację dla aplikacji.");
        if (Manager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
            Manager.RequestAlwaysAuthorization();
        return Task.CompletedTask;
    }

    private static Task StartPlatformAsync()
    {
        Manager.Delegate = Delegate;
        Manager.AllowsBackgroundLocationUpdates = true;
        Manager.ShowsBackgroundLocationIndicator = true;
        Manager.StartUpdatingLocation();
        return Task.CompletedTask;
    }

    private static void StopPlatform() => Manager.StopUpdatingLocation();

    private sealed class LocationDelegate : CLLocationManagerDelegate
    {
        public override void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
        {
            var location = locations.LastOrDefault();
            if (location == null) return;
            var recorded = DateTimeOffset.FromUnixTimeMilliseconds((long)(location.Timestamp.SecondsSince1970 * 1000));
            _ = SendAsync(location.Coordinate.Latitude, location.Coordinate.Longitude, location.HorizontalAccuracy, recorded,
                location.Speed, location.Course);
        }

        public override void Failed(CLLocationManager manager, NSError error) => Status = $"GPS: {error.LocalizedDescription}";
    }
}
