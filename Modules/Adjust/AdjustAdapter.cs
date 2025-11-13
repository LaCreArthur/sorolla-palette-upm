#if SOROLLA_ADJUST_ENABLED
using System;
using AdjustSdk;
using UnityEngine;

namespace SorollaPalette.Adjust
{
    /// <summary>
    ///     Adjust SDK adapter - only compiles when SOROLLA_ADJUST_ENABLED is defined
    ///     Used in Full Mode for attribution tracking
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

            var config = new AdjustConfig(appToken, environment == AdjustEnvironment.Production
                ? AdjustSdk.AdjustEnvironment.Production
                : AdjustSdk.AdjustEnvironment.Sandbox);


            // Initialize Adjust
            AdjustSdk.Adjust.InitSdk(config);

            _isInitialized = true;
            Debug.Log("[Adjust Adapter] Adjust SDK initialized successfully");
        }

        private static void OnAttributionChanged(AdjustAttribution attribution)
        {
            Debug.Log($"[Adjust Adapter] Attribution changed: {attribution.Network}");

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
        ///     Track custom event
        /// </summary>
        public static void TrackEvent(string eventToken)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            AdjustSdk.Adjust.TrackEvent(adjustEvent);

            Debug.Log($"[Adjust Adapter] Event tracked: {eventToken}");
        }

        /// <summary>
        ///     Track revenue event
        /// </summary>
        public static void TrackRevenue(string eventToken, double amount, string currency = "USD")
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.SetRevenue(amount, currency);

            AdjustSdk.Adjust.TrackEvent(adjustEvent);

            Debug.Log($"[Adjust Adapter] Revenue tracked: {amount} {currency}");
        }

        /// <summary>
        ///     Track ad revenue from AppLovin MAX
        ///     Called automatically when MAX reports revenue
        /// </summary>
#if APPLOVIN_MAX_INSTALLED
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

            Adjust.trackAdRevenue(adRevenue);

            Debug.Log($"[Adjust Adapter] Ad Revenue tracked: {adInfo.Revenue} USD from {adInfo.NetworkName}");
        }
#else
        public static void TrackAdRevenue(object adInfo)
        {
            // No-op when MAX is not installed
        }
#endif

        /// <summary>
        ///     Set user ID for attribution
        /// </summary>
        public static void SetUserId(string userId)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }

            AdjustSdk.Adjust.AddGlobalPartnerParameter("user_id", userId);
            Debug.Log($"[Adjust Adapter] User ID set: {userId}");
        }

        /// <summary>
        ///     Get Adjust attribution
        /// </summary>
        public static void GetAttribution(Action<AdjustAttribution> onAttributionCallback)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }

            AdjustSdk.Adjust.GetAttribution(onAttributionCallback);
        }

        /// <summary>
        ///     Get Adjust device ID (ADID)
        /// </summary>
        public static void GetAdid(Action<string> onAdidCallback)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Adjust Adapter] Not initialized");
                return;
            }

            AdjustSdk.Adjust.GetAdid(onAdidCallback);
        }
    }

    public enum AdjustEnvironment
    {
        Sandbox,
        Production
    }
}
#endif