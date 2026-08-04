using System.Collections.Generic;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        const string ManifestFile = "Assets/Plugins/Android/AndroidManifest.xml";
        const string LauncherManifestFile = "Assets/Plugins/Android/LauncherManifest.xml";

        /// <summary>
        ///     Check Android manifest health. Everything reported here is RESIDUE, in the strict sense: the
        ///     sanitizer attempts a repair for each of these, saves the file, and RE-DETECTS - so anything
        ///     still in its Remaining* set survived a repair attempt. The fixes say exactly that and name the
        ///     manual edit, because pressing Refresh only repeats the write that already failed to stick.
        ///
        ///     There is no second detection path here. The old fresh-detection fallback could disagree with
        ///     what the sanitizer had just done, and it was unreachable anyway: every caller runs
        ///     RunSafeAutoFixes immediately before RunAllChecks, which is what stashes these diagnostics.
        /// </summary>
        static void CheckAndroidManifest(List<ValidationResult> results,
            AndroidManifestSanitizer.ManifestDiagnostics diagnostics)
        {
            // Validation ran without its repair pass - report nothing rather than invent a detection the
            // fixes below could not honestly describe. The row then states that no result was produced.
            if (diagnostics == null) return;

            bool hasIssues = false;

            var orphaned = diagnostics.RemainingOrphans;
            var duplicates = diagnostics.RemainingDuplicates;
            string wrongActivity = diagnostics.RemainingWrongActivity;
            string themeMismatch = diagnostics.RemainingThemeMismatch;
            string launcherIssue = diagnostics.RemainingLauncherIssue;

            if (orphaned.Count > 0)
            {
                hasIssues = true;
                foreach ((SdkId sdkId, string[] entries) in orphaned)
                {
                    string sdkName = SdkRegistry.All[sdkId].Name;
                    results.Add(Error(
                        ReadinessChecks.AndroidManifest,
                        $"AndroidManifest.xml has {sdkName} entries but SDK is not installed!\n" +
                        $"  Found patterns: {string.Join(", ", entries)}\n" +
                        "  This WILL crash at runtime.\n" +
                        "  Palette already tried to remove them and re-checked the file afterwards; they are still there, so the rewrite did not take.",
                        $"Open {ManifestFile} and delete the elements matching the patterns listed above, " +
                        $"or reinstall {sdkName}"));
                }
            }

            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.Add(Error(
                    ReadinessChecks.AndroidManifest,
                    "AndroidManifest.xml has duplicate activity declarations!\n" +
                    $"  Duplicates: {string.Join(", ", duplicates)}\n" +
                    "  This WILL cause build failures.\n" +
                    "  Palette already tried to remove the extra declarations and re-checked the file afterwards; they are still there, so the rewrite did not take.",
                    $"Open {ManifestFile} and delete the extra <activity> declaration for each name listed " +
                    "above, keeping one"));
            }

            if (wrongActivity != null)
            {
                hasIssues = true;
                results.Add(Error(
                    ReadinessChecks.AndroidManifest,
                    "AndroidManifest.xml has wrong main activity!\n" +
                    $"  Found: {wrongActivity}\n" +
                    $"  Expected: {AndroidManifestSanitizer.GetExpectedMainActivity()}\n" +
                    "  The app WILL crash on launch.\n" +
                    "  Palette already tried to rewrite it and re-checked the file afterwards; it is unchanged, so the rewrite did not take.",
                    $"Open {ManifestFile} and set the launcher activity's android:name to " +
                    $"{AndroidManifestSanitizer.GetExpectedMainActivity()}"));
            }

            if (themeMismatch != null)
            {
                hasIssues = true;
                results.Add(Error(
                    ReadinessChecks.AndroidManifest,
                    "AndroidManifest.xml activity theme issue!\n" +
                    $"  {themeMismatch}\n" +
                    "  This WILL cause a Gradle merge conflict on build.\n" +
                    "  Palette already tried to correct it and re-checked the file afterwards; it is unchanged, so the rewrite did not take.",
                    $"Open {ManifestFile} and correct the activity's android:theme attribute as described " +
                    "above"));
            }

            if (launcherIssue != null)
            {
                hasIssues = true;
                results.Add(Error(
                    ReadinessChecks.AndroidManifest,
                    $"LauncherManifest.xml issue: {launcherIssue}\n" +
                    "  The app will install but fail to launch.\n" +
                    "  Palette already tried to repair it and re-checked the file afterwards; it is unchanged, so the rewrite did not take.",
                    $"Open {LauncherManifestFile} and correct the issue described above, or turn off Custom " +
                    "Launcher Manifest in Player Settings > Publishing Settings so Unity generates it"));
            }

            if (!hasIssues)
                results.Add(Valid(ReadinessChecks.AndroidManifest, "Manifest clean"));
        }
    }
}
