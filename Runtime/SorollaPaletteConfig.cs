using UnityEngine;

namespace SorollaPalette
{
    [CreateAssetMenu(fileName = "SorollaPaletteConfig", menuName = "Sorolla Palette/Config", order = 1)]
    public class SorollaPaletteConfig : ScriptableObject
    {
        [Header("Mode Selection")]
        [Tooltip("Prototype Mode: GA + Facebook + optional MAX\nFull Mode: GA + MAX + Adjust")]
        public PaletteMode mode = PaletteMode.Prototype;

        [Header("GameAnalytics (Required - Both Modes)")] [Tooltip("Game Key from GameAnalytics dashboard")]
        public string gaGameKey;

        [Tooltip("Secret Key from GameAnalytics dashboard")]
        public string gaSecretKey;

        [Header("AppLovin MAX (Optional in Prototype, Required in Full)")] [Tooltip("SDK Key from AppLovin dashboard")]
        public string maxSdkKey;

        [Tooltip("Rewarded ad unit ID")] public string maxRewardedAdUnitId;

        [Tooltip("Interstitial ad unit ID")] public string maxInterstitialAdUnitId;

        [Tooltip("Banner ad unit ID")] public string maxBannerAdUnitId;

        [Header("Facebook SDK (Prototype Mode Only)")] [Tooltip("Facebook App ID - Only used in Prototype Mode")]
        public string facebookAppId;

        [Tooltip("Facebook App Name - Only used in Prototype Mode")]
        public string facebookAppName;

        [Header("Adjust (Full Mode Only)")] [Tooltip("Adjust App Token - Only used in Full Mode")]
        public string adjustAppToken;

        [Header("Module Status (Read-Only)")] [Tooltip("Is MAX module enabled?")]
        public bool maxModuleEnabled;

        [Tooltip("Is Facebook module enabled?")]
        public bool facebookModuleEnabled;

        [Tooltip("Is Adjust module enabled?")] public bool adjustModuleEnabled;

        /// <summary>
        ///     Validates the configuration based on the selected mode
        /// </summary>
        public bool IsValid()
        {
            // GameAnalytics is always required
            if (string.IsNullOrEmpty(gaGameKey) || string.IsNullOrEmpty(gaSecretKey))
            {
                Debug.LogError("[Sorolla Palette] GameAnalytics keys are required for both modes");
                return false;
            }

            // Mode-specific validation
            if (mode == PaletteMode.Prototype)
            {
                // Prototype mode requires Facebook if enabled
                if (facebookModuleEnabled && string.IsNullOrEmpty(facebookAppId))
                {
                    Debug.LogError("[Sorolla Palette] Facebook App ID is required in Prototype Mode");
                    return false;
                }
            }
            else if (mode == PaletteMode.Full)
            {
                // Full mode requires MAX
                if (!maxModuleEnabled)
                {
                    Debug.LogError("[Sorolla Palette] MAX module must be enabled in Full Mode");
                    return false;
                }

                if (string.IsNullOrEmpty(maxSdkKey))
                {
                    Debug.LogError("[Sorolla Palette] MAX SDK Key is required in Full Mode");
                    return false;
                }

                // Adjust is required in Full Mode
                if (adjustModuleEnabled && string.IsNullOrEmpty(adjustAppToken))
                {
                    Debug.LogError("[Sorolla Palette] Adjust App Token is required in Full Mode");
                    return false;
                }
            }

            return true;
        }
    }

    public enum PaletteMode
    {
        Prototype,
        Full
    }
}