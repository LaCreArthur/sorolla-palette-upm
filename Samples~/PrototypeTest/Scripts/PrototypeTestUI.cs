using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sorolla;

namespace Sorolla.Samples
{
    /// <summary>
    /// Beautiful, dynamic test UI for Sorolla SDK Prototype mode.
    /// Automatically detects installed SDKs and shows relevant test buttons.
    /// Self-wiring: finds all buttons by naming convention and binds them automatically.
    /// </summary>
    public class PrototypeTestUI : MonoBehaviour
    {
        #region Private Fields
        
        private GameObject _mainPanel;
        private TextMeshProUGUI _sdkStatusText;
        private TextMeshProUGUI _modeText;
        private TextMeshProUGUI _versionText;
        private TextMeshProUGUI _logText;
        
        private Transform _analyticsSection;
        private Transform _remoteConfigSection;
        private Transform _facebookSection;
        private Transform _crashlyticsSection;
        
        private readonly List<string> _logLines = new();
        private const int MaxLogLines = 50;
        
        // SDK Detection Cache
        private bool _hasGameAnalytics;
        private bool _hasFacebook;
        private bool _hasFirebaseAnalytics;
        private bool _hasFirebaseCrashlytics;
        private bool _hasFirebaseRemoteConfig;
        
        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CacheReferences();
            DetectInstalledSdks();
            SetupUI();
            WireButtons();
        }

        private void Start()
        {
            RefreshStatus();
            Log("🎮 Sorolla Prototype Test UI Ready");
            Log($"   Mode: {(IsPrototypeMode() ? "Prototype" : "Full")}");
            LogInstalledSdks();
        }

        #endregion
        
        #region Self-Wiring

        private void CacheReferences()
        {
            // Main panel is one level up (we're on the root)
            _mainPanel = transform.Find("MainPanel")?.gameObject;
            
            // Find status texts in header
            var header = transform.Find("MainPanel/Header");
            if (header != null)
            {
                _sdkStatusText = FindDeep<TextMeshProUGUI>(header, "StatusValue");
                _modeText = FindDeep<TextMeshProUGUI>(header, "ModeValue");
                _versionText = FindDeep<TextMeshProUGUI>(header, "VersionValue");
            }
            
            // Find sections
            var content = transform.Find("MainPanel/ScrollView/Viewport/Content");
            if (content != null)
            {
                _analyticsSection = content.Find("AnalyticsSection");
                _remoteConfigSection = content.Find("RemoteConfigSection");
                _facebookSection = content.Find("FacebookSection");
                _crashlyticsSection = content.Find("CrashlyticsSection");
                
                _logText = FindDeep<TextMeshProUGUI>(content, "LogText");
            }
        }

        private void WireButtons()
        {
            // Find all buttons and wire them by name convention
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                var name = btn.gameObject.name;
                
                // Check for method suffix pattern: ButtonName_MethodName
                var parts = name.Split('_');
                if (parts.Length >= 2)
                {
                    var methodName = parts[^1]; // Last part
                    var method = GetType().GetMethod(methodName, 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (method != null)
                    {
                        btn.onClick.AddListener(() => method.Invoke(this, null));
                    }
                }
                
                // Also handle special buttons
                if (name.Contains("Toggle"))
                    btn.onClick.AddListener(TogglePanel);
                if (name.Contains("ClearLog"))
                    btn.onClick.AddListener(ClearLog);
            }
        }

        private T FindDeep<T>(Transform parent, string name) where T : Component
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    var comp = child.GetComponent<T>();
                    if (comp != null) return comp;
                }
                var found = FindDeep<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        #endregion

        #region SDK Detection

        private void DetectInstalledSdks()
        {
            // GameAnalytics - always expected in Prototype mode
#if GAMEANALYTICS_INSTALLED
            _hasGameAnalytics = true;
#endif
            
            // Facebook - expected in Prototype mode
#if SOROLLA_FACEBOOK_ENABLED
            _hasFacebook = true;
#endif

            // Firebase (optional)
#if FIREBASE_ANALYTICS_INSTALLED
            _hasFirebaseAnalytics = true;
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            _hasFirebaseCrashlytics = true;
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            _hasFirebaseRemoteConfig = true;
#endif
        }

        private void LogInstalledSdks()
        {
            Log("📦 Detected SDKs:");
            Log($"   • GameAnalytics: {(_hasGameAnalytics ? "✅" : "❌")}");
            Log($"   • Facebook: {(_hasFacebook ? "✅" : "❌")}");
            Log($"   • Firebase Analytics: {(_hasFirebaseAnalytics ? "✅" : "⬜")}");
            Log($"   • Firebase Crashlytics: {(_hasFirebaseCrashlytics ? "✅" : "⬜")}");
            Log($"   • Firebase Remote Config: {(_hasFirebaseRemoteConfig ? "✅" : "⬜")}");
        }

        private bool IsPrototypeMode()
        {
            return Sorolla.Config == null || Sorolla.Config.isPrototypeMode;
        }

        #endregion

        #region UI Setup

        private void SetupUI()
        {
            // Show/hide sections based on installed SDKs
            if (_analyticsSection != null)
                _analyticsSection.gameObject.SetActive(_hasGameAnalytics);
            
            if (_facebookSection != null)
                _facebookSection.gameObject.SetActive(_hasFacebook);
            
            if (_crashlyticsSection != null)
                _crashlyticsSection.gameObject.SetActive(_hasFirebaseCrashlytics);
            
            if (_remoteConfigSection != null)
                _remoteConfigSection.gameObject.SetActive(_hasGameAnalytics || _hasFirebaseRemoteConfig);
        }

        public void RefreshStatus()
        {
            if (_sdkStatusText != null)
            {
                var initialized = Sorolla.IsInitialized;
                _sdkStatusText.text = initialized ? "✅ Initialized" : "⏳ Not Ready";
                _sdkStatusText.color = initialized ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.7f, 0.3f);
            }

            if (_modeText != null)
            {
                _modeText.text = IsPrototypeMode() ? "🧪 Prototype" : "🚀 Full";
            }

            if (_versionText != null)
            {
                _versionText.text = "v2.1.0";
            }
        }

        private void TogglePanel()
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!_mainPanel.activeSelf);
                Log(_mainPanel.activeSelf ? "Panel shown" : "Panel hidden");
            }
        }

        #endregion

        #region Analytics Tests

        public void TestTrackDesign()
        {
            Sorolla.TrackDesign("test_button_click");
            Log("📊 TrackDesign('test_button_click')");
        }

        public void TestTrackDesignWithValue()
        {
            Sorolla.TrackDesign("test_score", 42.5f);
            Log("📊 TrackDesign('test_score', 42.5)");
        }

        public void TestProgressionStart()
        {
            Sorolla.TrackProgression(ProgressionStatus.Start, "World_01", "Level_01");
            Log("🎯 TrackProgression(Start, 'World_01', 'Level_01')");
        }

        public void TestProgressionComplete()
        {
            Sorolla.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_01", score: 1000);
            Log("🎯 TrackProgression(Complete, 'World_01', 'Level_01', score: 1000)");
        }

        public void TestProgressionFail()
        {
            Sorolla.TrackProgression(ProgressionStatus.Fail, "World_01", "Level_01");
            Log("🎯 TrackProgression(Fail, 'World_01', 'Level_01')");
        }

        public void TestResourceSource()
        {
            Sorolla.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
            Log("💰 TrackResource(Source, 'coins', 100, 'reward', 'level_complete')");
        }

        public void TestResourceSink()
        {
            Sorolla.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "power_up");
            Log("💸 TrackResource(Sink, 'coins', 50, 'shop', 'power_up')");
        }

        #endregion

        #region Remote Config Tests

        public void TestFetchRemoteConfig()
        {
            Log("⚙️ Fetching Remote Config...");
            Sorolla.FetchRemoteConfig(success =>
            {
                Log(success ? "⚙️ Remote Config: ✅ Fetched!" : "⚙️ Remote Config: ❌ Failed");
                RefreshStatus();
            });
        }

        public void TestGetStringConfig()
        {
            var value = Sorolla.GetRemoteConfig("welcome_message", "default");
            Log($"⚙️ GetRemoteConfig('welcome_message') = \"{value}\"");
        }

        public void TestGetIntConfig()
        {
            var value = Sorolla.GetRemoteConfigInt("daily_reward", 100);
            Log($"⚙️ GetRemoteConfigInt('daily_reward') = {value}");
        }

        public void TestGetBoolConfig()
        {
            var value = Sorolla.GetRemoteConfigBool("feature_enabled", false);
            Log($"⚙️ GetRemoteConfigBool('feature_enabled') = {value}");
        }

        public void TestGetFloatConfig()
        {
            var value = Sorolla.GetRemoteConfigFloat("difficulty_multiplier", 1.0f);
            Log($"⚙️ GetRemoteConfigFloat('difficulty_multiplier') = {value}");
        }

        public void TestRemoteConfigReady()
        {
            var ready = Sorolla.IsRemoteConfigReady();
            Log($"⚙️ IsRemoteConfigReady() = {ready}");
        }

        #endregion

        #region Facebook Tests

        public void TestFacebookStatus()
        {
#if SOROLLA_FACEBOOK_ENABLED
            Log("📘 Facebook SDK is enabled");
            // Facebook events go through TrackDesign in Prototype mode
#else
            Log("📘 Facebook SDK not installed");
#endif
        }

        #endregion

        #region Crashlytics Tests

        public void TestLogMessage()
        {
            Sorolla.LogCrashlytics("Test log from PrototypeTestUI");
            Log("🔥 LogCrashlytics('Test log from PrototypeTestUI')");
        }

        public void TestSetCustomKey()
        {
            var timestamp = DateTime.Now.Ticks.ToString();
            Sorolla.SetCrashlyticsKey("test_key", timestamp);
            Log($"🔥 SetCrashlyticsKey('test_key', '{timestamp}')");
        }

        public void TestLogException()
        {
            try
            {
                throw new InvalidOperationException("Test exception from PrototypeTestUI");
            }
            catch (Exception ex)
            {
                Sorolla.LogException(ex);
                Log($"🔥 LogException({ex.GetType().Name})");
            }
        }

        public void TestForceCrash()
        {
            Log("⚠️ Force crash in 2 seconds...");
            Invoke(nameof(DoCrash), 2f);
        }

        private void DoCrash()
        {
            Debug.LogError("[PrototypeTestUI] Forcing crash!");
            throw new Exception("Forced crash from PrototypeTestUI");
        }

        #endregion

        #region Logging

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"<color=#888>[{timestamp}]</color> {message}";
            
            _logLines.Insert(0, line);
            if (_logLines.Count > MaxLogLines)
                _logLines.RemoveAt(_logLines.Count - 1);
            
            if (_logText != null)
                _logText.text = string.Join("\n", _logLines);

            Debug.Log($"[PrototypeTest] {message}");
        }

        private void ClearLog()
        {
            _logLines.Clear();
            if (_logText != null)
                _logText.text = "";
            Log("Log cleared");
        }

        #endregion
    }
}
