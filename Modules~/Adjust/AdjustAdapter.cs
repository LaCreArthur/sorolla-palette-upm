#if SOROLLA_ADJUST_ENABLED
using UnityEngine;
using com.adjust.sdk;

namespace SorollaPalette.Adjust
{
    /// <summary>
    /// Adjust SDK adapter - only compiles when SOROLLA_ADJUST_ENABLED is defined
    /// Used in Full Mode for attribution tracking
    /// </summary>
    public static class AdjustAdapter
    {
        private static bool _isInitialized;
        
        public static void Initialize(string appToken, AdjustEnvironment environment)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Already initialized");
                return;
            }
            
            Debug.Log($"[Adjust Adapter] Initializing Adjust SDK ({environment})...");
            
            AdjustConfig config = new AdjustConfig(
                appToken,
                environment == AdjustEnvironment.Production ? 
                    com.adjust.sdk.AdjustEnvironment.Production : 
                    com.adjust.sdk.AdjustEnvironment.Sandbox
            );
            
            // Set log level
            config.setLogLevel(AdjustLogLevel.Info);
            
            // Enable event buffering to optimize network usage
            config.setEventBufferingEnabled(true);
            
            // Attribution callback
            config.setAttributionChangedDelegate(OnAttributionChanged);
            
            // Session callback
            config.setSessionSuccessDelegate(OnSessionSuccess);
            config.setSessionFailureDelegate(OnSessionFailure);
            
            // Event callback
            config.setEventSuccessDelegate(OnEventSuccess);
            config.setEventFailureDelegate(OnEventFailure);
            
            // Initialize Adjust
            com.adjust.sdk.Adjust.start(config);
            
            _isInitialized = true;
            Debug.Log("[Adjust Adapter] Adjust SDK initialized successfully");
        }
        
        private static void OnAttributionChanged(AdjustAttribution attribution)
        {
            Debug.Log($"[Adjust Adapter] Attribution changed: {attribution.network}");
            
            // You can access attribution data:
            // attribution.trackerToken
            // attribution.trackerName
            // attribution.network
            // attribution.campaign
            // attribution.adgroup
            // attribution.creative
        }
        
        private static void OnSessionSuccess(AdjustSessionSuccess sessionSuccess)
        {
            Debug.Log($"[Adjust Adapter] Session tracked: {sessionSuccess.Message}");
        }
        
        private static void OnSessionFailure(AdjustSessionFailure sessionFailure)
        {
            Debug.LogWarning($"[Adjust Adapter] Session failed: {sessionFailure.Message}");
        }
        
        private static void OnEventSuccess(AdjustEventSuccess eventSuccess)
        {
            Debug.Log($"[Adjust Adapter] Event tracked: {eventSuccess.Message}");
        }
        
        private static void OnEventFailure(AdjustEventFailure eventFailure)
        {
            Debug.LogWarning($"[Adjust Adapter] Event failed: {eventFailure.Message}");
        }
        
        /// <summary>
        /// Track custom event
        /// </summary>
        public static void TrackEvent(string eventToken)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }
            
            AdjustEvent adjustEvent = new AdjustEvent(eventToken);
            com.adjust.sdk.Adjust.trackEvent(adjustEvent);
            
            Debug.Log($"[Adjust Adapter] Event tracked: {eventToken}");
        }
        
        /// <summary>
        /// Track revenue event
        /// </summary>
        public static void TrackRevenue(string eventToken, double amount, string currency = "USD")
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }
            
            AdjustEvent adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.setRevenue(amount, currency);
            
            com.adjust.sdk.Adjust.trackEvent(adjustEvent);
            
            Debug.Log($"[Adjust Adapter] Revenue tracked: {amount} {currency}");
        }
        
        /// <summary>
        /// Track ad revenue from AppLovin MAX
        /// Called automatically when MAX reports revenue
        /// </summary>
        public static void TrackAdRevenue(MaxSdkBase.AdInfo adInfo)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }
            
            AdjustAdRevenue adRevenue = new AdjustAdRevenue(AdjustConfig.AdjustAdRevenueSourceAppLovinMAX);
            
            adRevenue.setRevenue(adInfo.Revenue, "USD");
            adRevenue.setAdRevenueNetwork(adInfo.NetworkName);
            adRevenue.setAdRevenueUnit(adInfo.AdUnitIdentifier);
            adRevenue.setAdRevenuePlacement(adInfo.Placement);
            
            com.adjust.sdk.Adjust.trackAdRevenue(adRevenue);
            
            Debug.Log($"[Adjust Adapter] Ad Revenue tracked: {adInfo.Revenue} USD from {adInfo.NetworkName}");
        }
        
        /// <summary>
        /// Set user ID for attribution
        /// </summary>
        public static void SetUserId(string userId)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }
            
            com.adjust.sdk.Adjust.addSessionPartnerParameter("user_id", userId);
            Debug.Log($"[Adjust Adapter] User ID set: {userId}");
        }
        
        /// <summary>
        /// Get Adjust attribution
        /// </summary>
        public static AdjustAttribution GetAttribution()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return null;
            }
            
            return com.adjust.sdk.Adjust.getAttribution();
        }
        
        /// <summary>
        /// Get Adjust device ID (ADID)
        /// </summary>
        public static string GetAdid()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return null;
            }
            
            return com.adjust.sdk.Adjust.getAdid();
        }
    }
}
#endif

