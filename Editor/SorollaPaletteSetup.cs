using UnityEditor;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Simplified setup - now just delegates to specialized managers
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaPaletteSetup
    {
        static SorollaPaletteSetup()
        {
            // Run simplified setup on package import
            EditorApplication.delayCall += RunSetup;
        }

        [MenuItem("Tools/Sorolla Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            RunSetup();
        }

        private static void RunSetup()
        {
            Debug.Log("[Sorolla Palette] Running initial setup...");

            // Add required registries
            var registriesAdded = ManifestManager.AddOrUpdateRegistry("Game Package Registry by Google",
                    "https://unityregistry-pa.googleapis.com/", new[] { "com.google" }) ||
                ManifestManager.AddOrUpdateRegistry("package.openupm.com",
                    "https://package.openupm.com", new[] { "com.gameanalytics", "com.google.external-dependency-manager" });

            // Install core dependencies
            var gaInstalled = InstallationManager.InstallGameAnalytics();
            var edmInstalled = InstallationManager.InstallExternalDependencyManager();

            if (registriesAdded || gaInstalled || edmInstalled)
            {
                Debug.Log("[Sorolla Palette] Setup complete. Package Manager will resolve dependencies.");
            }
            else
            {
                Debug.Log("[Sorolla Palette] All dependencies already configured.");
            }
        }

        /// <summary>
        ///     Installs AppLovin MAX SDK - delegates to InstallationManager
        /// </summary>
        public static void InstallAppLovinMAX()
        {
            InstallationManager.InstallAppLovinMAX();
        }

        /// <summary>
        ///     Uninstalls AppLovin MAX package from manifest
        /// </summary>
        public static void UninstallAppLovinMAX()
        {
            Debug.Log("[Sorolla Palette] Uninstalling AppLovin MAX SDK...");
            var removed = ManifestManager.RemoveDependencies(new[] { "com.applovin.mediation.ads" });
            if (removed)
            {
                UnityEditor.PackageManager.Client.Resolve();
                Debug.Log("[Sorolla Palette] AppLovin MAX removed. Resolving packages...");
            }
            else
            {
                Debug.Log("[Sorolla Palette] AppLovin MAX was not present in manifest.");
            }
        }

        /// <summary>
        ///     Installs Adjust SDK - delegates to InstallationManager
        /// </summary>
        public static void InstallAdjustSDK()
        {
            InstallationManager.InstallAdjustSDK();
        }
    }
}