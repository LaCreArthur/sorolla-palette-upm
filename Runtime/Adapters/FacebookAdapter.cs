using System;
using UnityEngine;

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

            if (!ContainsAppId(result))
            {
                string detail = string.IsNullOrEmpty(result.RawResult)
                    ? "Validation response did not include app id"
                    : "Validation response did not include app id: " + SafeDetail(result.RawResult);
                PaletteLog.Warning($"{Tag} Validation warning: {detail}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Warning,
                    "auth_unverified", detail);
                return;
            }

            if (!TryCurrentPlatformRegistration(result.RawResult, out bool platformRegistered))
            {
                const string detail = "Validation response did not include a readable supported_platforms list";
                PaletteLog.Warning($"{Tag} Validation warning: {detail}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Warning,
                    "platform_unverified", detail);
                return;
            }

            if (!platformRegistered)
            {
                ReportProbeFailure($"{CurrentPlatformDisplayName} not registered on FB app {NormalizeAppId(FB.AppId)}");
                return;
            }

            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Ready,
                "validated", "Initialized, app credentials validated, and current platform registered");
        }

        private static void ReportProbeFailure(string detail)
        {
            if (IsTlsCertificateFailure(detail))
                detail = $"{detail} | device-clock suspect: device date {DateTime.Now:yyyy-MM-dd}; if this date is wrong, fix Settings -> General -> Date & Time -> Set Automatically, then restart - a wrong device clock makes vendors with near-expiry TLS certificates fail while others still work.";

            PaletteLog.Error($"{Tag} AuthError: {detail}");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                "auth_error", detail);
        }

        static bool IsTlsCertificateFailure(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return false;
            return detail.IndexOf("ssl", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("tls", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("cert ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Graph vocabulary trap: FB Graph API supported_platforms uses IPHONE / IPAD / ANDROID.
        // There is no "IOS" value - a correctly-provisioned iOS app registers as IPHONE and/or
        // IPAD. This shipped once as a false "platform missing" report; compare against the
        // Graph vocabulary here, keep "iOS" only for the human-facing message.
#if UNITY_IOS
        private static readonly string[] s_iosGraphPlatforms = { "IPHONE", "IPAD" };

        private static bool IsCurrentPlatformRegistered(string[] supportedPlatforms) =>
            Array.IndexOf(supportedPlatforms, s_iosGraphPlatforms[0]) >= 0
            || Array.IndexOf(supportedPlatforms, s_iosGraphPlatforms[1]) >= 0;

        private static string CurrentPlatformDisplayName => "iOS";
#else
        private static bool IsCurrentPlatformRegistered(string[] supportedPlatforms) =>
            Array.IndexOf(supportedPlatforms, "ANDROID") >= 0;

        private static string CurrentPlatformDisplayName => "ANDROID";
#endif

        [Serializable]
        private class SupportedPlatformsResponse
        {
            public string[] supported_platforms;
        }

        private static bool TryCurrentPlatformRegistration(string rawResult, out bool registered)
        {
            registered = false;
            if (string.IsNullOrEmpty(rawResult)) return false;
            SupportedPlatformsResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<SupportedPlatformsResponse>(rawResult);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (parsed?.supported_platforms == null) return false;
            registered = IsCurrentPlatformRegistered(parsed.supported_platforms);
            return true;
        }

        private static bool ContainsAppId(IGraphResult result)
        {
            string appId = NormalizeAppId(FB.AppId);
            if (result.ResultDictionary != null
                && result.ResultDictionary.TryGetValue("id", out object id)
                && string.Equals(id?.ToString(), appId, StringComparison.Ordinal))
                return true;

            return !string.IsNullOrEmpty(result.RawResult)
                && result.RawResult.Contains("\"id\":\"" + appId + "\"");
        }

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

        static string GraphMessage(string rawResult)
        {
            if (string.IsNullOrEmpty(rawResult)) return "";
            try
            {
                var parsed = JsonUtility.FromJson<GraphErrorResponse>(rawResult);
                return parsed?.error?.message ?? "";
            }
            catch (ArgumentException)
            {
                return "";
            }
        }

        static string SafeDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "Unknown";
            detail = detail.Replace('\n', ' ').Replace('\r', ' ');
            return detail.Length > 300 ? detail.Substring(0, 299) + "..." : detail;
        }

        [Serializable]
        sealed class GraphErrorResponse
        {
            public GraphError error;
        }

        [Serializable]
        sealed class GraphError
        {
            public string message;
        }
    }
}
