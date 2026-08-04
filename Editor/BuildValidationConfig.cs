using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that SorollaConfig settings match installed SDKs, AND that the asset lives at the
        ///     exact path (<see cref="ExpectedConfigPath"/>) Resources.Load/runtime require - not just
        ///     "some SorollaConfig exists somewhere" (F14, 2026-07-21 audit: this window's own
        ///     asset-finder, AssetDatabase.FindAssets, can load/edit a SorollaConfig outside Resources/
        ///     entirely, which Resources.Load then can never resolve - the hero header, this check, and
        ///     the Create-button availability disagreed as a result). Escalates to Error in Full mode
        ///     (F14 ruling, 2026-07-21 ~12:30, DR-133 alignment: a missing/misplaced config wedges a Full
        ///     build's init forever, so this is a real build blocker there, not just a warning) - stays a
        ///     Warning in Prototype/unconfigured, where runtime continues in degraded mode.
        /// </summary>
        const string ExpectedConfigPath = "Assets/Resources/SorollaConfig.asset";

        static ValidationResult ConfigSyncIssue(string message, string fix) =>
            !SorollaSettings.IsPrototype
                ? Error(ReadinessChecks.ConfigSync, message, fix)
                : Warning(ReadinessChecks.ConfigSync, message, fix);

        static void CheckConfigSync(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
                if (guids.Length > 0)
                {
                    string actualPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    results.Add(ConfigSyncIssue(
                        $"SorollaConfig exists at '{actualPath}', not '{ExpectedConfigPath}'.\n" +
                        "  Resources.Load (and the runtime) cannot find it there, so the SDK silently runs unconfigured.",
                        // Moving the existing asset comes FIRST here: opening the window creates a blank
                        // config at the canonical path and leaves the misplaced one behind, which would lose
                        // whatever is already filled in. The collision is named because it is the LIKELY
                        // state by the time anyone reads this - the window creates that blank one on open,
                        // so a studio following this remedy finds the destination already occupied. Neither
                        // remedy names the old "Create Configuration Asset" button - no such control exists.
                        $"Move the existing asset to {ExpectedConfigPath}, replacing the blank one if opening " +
                        "Tools > Sorolla Palette SDK already created it - the window creates a blank config " +
                        "there, and the misplaced asset's settings do not carry over to it"));
                }
                else
                {
                    results.Add(ConfigSyncIssue(
                        "SorollaConfig not found, so the SDK runs unconfigured.",
                        $"Open Tools > Sorolla Palette SDK - the window creates {ExpectedConfigPath} when it opens"));
                }

                return;
            }

            string configPath = AssetDatabase.GetAssetPath(config);
            if (configPath != ExpectedConfigPath)
            {
                // Resources.Load scans EVERY folder literally named "Resources" anywhere under Assets, not
                // just the top-level one - a config in a different Resources/ folder still resolves here
                // but isn't at the canonical path other tooling assumes.
                results.Add(ConfigSyncIssue(
                    $"SorollaConfig resolves via Resources.Load but lives at '{configPath}', not the canonical '{ExpectedConfigPath}'.",
                    $"Move the asset to {ExpectedConfigPath}"));
                return;
            }

            results.Add(Valid(ReadinessChecks.ConfigSync, "Config synced"));
        }

        /// <summary>
        ///     Editor-workflow repair for package and scoped-registry state, returning the concrete changes
        ///     it made. This can trigger asynchronous Unity Package Manager work, so callers must refresh
        ///     again when package registration settles.
        /// </summary>
        public static List<string> ResolveRequiredPackages()
        {
            var repairs = new List<string>();

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
                return repairs;

            // Auto-install missing required SDKs. Only real manifest additions are reported: a required
            // package that is present but not yet resolved is offered to the installer on every pass, and
            // it must not read as a fresh repair each time.
            if (!SdkDetector.AreAllRequiredInstalled(config.isPrototypeMode))
            {
                string modeName = config.isPrototypeMode ? "Prototype" : "Full";
                Debug.Log($"{Tag} Auto-fixing: Installing missing required SDKs for {modeName} mode...");
                foreach (SdkInfo sdk in SdkInstaller.InstallRequiredSdks(config.isPrototypeMode))
                    repairs.Add($"Added {sdk.Name} to Packages/manifest.json - {modeName} mode requires it " +
                                "(Package Manager is resolving it)");
            }

            // Unconditionally, not as the alternative to installing: the install path covers the registries
            // of the REQUIRED set only, so an installed optional capability with a missing scope would be
            // left unrepaired precisely on the passes where something else needed installing - and the
            // check's residue text would then blame a write failure on a repair that never ran. Repeating
            // it after an install is free: it only writes when an entry is actually absent.
            foreach (SdkInfo sdk in SdkInstaller.EnsureInstalledRegistries())
                repairs.Add($"Restored the scoped registry for {sdk.Name} ({sdk.Scope}) in " +
                            "Packages/manifest.json");

            if (repairs.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{Tag} Config sync issues auto-fixed");
            }

            return repairs;
        }
    }
}
