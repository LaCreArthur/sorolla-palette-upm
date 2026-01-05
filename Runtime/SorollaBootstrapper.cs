using System.Collections;
using Sorolla.Palette.ATT;
using UnityEngine;
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     Entry point for Palette SDK.
    ///     Auto-initializes at startup - NO MANUAL SETUP REQUIRED.
    ///     Handles iOS ATT before initializing SDKs.
    ///     In Editor, shows fake dialogs for testing.
    /// </summary>
    public class SorollaBootstrapper : MonoBehaviour
    {
        const string ContextScreenPath = "ContextScreen";
        const float PollInterval = 0.5f;

        static SorollaBootstrapper s_instance;

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Start() => StartCoroutine(Initialize());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Palette] Sorolla SDK auto-initializing (plug & play mode)...");

            var go = new GameObject("[Palette SDK]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

        IEnumerator Initialize()
        {
#if UNITY_EDITOR
            Debug.Log("[Palette] Running in Unity Editor - using simulated ATT flow");
            yield return ShowContextAndRequestEditor();
#elif UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
            Debug.Log("[Palette] iOS platform detected - checking ATT status");
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();

            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                Debug.Log("[Palette] ATT not determined - showing consent dialog");
                yield return ShowContextAndRequest();
            }
            else
            {
                Debug.Log($"[Palette] ATT already determined: {status}");
                Palette.Initialize(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
            }
#else
            // Android or other platforms - initialize with consent granted
            Debug.Log("[Palette] Android/Other platform - initializing with consent granted");
            Palette.Initialize(true);
            yield break;
#endif
        }

#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
        IEnumerator ShowContextAndRequest()
        {
            GameObject contextScreen = null;
            var prefab = Resources.Load<GameObject>(ContextScreenPath);

            if (prefab != null)
            {
                contextScreen = Instantiate(prefab);
                var canvas = contextScreen.GetComponent<Canvas>();
                if (canvas) canvas.sortingOrder = 999;
                Debug.Log("[Palette] Pre-ATT context screen displayed to user");
            }
            else
            {
                Debug.LogWarning("[Palette] ContextScreen prefab not found in Resources. " +
                    "Triggering ATT dialog directly (recommended: add context screen for better consent rates).");
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }

            // Wait for user decision
            ATTrackingStatusBinding.AuthorizationTrackingStatus status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            int waitCount = 0;
            while (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return new WaitForSeconds(PollInterval);
                status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
                waitCount++;
                
                // Safety timeout after 2 minutes
                if (waitCount > 240)
                {
                    Debug.LogWarning("[Palette] ATT dialog timeout - proceeding without tracking consent");
                    break;
                }
            }

            if (contextScreen != null)
                Destroy(contextScreen);

            bool hasConsent = status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
            Debug.Log($"[Palette] ATT decision received: {status} (consent granted: {hasConsent})");
            Palette.Initialize(hasConsent);
        }
#endif

#if UNITY_EDITOR
        IEnumerator ShowContextAndRequestEditor()
        {
            var prefab = Resources.Load<GameObject>(ContextScreenPath);

            if (prefab == null)
            {
                Debug.LogWarning("[Palette] ContextScreen prefab not found in Resources. " +
                    "This is optional - initializing without ATT dialog. " +
                    "To add ATT testing: Palette > ATT > Create PreATT Popup Prefab");
                Palette.Initialize(true);
                yield break;
            }

            GameObject contextScreen = Instantiate(prefab);
            var canvas = contextScreen.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 999;
            Debug.Log("[Palette] Context screen displayed (Editor mode - simulated ATT flow)");

            // Wait for ContextScreenView to trigger FakeATTDialog
            var view = contextScreen.GetComponent<ContextScreenView>();
            bool completed = false;

            view.SentTrackingAuthorizationRequest += () =>
            {
                Destroy(contextScreen);
                completed = true;
            };

            // Wait for completion (FakeATTDialog handles the decision)
            while (!completed)
            {
                yield return null;
            }

            // In Editor, we just assume consent for simplicity
            Debug.Log("[Palette] ATT flow complete (Editor).");
            Palette.Initialize(true);
        }
#endif
    }
}
