using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Shows SDK health status. Self-sufficient - checks Sorolla.IsInitialized on start.
    /// </summary>
    public class SDKHealthIndicator : UIComponentBase
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _sdkNameLabel;
        [SerializeField] Image _statusDot;

        [SerializeField] string _sdkName;
        [SerializeField] bool _isEnabled = true;
        [SerializeField] bool _isHealthy;

        void Start()
        {
            // Auto-detect SDK health based on name
            CheckSDKStatus();
        }

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnSDKHealthChanged += HandleSDKHealthChanged;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnSDKHealthChanged -= HandleSDKHealthChanged;

        void HandleSDKHealthChanged(string sdkName, bool isHealthy)
        {
            if (sdkName == _sdkName)
            {
                SetHealth(isHealthy);
            }
        }

        void CheckSDKStatus()
        {
            // Check actual SDK status based on name
            bool isHealthy = _sdkName.ToLower() switch
            {
                "gameanalytics" or "ga" => Sorolla.IsInitialized,
                "sorolla" => Sorolla.IsInitialized,
                "firebase" => Sorolla.IsInitialized && Sorolla.Config != null && Sorolla.Config.enableFirebaseAnalytics,
                "crashlytics" => Sorolla.IsInitialized && Sorolla.Config != null && Sorolla.Config.enableCrashlytics,
                "remoteconfig" or "remote config" => Sorolla.IsRemoteConfigReady(),
                "max" or "applovin" => Sorolla.IsInitialized && Sorolla.Config != null && !Sorolla.Config.isPrototypeMode,
                "facebook" or "fb" => Sorolla.IsInitialized && Sorolla.Config != null && Sorolla.Config.isPrototypeMode,
                "adjust" => Sorolla.IsInitialized && Sorolla.Config != null && !Sorolla.Config.isPrototypeMode,
                _ => Sorolla.IsInitialized,
            };

            _isHealthy = isHealthy;
            UpdateVisual();
        }

        public void Setup(string sdkName, bool enabled, bool healthy)
        {
            _sdkName = sdkName;
            _isEnabled = enabled;
            _isHealthy = healthy;
            UpdateVisual();
        }

        public void SetHealth(bool healthy)
        {
            _isHealthy = healthy;
            UpdateVisual();
        }

        void UpdateVisual()
        {
            _sdkNameLabel.text = _sdkName;
            _sdkNameLabel.color = _isEnabled ? Theme.textPrimary : Theme.textDisabled;
            _statusDot.color = _isEnabled
                ? _isHealthy ? Theme.statusActive : Theme.statusIdle
                : Theme.statusIdle;
        }
    }
}
