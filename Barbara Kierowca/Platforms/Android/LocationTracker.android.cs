using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Location = Android.Locations.Location;

namespace Barbara_Kierowca;

public static partial class LocationTracker
{
    private static Task PreparePlatformAsync()
    {
        var context = Android.App.Application.Context;
        if (context.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Permission.Granted)
            throw new InvalidOperationException("Wymagana jest dokładna lokalizacja.");
        var manager = (LocationManager?)context.GetSystemService(Context.LocationService);
        if (manager == null || !manager.IsProviderEnabled(LocationManager.GpsProvider))
            throw new InvalidOperationException("Włącz GPS w ustawieniach telefonu.");
        return Task.CompletedTask;
    }

    private static Task StartPlatformAsync()
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(TrackingService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) context.StartForegroundService(intent);
        else context.StartService(intent);
        return Task.CompletedTask;
    }

    private static void StopPlatform() =>
        Android.App.Application.Context.StopService(new Intent(Android.App.Application.Context, typeof(TrackingService)));

    internal static Task SendLocationAsync(Location location) =>
        SendAsync(location.Latitude, location.Longitude, location.Accuracy, DateTimeOffset.FromUnixTimeMilliseconds(location.Time),
            location.HasSpeed ? location.Speed : null, location.HasBearing ? location.Bearing : null);

    internal static void SetError(string message) => Status = message;
    static partial void PlatformStatusChanged(string message) => TrackingService.UpdateNotification(message);
}

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class TrackingService : Service, ILocationListener
{
    private LocationManager? manager;
    private Location? latest;
    private Handler? handler;
    private Java.Lang.Runnable? tick;
    private static bool running;

    private static Notification CreateNotification(Context context, string text)
    {
        var intent = new Intent(context, typeof(MainActivity));
        var pending = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Builder(context, "tracking")
            .SetContentTitle("Barbara Kierowca")
            .SetContentText(text)
            .SetSmallIcon(Resource.Mipmap.barbaraicon)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
    }

    internal static void UpdateNotification(string message)
    {
        if (!running) return;
        var context = Android.App.Application.Context;
        var manager = (NotificationManager?)context.GetSystemService(NotificationService);
        try { manager?.Notify(1, CreateNotification(context, message)); }
        catch (Java.Lang.SecurityException) { }
    }

    public override void OnCreate()
    {
        base.OnCreate();
        var notificationManager = (NotificationManager)GetSystemService(NotificationService)!;
        notificationManager.CreateNotificationChannel(new NotificationChannel("tracking", "Lokalizacja kursu", NotificationImportance.Low));
        var notification = CreateNotification(this, "Oczekiwanie na lokalizację.");
        if (OperatingSystem.IsAndroidVersionAtLeast(29)) StartForeground(1, notification, ForegroundService.TypeLocation);
        else StartForeground(1, notification);
        running = true;
        manager = (LocationManager?)GetSystemService(LocationService);
        try
        {
            manager?.RequestLocationUpdates(LocationManager.GpsProvider, 5000, 0, this);
            handler = new Handler(Looper.MainLooper!);
            tick = new Java.Lang.Runnable(() =>
            {
                if (latest != null) _ = LocationTracker.SendLocationAsync(latest);
                if (tick != null) handler?.PostDelayed(tick, 5000);
            });
            handler.Post(tick);
        }
        catch (Exception e)
        {
            LocationTracker.SetError($"GPS: {e.Message}");
            StopSelf();
        }
    }

    public void OnLocationChanged(Location location) => latest = location;
    public void OnProviderDisabled(string provider) => LocationTracker.SetError("GPS został wyłączony.");
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId) => StartCommandResult.NotSticky;
    public override IBinder? OnBind(Intent? intent) => null;
    public override void OnDestroy()
    {
        running = false;
        if (tick != null) handler?.RemoveCallbacks(tick);
        tick = null;
        manager?.RemoveUpdates(this);
        base.OnDestroy();
    }
}
