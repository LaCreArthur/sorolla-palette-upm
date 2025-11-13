using UnityEngine;

#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK; 
#endif

namespace SorollaPalette
{
    /// <summary>
    /// GameAnalytics adapter - always available since GA is auto-installed
    /// </summary>
    public static class GameAnalyticsAdapter
    {
        private static bool _isInitialized;
        private static bool _remoteConfigReady;
        
        public static void Initialize(string gameKey, string secretKey)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GA Adapter] Already initialized");
                return;
            }
            
            Debug.Log("[GA Adapter] Initializing GameAnalytics...");
            
            // GameAnalytics auto-initializes via their prefab/settings
            // We just need to ensure it's ready
            
#if !UNITY_EDITOR
            // In builds, GA initializes automatically
            // Check if remote config is available
            CheckRemoteConfigStatus();
#else
            Debug.Log($"[GA Adapter] Editor mode - GameAnalytics configured with Game Key: {gameKey}");
            _remoteConfigReady = true; // Simulate ready in editor
#endif
            
            _isInitialized = true;
            Debug.Log("[GA Adapter] GameAnalytics initialized successfully");
        }
        
#if GAMEANALYTICS_INSTALLED
        private static void CheckRemoteConfigStatus()
        {
            // GameAnalytics remote config becomes available after SDK initialization
            // We'll check this periodically or via callback if available
            _remoteConfigReady = GameAnalytics.IsRemoteConfigsReady();
        }
#endif
        
        public static bool IsRemoteConfigReady()
        {
#if UNITY_EDITOR
            return true; // Always ready in editor for testing
#else
            return _remoteConfigReady;
#endif
        }
        
#if GAMEANALYTICS_INSTALLED
        public static void TrackProgressionEvent(GAProgressionStatus status, string progression01, string progression02 = null, string progression03 = null, int score = 0)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[GA Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            if (string.IsNullOrEmpty(progression02) && string.IsNullOrEmpty(progression03))
            {
                if (score > 0)
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01, score);
                else
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01);
            }
            else if (string.IsNullOrEmpty(progression03))
            {
                if (score > 0)
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01, progression02, score);
                else
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01, progression02);
            }
            else
            {
                if (score > 0)
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03, score);
                else
                    GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(status, progression01, progression02, progression03);
            }
#else
            Debug.Log($"[GA Adapter] Progression Event: {status} - {progression01}/{progression02}/{progression03} (Score: {score})");
#endif
        }
#else
        // Fallback when GameAnalytics is not installed
        public static void TrackProgressionEvent(string status, string progression01, string progression02 = null, string progression03 = null, int score = 0)
        {
            Debug.LogWarning("[GA Adapter] GameAnalytics not installed. Install package to enable analytics.");
        }
#endif
        
#if GAMEANALYTICS_INSTALLED
        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[GA Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            if (value != 0)
                GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventName, value);
            else
                GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventName);
#else
            Debug.Log($"[GA Adapter] Design Event: {eventName} = {value}");
#endif
        }
#else
        // Fallback when GameAnalytics is not installed
        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            Debug.LogWarning("[GA Adapter] GameAnalytics not installed. Install package to enable analytics.");
        }
#endif
        
#if GAMEANALYTICS_INSTALLED
        public static void TrackResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[GA Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            GameAnalyticsSDK.GameAnalytics.NewResourceEvent(flowType, currency, amount, itemType, itemId);
#else
            Debug.Log($"[GA Adapter] Resource Event: {flowType} - {currency} {amount} ({itemType}:{itemId})");
#endif
        }
#else
        // Fallback when GameAnalytics is not installed
        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId)
        {
            Debug.LogWarning("[GA Adapter] GameAnalytics not installed. Install package to enable analytics.");
        }
#endif
        
#if GAMEANALYTICS_INSTALLED
        public static string GetRemoteConfigValue(string key, string defaultValue = "")
        {
            if (!_isInitialized || !IsRemoteConfigReady())
            {
                return defaultValue;
            }
            
#if !UNITY_EDITOR
            return GameAnalyticsSDK.GameAnalytics.GetRemoteConfigsValueAsString(key, defaultValue);
#else
            Debug.Log($"[GA Adapter] Remote Config: {key} (default: {defaultValue})");
            return defaultValue;
#endif
        }
#else
        // Fallback when GameAnalytics is not installed
        public static string GetRemoteConfigValue(string key, string defaultValue = "")
        {
            Debug.LogWarning("[GA Adapter] GameAnalytics not installed. Install package to enable analytics.");
            return defaultValue;
        }
#endif
    }
}

