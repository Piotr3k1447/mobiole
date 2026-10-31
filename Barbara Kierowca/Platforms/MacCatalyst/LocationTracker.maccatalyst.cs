namespace Barbara_Kierowca;

public static partial class LocationTracker
{
    private static Task PreparePlatformAsync() => throw new PlatformNotSupportedException("Ta wersja nie obsługuje GPS na Macu.");
    private static Task StartPlatformAsync() => throw new PlatformNotSupportedException("Ta wersja nie obsługuje GPS na Macu.");
    private static void StopPlatform() { }
}
