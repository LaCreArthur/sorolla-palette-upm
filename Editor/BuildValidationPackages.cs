using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that all required SDKs for the current mode are installed.
        /// </summary>
        static void CheckRequiredSdks(List<ValidationResult> results)
        {
            if (!SorollaSettings.IsConfigured)
            {
                results.Add(Skipped(ReadinessChecks.RequiredSdks, "Mode not configured"));
                return;
            }

            var missing = new List<string>();
            foreach (SdkInfo sdk in SdkRegistry.GetRequired(SorollaSettings.IsPrototype))
            {
                if (!SdkDetector.IsInstalled(sdk))
                    missing.Add(sdk.Name);
            }

            if (missing.Count > 0)
            {
                string modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(Error(
                    ReadinessChecks.RequiredSdks,
                    $"Missing required SDKs for {modeName} mode:\n" +
                    $"  {string.Join(", ", missing)}",
                    // Refresh is the REAL repair here, not a loop: it runs ResolveRequiredPackages, which
                    // installs exactly the required set. The vendor group headers deliberately show no
                    // Install button for an auto-installed SDK, so naming one would send the studio to a
                    // control that is not there. Resolution is asynchronous, hence the second clause.
                    "Click Refresh to auto-install missing SDKs; if the row survives a completed resolve, " +
                    "read the Package Manager error in the Console and check Packages/manifest.json"));
            }
            else
            {
                string modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(Valid(ReadinessChecks.RequiredSdks, $"All required SDKs present for {modeName} mode"));
            }
        }

        /// <summary>
        ///     Check for version mismatches between SdkRegistry and manifest.
        ///     Only warns if manifest version is OLDER than expected (newer is fine).
        /// </summary>
        static void CheckVersionMismatches(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            bool hasIssues = false;

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (!dependencies.TryGetValue(sdk.PackageId, out object manifestValue))
                    continue; // Not installed, checked elsewhere

                string manifestVersion = manifestValue?.ToString() ?? "";
                string expectedVersion = sdk.DependencyValue;

                if (string.IsNullOrEmpty(expectedVersion))
                    continue; // Git URL or no version

                // Skip if versions match exactly
                if (manifestVersion == expectedVersion)
                    continue;

                // For Git URLs, compare tags
                if (manifestVersion.Contains("#") && expectedVersion.Contains("#"))
                {
                    string manifestTag = manifestVersion.Split('#').LastOrDefault();
                    string expectedTag = expectedVersion.Split('#').LastOrDefault();

                    if (manifestTag == expectedTag)
                        continue; // Tags match

                    // Compare Git URL tags as versions
                    if (CompareVersions(manifestTag, expectedTag) >= 0)
                        continue; // Manifest tag is newer or equal

                    hasIssues = true;
                    results.Add(Warning(
                        ReadinessChecks.VersionMismatches,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedTag}\n  Found: {manifestTag}",
                        "Update the package to the minimum required version"));
                    continue;
                }

                // Compare semantic versions - only warn if manifest is OLDER
                if (CompareVersions(manifestVersion, expectedVersion) < 0)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        ReadinessChecks.VersionMismatches,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedVersion}\n  Found: {manifestVersion}",
                        "Update the package to the minimum required version"));
                }
            }

            // Add valid result if no issues found
            if (!hasIssues)
                results.Add(Valid(ReadinessChecks.VersionMismatches, "All SDK versions OK"));
        }

        /// <summary>
        ///     Compare two version strings. Returns:
        ///     -1 if v1 &lt; v2, 0 if equal, 1 if v1 &gt; v2
        /// </summary>
        static int CompareVersions(string v1, string v2)
        {
            if (v1 == v2) return 0;
            if (string.IsNullOrEmpty(v1)) return -1;
            if (string.IsNullOrEmpty(v2)) return 1;

            try
            {
                int[] parts1 = v1.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
                int[] parts2 = v2.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();

                int maxLen = Math.Max(parts1.Length, parts2.Length);
                for (int i = 0; i < maxLen; i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;

                    if (p1 < p2) return -1;
                    if (p1 > p2) return 1;
                }

                return 0;
            }
            catch
            {
                // Fallback to string comparison
                return string.Compare(v1, v2, StringComparison.Ordinal);
            }
        }

        /// <summary>
        ///     Check mode consistency - verify installed SDKs match current mode
        /// </summary>
        static void CheckModeConsistency(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            bool hasIssues = false;

            if (!SorollaSettings.IsConfigured)
            {
                // Fix hint no longer tells you to open the window you're already inside (F6, 2026-07-21
                // audit) - points at the mode switch control in this same window's hero header instead.
                results.Add(Warning(
                    ReadinessChecks.ModeConsistency,
                    "No SDK mode configured.",
                    "Select Prototype or Full using the mode switch above"));
                return;
            }

            bool isPrototype = SorollaSettings.IsPrototype;
            string modeName = isPrototype ? "Prototype" : "Full";

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                bool isInstalled = dependencies.ContainsKey(sdk.PackageId);

                // Check PrototypeOnly SDKs in Full mode
                if (sdk.Requirement == SdkRequirement.PrototypeOnly && !isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        ReadinessChecks.ModeConsistency,
                        $"{sdk.Name} is installed but only needed in Prototype mode (current: {modeName})",
                        "Switch to Prototype mode or remove the SDK"));
                }

                // Check FullOnly SDKs missing in Full mode
                if (sdk.Requirement == SdkRequirement.FullOnly && !isPrototype && !isInstalled)
                {
                    hasIssues = true;
                    results.Add(Error(
                        ReadinessChecks.ModeConsistency,
                        $"{sdk.Name} is required in Full mode but not installed",
                        "Install the SDK or switch to Prototype mode"));
                }

                // Check FullOnly SDKs in Prototype mode
                if (sdk.Requirement == SdkRequirement.FullOnly && isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        ReadinessChecks.ModeConsistency,
                        $"{sdk.Name} is installed but only needed in Full mode (current: {modeName})",
                        "Switch to Full mode or remove the SDK"));
                }
            }

            if (!hasIssues)
                results.Add(Valid(ReadinessChecks.ModeConsistency, $"No mode-mismatched SDKs installed ({modeName} mode)"));
        }

        /// <summary>
        ///     Check that required scoped registries are configured
        /// </summary>
        static void CheckScopedRegistries(List<ValidationResult> results,
            Dictionary<string, object> dependencies,
            List<object> registries)
        {
            bool hasIssues = false;

            // Build list of all scopes in registries
            var configuredScopes = new HashSet<string>();
            foreach (object reg in registries)
            {
                if (reg is Dictionary<string, object> registry &&
                    registry.TryGetValue("scopes", out object scopesObj) &&
                    scopesObj is List<object> scopes)
                {
                    foreach (object scope in scopes)
                        configuredScopes.Add(scope.ToString());
                }
            }

            // Check each installed SDK has required scope
            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (string.IsNullOrEmpty(sdk.Scope))
                    continue; // No scope needed (Unity registry or Git URL)

                if (!dependencies.ContainsKey(sdk.PackageId))
                    continue; // Not installed

                if (!configuredScopes.Contains(sdk.Scope))
                {
                    hasIssues = true;
                    // Fix hint repointed at reality (F6, 2026-07-21 audit): there is no registry UI in this
                    // window at all - scopedRegistries lives only in Packages/manifest.json, so the manual
                    // action names that file. Refresh re-adds the registry for REQUIRED SDKs only, which is
                    // why it is the retry rather than the primary remedy.
                    results.Add(Error(
                        ReadinessChecks.ScopedRegistries,
                        $"Missing scoped registry for {sdk.Name}\n" +
                        $"  Required scope: {sdk.Scope}\n" +
                        "  The package resolves from the wrong registry (or not at all) until this scope is " +
                        "listed.",
                        $"Add \"{sdk.Scope}\" to the scopedRegistries entry in Packages/manifest.json, " +
                        "then click Refresh"));
                }
            }

            if (!hasIssues)
                results.Add(Valid(ReadinessChecks.ScopedRegistries, "All registries configured"));
        }
    }
}
