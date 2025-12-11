using Sorolla.ATT;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Controls the Privacy & CMP section buttons.
    ///     Allows testing ATT and CMP dialogs from Debug UI.
    /// </summary>
    public class PrivacyController : UIComponentBase
    {
        [Header("Buttons")]
        [SerializeField] Button showATTButton;
        [SerializeField] Button showCMPButton;
        [SerializeField] Button resetConsentButton;

        void Awake()
        {
            showATTButton.onClick.AddListener(HandleShowATT);
            showCMPButton.onClick.AddListener(HandleShowCMP);
            resetConsentButton.onClick.AddListener(HandleResetConsent);
        }

        void OnDestroy()
        {
            showATTButton.onClick.RemoveAllListeners();
            showCMPButton.onClick.RemoveAllListeners();
            resetConsentButton.onClick.RemoveAllListeners();
        }

        void HandleShowATT()
        {
            DebugPanelManager.Instance?.Log("Showing PreATT -> ATT flow...", LogSource.Sorolla);
            
            // Load and show PreATT (ContextScreen) first
            var prefab = Resources.Load<GameObject>("ContextScreen");
            if (prefab == null)
            {
                DebugPanelManager.Instance?.Log("ContextScreen prefab not found, showing ATT directly", LogSource.Sorolla, LogLevel.Warning);
                ShowFakeATT();
                return;
            }

            GameObject contextScreen = Object.Instantiate(prefab);
            var canvas = contextScreen.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 999;

            // When user clicks Continue, ContextScreenView triggers FakeATT and fires event
            var view = contextScreen.GetComponent<ContextScreenView>();
            view.SentTrackingAuthorizationRequest += () =>
            {
                Object.Destroy(contextScreen);
                // FakeATT is already shown by ContextScreenView, just log completion
                DebugPanelManager.Instance?.Log("ATT flow completed", LogSource.Sorolla);
            };
        }

        void ShowFakeATT()
        {
            FakeATTDialog.Show(allowed =>
            {
                string result = allowed ? "Allowed" : "Denied";
                DebugPanelManager.Instance?.Log($"ATT Result: {result}", LogSource.Sorolla);
                SorollaDebugEvents.RaiseShowToast($"ATT: {result}", allowed ? ToastType.Success : ToastType.Warning);
            });
        }

        void HandleShowCMP()
        {
            DebugPanelManager.Instance?.Log("Showing CMP dialog...", LogSource.Sorolla);
            FakeCMPDialog.Show(accepted =>
            {
                string result = accepted ? "Accepted" : "Rejected";
                DebugPanelManager.Instance?.Log($"CMP Result: {result}", LogSource.Sorolla);
                SorollaDebugEvents.RaiseShowToast($"CMP: {result}", accepted ? ToastType.Success : ToastType.Warning);
            });
        }

        void HandleResetConsent()
        {
            // In a real implementation, this would clear stored consent
            DebugPanelManager.Instance?.Log("Consent reset (mock)", LogSource.Sorolla);
            SorollaDebugEvents.RaiseShowToast("Consent Reset", ToastType.Info);
        }
    }
}
