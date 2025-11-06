#if SOROLLA_FACEBOOK_ENABLED
using UnityEngine;
using Facebook.Unity;
using System.Collections.Generic;

namespace SorollaPalette.Facebook
{
    /// <summary>
    /// Facebook SDK adapter - only compiles when SOROLLA_FACEBOOK_ENABLED is defined
    /// Used in Prototype Mode for UA tracking
    /// </summary>
    public static class FacebookAdapter
    {
        private static bool _isInitialized;
        
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Already initialized");
                return;
            }
            
            Debug.Log("[Facebook Adapter] Initializing Facebook SDK...");
            
            if (!FB.IsInitialized)
            {
                FB.Init(OnFacebookInitialized, OnHideUnity);
            }
            else
            {
                FB.ActivateApp();
                _isInitialized = true;
                Debug.Log("[Facebook Adapter] Facebook SDK already initialized");
            }
        }
        
        private static void OnFacebookInitialized()
        {
            Debug.Log("[Facebook Adapter] Facebook SDK initialized");
            
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                _isInitialized = true;
            }
            else
            {
                Debug.LogError("[Facebook Adapter] Failed to initialize Facebook SDK");
            }
        }
        
        private static void OnHideUnity(bool isGameShown)
        {
            if (!isGameShown)
            {
                // Pause the game
                Time.timeScale = 0;
            }
            else
            {
                // Resume the game
                Time.timeScale = 1;
            }
        }
        
        /// <summary>
        /// Track a custom event to Facebook Analytics
        /// </summary>
        public static void TrackEvent(string eventName, float? valueToSum = null, Dictionary<string, object> parameters = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            if (valueToSum.HasValue)
            {
                FB.LogAppEvent(eventName, valueToSum.Value, parameters);
            }
            else
            {
                FB.LogAppEvent(eventName, parameters: parameters);
            }
#else
            Debug.Log($"[Facebook Adapter] Event: {eventName} (Value: {valueToSum})");
#endif
        }
        
        /// <summary>
        /// Track app activation (called automatically on init)
        /// </summary>
        public static void TrackAppActivation()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            FB.Mobile.FetchDeferredAppLinkData(OnDeferredAppLinkReceived);
#endif
        }
        
        private static void OnDeferredAppLinkReceived(IAppLinkResult result)
        {
            if (!string.IsNullOrEmpty(result.Url))
            {
                Debug.Log($"[Facebook Adapter] Deferred app link: {result.Url}");
                // Handle deep link if needed
            }
        }
        
        /// <summary>
        /// Track purchase event
        /// </summary>
        public static void TrackPurchase(float amount, string currency = "USD", Dictionary<string, object> parameters = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
#if !UNITY_EDITOR
            FB.LogPurchase((decimal)amount, currency, parameters);
#else
            Debug.Log($"[Facebook Adapter] Purchase: {amount} {currency}");
#endif
        }
        
        /// <summary>
        /// Track level achieved
        /// </summary>
        public static void TrackLevelAchieved(int level)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
            var parameters = new Dictionary<string, object>
            {
                { AppEventParameterName.Level, level.ToString() }
            };
            
            TrackEvent(AppEventName.AchievedLevel, null, parameters);
        }
        
        /// <summary>
        /// Track tutorial completion
        /// </summary>
        public static void TrackTutorialCompleted()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
            TrackEvent(AppEventName.CompletedTutorial);
        }
        
        /// <summary>
        /// Track ad impression (for UA tracking)
        /// </summary>
        public static void TrackAdImpression(string adNetwork, string adPlacementId, float revenue)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Facebook Adapter] Not initialized");
                return;
            }
            
            var parameters = new Dictionary<string, object>
            {
                { "ad_network", adNetwork },
                { "ad_placement_id", adPlacementId },
                { "revenue", revenue }
            };
            
            TrackEvent("ad_impression", revenue, parameters);
        }
    }
}
#endif

