using System;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    ///     DRY base class for ad management - eliminates duplicated ad loading logic
    /// </summary>
    public abstract class BaseAdManager<T> where T : class
    {
        protected bool _adReady;
        protected string _adUnitId;
        protected bool _isInitialized;
        protected Action _onComplete;
        protected Action _onFailed;

        protected abstract string AdTypeName { get; }

        public void Initialize(string adUnitId)
        {
            _adUnitId = adUnitId;
            _isInitialized = true;
            Debug.Log($"[{AdTypeName} Manager] Initialized with unit ID: {adUnitId}");
        }

        public void ShowAd(Action onComplete, Action onFailed)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning($"[{AdTypeName} Manager] Not initialized");
                onFailed?.Invoke();
                return;
            }

            if (!_adReady || !IsAdReady())
            {
                Debug.LogWarning($"[{AdTypeName} Manager] {AdTypeName} not ready");
                onFailed?.Invoke();
                LoadAd(); // Try to load for next time
                return;
            }

            _onComplete = onComplete;
            _onFailed = onFailed;

            Debug.Log($"[{AdTypeName} Manager] Showing {AdTypeName.ToLower()}");
            ShowAdInternal();
        }

        protected abstract void ShowAdInternal();
        protected abstract void LoadAd();
        protected abstract bool IsAdReady();

        protected void OnAdLoaded()
        {
            Debug.Log($"[{AdTypeName} Manager] {AdTypeName} loaded");
            _adReady = true;
        }

        protected void OnAdLoadFailed(string error = null)
        {
            var errorMsg = error != null ? $": {error}" : "";
            Debug.LogWarning($"[{AdTypeName} Manager] {AdTypeName} failed to load{errorMsg}");
            _adReady = false;

            // Retry after delay
            LoadAd();
        }

        protected void OnAdDisplayed()
        {
            Debug.Log($"[{AdTypeName} Manager] {AdTypeName} displayed");
        }

        protected void OnAdHidden()
        {
            Debug.Log($"[{AdTypeName} Manager] {AdTypeName} hidden");
            _adReady = false;

            // Load next ad
            LoadAd();
        }

        protected void OnAdFailedToDisplay(string error = null)
        {
            var errorMsg = error != null ? $": {error}" : "";
            Debug.LogWarning($"[{AdTypeName} Manager] {AdTypeName} failed to display{errorMsg}");
            _adReady = false;

            _onFailed?.Invoke();
            _onFailed = null;
            _onComplete = null;

            // Load next ad
            LoadAd();
        }

        protected void OnAdCompleted()
        {
            Debug.Log($"[{AdTypeName} Manager] {AdTypeName} completed");

            _onComplete?.Invoke();
            _onComplete = null;
            _onFailed = null;
        }
    }
}