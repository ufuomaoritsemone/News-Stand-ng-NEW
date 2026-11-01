namespace NigerianNewGrid.Constants;

/// <summary>
/// Centralized API endpoint and network defaults.
/// Avoids scattering hardcoded localhost or platform-specific URLs across ViewModels and pages (Fix #13).
/// </summary>
public static class AppNetworkConstants
{
    public const string ProductionApiUrl = "https://api.newsstand.ng";

    public static string DefaultLocalApiUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:56193"
        : "http://localhost:56193";

    /// <summary>
    /// Returns the active API base URL. Prefers stored user preference unless it points to
    /// an obsolete legacy domain or unreachable loopback on physical hardware, defaulting
    /// directly to the live production endpoint (https://api.newsstand.ng).
    /// </summary>
    public static string GetConfiguredApiBaseUrl()
    {
        var saved = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            if (saved.Contains("nigeriannewsgrid.com", StringComparison.OrdinalIgnoreCase))
            {
                Preferences.Set(AppPreferenceKeys.ApiBaseUrl, ProductionApiUrl);
                return ProductionApiUrl;
            }

            if (DeviceInfo.DeviceType == DeviceType.Physical &&
                (saved.Contains("10.0.2.2", StringComparison.OrdinalIgnoreCase) ||
                 saved.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                 saved.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                 saved.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase)))
            {
                Preferences.Set(AppPreferenceKeys.ApiBaseUrl, ProductionApiUrl);
                return ProductionApiUrl;
            }

            return saved;
        }

        return ProductionApiUrl;
    }
}
