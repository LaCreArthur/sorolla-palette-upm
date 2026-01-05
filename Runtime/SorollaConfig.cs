using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Configuration asset for Palette SDK.
    ///     Create via: Assets > Create > Palette > Config
    ///     Save to: Assets/Resources/SorollaConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "SorollaConfig", menuName = "Palette/Config", order = 1)]
    public class SorollaConfig : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("Prototype = GA + Facebook | Full = GA + MAX + Adjust")]
        public bool isPrototypeMode = true;

        [Header("AppLovin MAX")]
        [Tooltip("SDK Key from AppLovin dashboard")]
        public string maxSdkKey;

        [Tooltip("Rewarded ad unit ID")]
        public string maxRewardedAdUnitId;

        [Tooltip("Interstitial ad unit ID")]
        public string maxInterstitialAdUnitId;

        [Tooltip("Banner ad unit ID (optional)")]
        public string maxBannerAdUnitId;

        [Header("Adjust (Full Mode Only)")]
        [Tooltip("Adjust App Token")]
        public string adjustAppToken;

        [Tooltip("Use Sandbox environment for testing (disable for production builds)")]
        public bool adjustSandboxMode = true;


        [Header("Firebase Analytics (Optional)")]
        [Tooltip("Enable Firebase Analytics (requires google-services.json / GoogleService-Info.plist)")]
        public bool enableFirebaseAnalytics;

        [Header("Firebase Crashlytics (Optional)")]
        [Tooltip("Enable Firebase Crashlytics for crash reporting")]
        public bool enableCrashlytics;

        [Header("Firebase Remote Config (Optional)")]
        [Tooltip("Enable Firebase Remote Config for A/B testing and feature flags")]
        public bool enableRemoteConfig;

        /// <summary>
        ///     Validate configuration for current mode and provide helpful error messages
        /// </summary>
        public bool IsValid()
        {
            if (isPrototypeMode)
            {
                // Prototype mode is lenient - only GameAnalytics is required
                // Facebook SDK is strongly recommended but optional
                return true;
            }

            // Full mode requires MAX and Adjust
            bool isValid = true;
            
            if (string.IsNullOrEmpty(maxSdkKey))
            {
                Debug.LogError("[Palette] Full Mode: MAX SDK Key is required. " +
                    "Configure via: Window > Palette > Configuration. " +
                    "Get your SDK Key from: https://dash.applovin.com/o/account#keys");
                isValid = false;
            }

            if (string.IsNullOrEmpty(adjustAppToken))
            {
                Debug.LogError("[Palette] Full Mode: Adjust App Token is required. " +
                    "Configure via: Window > Palette > Configuration. " +
                    "Get your App Token from: https://dash.adjust.com");
                isValid = false;
            }

            return isValid;
        }
        
        /// <summary>
        ///     Log helpful warnings for missing optional configurations
        /// </summary>
        public void ValidateOptionalSettings()
        {
            if (isPrototypeMode)
            {
                // In prototype mode, warn if common settings are missing
                if (string.IsNullOrEmpty(maxSdkKey) && string.IsNullOrEmpty(maxRewardedAdUnitId))
                {
                    Debug.Log("[Palette] Prototype Mode: MAX SDK not configured. " +
                        "Ads are optional but recommended for early monetization testing. " +
                        "Configure via: Window > Palette > Configuration");
                }
            }
            else
            {
                // In full mode, warn about missing ad units
                if (string.IsNullOrEmpty(maxRewardedAdUnitId) && string.IsNullOrEmpty(maxInterstitialAdUnitId))
                {
                    Debug.LogWarning("[Palette] Full Mode: No ad units configured. " +
                        "Add Rewarded or Interstitial ad unit IDs via: Window > Palette > Configuration");
                }
            }
        }
    }
}
