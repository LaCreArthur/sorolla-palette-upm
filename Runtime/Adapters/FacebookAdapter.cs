using System;
using Sorolla.Palette.Health;

#if SOROLLA_FACEBOOK_ENABLED
using System.Collections.Generic;
using Facebook.Unity;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Facebook SDK adapter. Use Sorolla API instead.
    /// </summary>
    internal static class FacebookAdapter
    {
        const string Tag = "[Palette:FB]";
        private static bool s_init;
        private static bool s_consent;
        private static bool s_validationRequested;
        static bool s_advertiserTrackingEnabled;
        static bool s_advertiserTrackingApplied;

        public static event Action<bool> OnGameVisibilityChanged;
        internal static bool LastAdvertiserTrackingEnabled => s_advertiserTrackingEnabled;
        internal static bool AdvertiserTrackingApplied => s_advertiserTrackingApplied;

        public static void Initialize(bool consent)
        {
            s_consent = consent;
            if (s_init) return;

            PaletteLog.Vital($"{Tag} Initializing...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Initializing,
                "init_requested", "Initializing");

            if (!FB.IsInitialized)
                FB.Init(OnInit, OnHideUnity);
            else
            {
                ApplyConsent();
                s_init = true;
                RequestValidation("already_initialized");
            }
        }

        private static void OnInit()
        {
            if (FB.IsInitialized)
            {
                ApplyConsent();
                s_init = true;
                PaletteLog.Vital($"{Tag} Initialized (tracking: {s_consent})");
                RequestValidation("initialized");
            }
            else
            {
                PaletteLog.Error($"{Tag} Failed to initialize");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "init_failed", "Initialization failed");
            }
        }

        public static void UpdateConsent(bool consent)
        {
            s_consent = consent;
            if (!s_init) return; // will be applied in ApplyConsent() when init completes
            SetAdvertiserTrackingEnabled(consent);
            PaletteLog.Vital($"{Tag} SetAdvertiserTrackingEnabled({consent})");
        }

        private static void ApplyConsent()
        {
            SetAdvertiserTrackingEnabled(s_consent);
            FB.ActivateApp();
        }

        static void SetAdvertiserTrackingEnabled(bool consent)
        {
            s_advertiserTrackingEnabled = consent;
            s_advertiserTrackingApplied = true;
            FB.Mobile.SetAdvertiserTrackingEnabled(consent);
        }

        private static void RequestValidation(string source)
        {
            if (s_validationRequested) return;
            s_validationRequested = true;

            string appId = NormalizeAppId(FB.AppId);
            string clientToken = FB.ClientToken;
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(clientToken))
            {
                PaletteLog.Error($"{Tag} AuthError: App ID or Client Token missing");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_missing", "App ID or Client Token missing");
                return;
            }

            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Initializing,
                "validation_requested", $"Initialized ({source}); validating app credentials");

            try
            {
                var formData = new Dictionary<string, string>
                {
                    { "access_token", appId + "|" + clientToken },
                };
                FB.API("/" + appId + "?fields=id,supported_platforms", HttpMethod.GET, OnValidationProbe, formData);
            }
            catch (Exception e)
            {
                PaletteLog.Error($"{Tag} AuthError: validation request failed ({e.Message})");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_probe_failed", "Validation request failed: " + e.Message);
            }
        }

        private static void OnValidationProbe(IGraphResult result)
        {
            if (result == null)
            {
                PaletteLog.Error($"{Tag} AuthError: validation returned no result");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_no_result", "Validation returned no result");
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                ReportProbeFailure(FacebookProbeFailure.Detail(
                    NormalizeAppId(FB.AppId), result.Error, result.RawResult));
                return;
            }

            string appId = NormalizeAppId(FB.AppId);
            FacebookRegistrationResult registration =
                FacebookPlatformRegistration.Classify(result.RawResult, appId, CurrentGraphPlatform);

            // Unverified absorbs what used to be two separate warnings (no app id proven, and no readable
            // platform list): neither proves anything about registration, and they carried the same remedy.
            if (registration.Verdict == FacebookRegistrationVerdict.Unverified)
            {
                string detail = string.IsNullOrEmpty(result.RawResult)
                    ? "Validation response did not prove this app's platform registration"
                    : "Validation response did not prove this app's platform registration: " + SafeDetail(result.RawResult);
                PaletteLog.Warning($"{Tag} Validation warning: {detail}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Warning,
                    "auth_unverified", detail);
                return;
            }

            if (registration.Verdict == FacebookRegistrationVerdict.NotRegistered)
            {
                ReportProbeFailure($"{CurrentPlatformDisplayName} not registered on FB app {appId}", "platform_missing");
                return;
            }

            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Ready,
                "validated", "Initialized, app credentials validated, and current platform registered");
        }

        private static void ReportProbeFailure(string detail, string code = "auth_error")
        {
            if (IsTlsCertificateFailure(detail))
                detail = $"{detail} | device-clock suspect: device date {DateTime.Now:yyyy-MM-dd}; if this date is wrong, fix Settings -> General -> Date & Time -> Set Automatically, then restart - a wrong device clock makes vendors with near-expiry TLS certificates fail while others still work.";

            PaletteLog.Error($"{Tag} AuthError: {detail}");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                code, detail);
        }

        static bool IsTlsCertificateFailure(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return false;
            return detail.IndexOf("ssl", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("tls", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("cert ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Which platform this build is graded as. The Graph vocabulary itself (IPHONE / IPAD / ANDROID)
        // lives solely in FacebookPlatformRegistration; only the human-facing name stays here.
#if UNITY_IOS
        const FacebookPlatform CurrentGraphPlatform = FacebookPlatform.iOS;

        private static string CurrentPlatformDisplayName => "iOS";
#else
        const FacebookPlatform CurrentGraphPlatform = FacebookPlatform.Android;

        private static string CurrentPlatformDisplayName => "ANDROID";
#endif

        private static string NormalizeAppId(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return appId;
            return appId.StartsWith("fb", StringComparison.OrdinalIgnoreCase)
                ? appId.Substring(2)
                : appId;
        }

        private static string SafeDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "Unknown";
            detail = detail.Replace('\n', ' ').Replace('\r', ' ');
            return detail.Length > 180 ? detail.Substring(0, 179) + "..." : detail;
        }

        private static void OnHideUnity(bool isGameShown)
        {
            OnGameVisibilityChanged?.Invoke(isGameShown);
            if (isGameShown && FB.IsInitialized)
                FB.ActivateApp();
        }

    }
}
#else
namespace Sorolla.Palette.Adapters
{
    internal static class FacebookAdapter
    {
        #pragma warning disable CS0067 // Event is never used (stub for API compatibility)
        public static event System.Action<bool> OnGameVisibilityChanged;
        #pragma warning restore CS0067
        internal static bool LastAdvertiserTrackingEnabled => false;
        internal static bool AdvertiserTrackingApplied => false;
        public static void Initialize(bool consent)
        {
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Unavailable,
                "not_installed", "Facebook implementation not installed");
            PaletteLog.Warning("[Palette:FB] Not installed");
        }
        public static void UpdateConsent(bool consent) { }

    }
}
#endif

namespace Sorolla.Palette.Adapters
{
    internal static class FacebookProbeFailure
    {
        internal static string Detail(string appId, string transportError, string rawResult)
        {
            string graphMessage = GraphMessage(rawResult);
            if (graphMessage.IndexOf("Application has been deleted", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"Facebook app {appId} has been deleted. Create a replacement Meta app, register this build's platform, then update the App ID and Client Token in Facebook Settings.";

            if (graphMessage.IndexOf("Invalid OAuth access token signature", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"Facebook app {appId} exists, but its Client Token does not match. Re-copy the Client Token from Meta App Dashboard -> Settings -> Advanced.";

            if (graphMessage.IndexOf("Invalid application ID", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"Facebook App ID {appId} is invalid. Re-copy the App ID from Meta App Dashboard -> Settings -> Basic.";

            if (!string.IsNullOrEmpty(graphMessage))
                return SafeDetail(graphMessage);

            return SafeDetail(transportError);
        }

        static string GraphMessage(string rawResult) =>
            FacebookPlatformRegistration.TryGetGraphErrorMessage(rawResult, out string message) ? message : "";

        static string SafeDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "Unknown";
            detail = detail.Replace('\n', ' ').Replace('\r', ' ');
            return detail.Length > 300 ? detail.Substring(0, 299) + "..." : detail;
        }
    }
}
