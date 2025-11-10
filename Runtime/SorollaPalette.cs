using System;
using UnityEngine;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace SorollaPalette
{
    /// <summary>
    ///     Main API for Sorolla Palette SDK
    ///     Provides unified interface for analytics, ads, and attribution
    /// </summary>
    public static class SorollaPalette
    {
        private static SorollaPaletteConfig _Config;

        /// <summary>
        ///     Check if Sorolla Palette is initialized
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        ///     Initialize Sorolla Palette with configuration
        ///     Call this once at app startup
        /// </summary>
        public static void Initialize(SorollaPaletteConfig config)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[Sorolla Palette] Already initialized. Skipping.");
                return;
            }

            if (config == null)
            {
                Debug.LogError("[Sorolla Palette] Config is null! Cannot initialize.");
                return;
            }

            if (!config.IsValid())
            {
                Debug.LogError("[Sorolla Palette] Config validation failed! Check your settings.");
                return;
            }

            _Config = config;

            Debug.Log($"[Sorolla Palette] Initializing in {config.mode} Mode...");

            // Initialize GameAnalytics (always required)
            InitializeGameAnalytics();

            // Initialize MAX if enabled
#if SOROLLA_MAX_ENABLED
            if (config.maxModuleEnabled)
            {
                InitializeMAX();
            }
#endif

            // Initialize Facebook in Prototype Mode
#if SOROLLA_FACEBOOK_ENABLED
            if (config.mode == PaletteMode.Prototype && config.facebookModuleEnabled)
            {
                InitializeFacebook();
            }
#endif

            // Initialize Adjust in Full Mode
#if SOROLLA_ADJUST_ENABLED
            if (config.mode == PaletteMode.Full && config.adjustModuleEnabled)
            {
                InitializeAdjust();
            }
#endif

            IsInitialized = true;
            Debug.Log("[Sorolla Palette] Initialization complete!");
        }

        /// <summary>
        ///     Get current configuration
        /// </summary>
        public static SorollaPaletteConfig GetConfig()
        {
            return _Config;
        }

        #region Facebook Integration

        private static void InitializeFacebook()
        {
#if SOROLLA_FACEBOOK_ENABLED
            Debug.Log("[Sorolla Palette] Initializing Facebook SDK...");
            
            if (string.IsNullOrEmpty(_config.facebookAppId))
            {
                Debug.LogError("[Sorolla Palette] Facebook App ID is empty!");
                return;
            }
            
            Facebook.FacebookAdapter.Initialize();
#endif
        }

        #endregion

        #region Adjust Integration

        private static void InitializeAdjust()
        {
#if SOROLLA_ADJUST_ENABLED
            Debug.Log("[Sorolla Palette] Initializing Adjust SDK...");
            
            if (string.IsNullOrEmpty(_config.adjustAppToken))
            {
                Debug.LogError("[Sorolla Palette] Adjust App Token is empty!");
                return;
            }
            
            Adjust.AdjustAdapter.Initialize(_config.adjustAppToken, _config.adjustEnvironment);
#endif
        }

        #endregion

        #region GameAnalytics Integration

        private static void InitializeGameAnalytics()
        {
            Debug.Log("[Sorolla Palette] Initializing GameAnalytics...");

            GameAnalyticsAdapter.Initialize(_Config.gaGameKey, _Config.gaSecretKey);
        }

        /// <summary>
        ///     Track a progression event (level start, complete, fail)
        /// </summary>
        public static void TrackProgressionEvent(string progressionStatus, string progression01,
            string progression02 = null, string progression03 = null, int score = 0)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Sorolla Palette] Not initialized. Call Initialize() first.");
                return;
            }

#if GAMEANALYTICS_INSTALLED
            // Convert string status to GA enum
            GAProgressionStatus status;
            switch (progressionStatus.ToLower())
            {
                case "start":
                    status = GAProgressionStatus.Start;
                    break;
                case "complete":
                    status = GAProgressionStatus.Complete;
                    break;
                case "fail":
                    status = GAProgressionStatus.Fail;
                    break;
                default:
                    Debug.LogWarning($"[Sorolla Palette] Invalid progression status: {progressionStatus}");
                    return;
            }

            GameAnalyticsAdapter.TrackProgressionEvent(status, progression01, progression02, progression03, score);
#else
            // Fallback when GA not installed
            GameAnalyticsAdapter.TrackProgressionEvent(progressionStatus, progression01, progression02, progression03, score);
#endif
        }

        /// <summary>
        ///     Track a design event (custom event)
        /// </summary>
        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Sorolla Palette] Not initialized. Call Initialize() first.");
                return;
            }

            // Always track to GameAnalytics
            GameAnalyticsAdapter.TrackDesignEvent(eventName, value);

            // Forward to Facebook in Prototype Mode
#if SOROLLA_FACEBOOK_ENABLED
            if (_config.mode == PaletteMode.Prototype && _config.facebookModuleEnabled)
            {
                Facebook.FacebookAdapter.TrackEvent(eventName, value);
            }
#endif
        }

        /// <summary>
        ///     Track a resource event (source/sink)
        /// </summary>
        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType,
            string itemId)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Sorolla Palette] Not initialized. Call Initialize() first.");
                return;
            }

#if GAMEANALYTICS_INSTALLED
            // Convert string flowType to GA enum
            GAResourceFlowType flow;
            switch (flowType.ToLower())
            {
                case "source":
                    flow = GAResourceFlowType.Source;
                    break;
                case "sink":
                    flow = GAResourceFlowType.Sink;
                    break;
                default:
                    Debug.LogWarning($"[Sorolla Palette] Invalid resource flow type: {flowType}");
                    return;
            }

            GameAnalyticsAdapter.TrackResourceEvent(flow, currency, amount, itemType, itemId);
#else
            // Fallback when GA not installed
            GameAnalyticsAdapter.TrackResourceEvent(flowType, currency, amount, itemType, itemId);
#endif
        }

        /// <summary>
        ///     Check if remote config is ready
        /// </summary>
        public static bool IsRemoteConfigReady()
        {
            return GameAnalyticsAdapter.IsRemoteConfigReady();
        }

        /// <summary>
        ///     Get remote config value as string
        /// </summary>
        public static string GetRemoteConfigValue(string key, string defaultValue = null)
        {
            if (!IsInitialized) return defaultValue;

            return GameAnalyticsAdapter.GetRemoteConfigValue(key, defaultValue ?? "");
        }

        /// <summary>
        ///     Get remote config value as int
        /// </summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
            var value = GetRemoteConfigValue(key, defaultValue.ToString());
            if (int.TryParse(value, out var result)) return result;
            return defaultValue;
        }

        /// <summary>
        ///     Get remote config value as float
        /// </summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f)
        {
            var value = GetRemoteConfigValue(key, defaultValue.ToString());
            if (float.TryParse(value, out var result)) return result;
            return defaultValue;
        }

        /// <summary>
        ///     Get remote config value as bool
        /// </summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
            var value = GetRemoteConfigValue(key, defaultValue.ToString());
            if (bool.TryParse(value, out var result)) return result;
            return defaultValue;
        }

        #endregion

        #region MAX Integration

        private static void InitializeMAX()
        {
#if SOROLLA_MAX_ENABLED
            Debug.Log("[Sorolla Palette] Initializing AppLovin MAX...");
            
            if (string.IsNullOrEmpty(_config.maxSdkKey))
            {
                Debug.LogError("[Sorolla Palette] MAX SDK Key is empty!");
                return;
            }
            
            MAX.MaxAdapter.Initialize(
                _config.maxSdkKey,
                _config.maxRewardedAdUnitId,
                _config.maxInterstitialAdUnitId,
                _config.maxBannerAdUnitId
            );
#endif
        }

        /// <summary>
        ///     Show rewarded ad
        /// </summary>
        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
#if SOROLLA_MAX_ENABLED
            if (!_config.maxModuleEnabled)
            {
                Debug.LogWarning("[Sorolla Palette] MAX module not enabled!");
                onFailed?.Invoke();
                return;
            }
            
            MAX.MaxAdapter.ShowRewardedAd(onComplete, onFailed);
#else
            Debug.LogWarning("[Sorolla Palette] MAX module not compiled. Enable MAX module in configuration.");
            onFailed?.Invoke();
#endif
        }

        /// <summary>
        ///     Show interstitial ad
        /// </summary>
        public static void ShowInterstitialAd(Action onComplete)
        {
#if SOROLLA_MAX_ENABLED
            if (!_config.maxModuleEnabled)
            {
                Debug.LogWarning("[Sorolla Palette] MAX module not enabled!");
                return;
            }
            
            MAX.MaxAdapter.ShowInterstitialAd(onComplete);
#else
            Debug.LogWarning("[Sorolla Palette] MAX module not compiled. Enable MAX module in configuration.");
#endif
        }

        #endregion
    }
}