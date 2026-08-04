using System;
using System.Collections.Generic;
using System.IO;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    static class GreenlightEvaluator
    {
        /// <summary>
        ///     The fingerprint is captured ONCE, inside the evaluator. Re-capturing it here shelled out to
        ///     git a second time for an identical answer, on a path that runs before every build.
        /// </summary>
        internal static ReadinessReport Evaluate(List<BuildValidator.ValidationResult> results) =>
            ReadinessEvaluator.Evaluate(CaptureContext(), results);

        internal static ReadinessContext CaptureContext()
        {
            bool resolved = TryDetectInstalledModules(out SdkModule modules);
            return new ReadinessContext
            {
                Mode = SorollaSettings.Mode switch
                {
                    SorollaMode.Prototype => EvalMode.Prototype,
                    SorollaMode.Full => EvalMode.Full,
                    _ => EvalMode.Unknown,
                },
                Platform = EditorUserBuildSettings.activeBuildTarget switch
                {
                    BuildTarget.Android => ReadinessPlatform.Android,
                    BuildTarget.iOS => ReadinessPlatform.iOS,
                    _ => ReadinessPlatform.Unknown,
                },
                InstalledModules = modules,
                ModulesResolved = resolved,
            };
        }

        internal static bool TryDetectInstalledModules(out SdkModule modules)
        {
            modules = SdkModule.None;
            Dictionary<string, object> dependencies = ReadManifestDependencies();
            if (dependencies == null)
                return false;

            if (HasPackage(dependencies, SdkId.GameAnalytics)) modules |= SdkModule.GameAnalytics;
            if (HasPackage(dependencies, SdkId.Facebook)) modules |= SdkModule.Facebook;
            if (HasPackage(dependencies, SdkId.AppLovinMAX)) modules |= SdkModule.AppLovinMax;
            if (HasPackage(dependencies, SdkId.Adjust)) modules |= SdkModule.Adjust;
            if (HasPackage(dependencies, SdkId.FirebaseApp)) modules |= SdkModule.FirebaseApp;
            if (HasPackage(dependencies, SdkId.FirebaseAnalytics)) modules |= SdkModule.FirebaseAnalytics;
            if (HasPackage(dependencies, SdkId.FirebaseCrashlytics)) modules |= SdkModule.FirebaseCrashlytics;
            if (HasPackage(dependencies, SdkId.FirebaseRemoteConfig)) modules |= SdkModule.FirebaseRemoteConfig;
            if (dependencies.ContainsKey("com.unity.purchasing")) modules |= SdkModule.UnityIap;
            return true;
        }

        static bool HasPackage(Dictionary<string, object> dependencies, SdkId id) =>
            SdkRegistry.All.TryGetValue(id, out SdkInfo info) && dependencies.ContainsKey(info.PackageId);

        static Dictionary<string, object> ReadManifestDependencies()
        {
            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!File.Exists(path))
                    return null;
                return MiniJson.Deserialize(File.ReadAllText(path)) is Dictionary<string, object> manifest &&
                       manifest.TryGetValue("dependencies", out object value) &&
                       value is Dictionary<string, object> dependencies
                    ? dependencies
                    : null;
            }
            catch
            {
                return null;
            }
        }

        internal static string VerdictLabel(ReadinessOutcome outcome, int failCount, int warnCount) =>
            outcome switch
            {
                ReadinessOutcome.Fail => "FAILING",
                ReadinessOutcome.Incomplete => "INCOMPLETE",
                ReadinessOutcome.Warn => $"{failCount + warnCount} ISSUES",
                ReadinessOutcome.Pass => "HEALTHY",
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled outcome."),
            };

        internal static StatusBadge.Severity BadgeSeverity(ReadinessOutcome outcome) => outcome switch
        {
            ReadinessOutcome.Fail => StatusBadge.Severity.Fail,
            ReadinessOutcome.Incomplete => StatusBadge.Severity.Wait,
            ReadinessOutcome.Warn => StatusBadge.Severity.Advisory,
            ReadinessOutcome.Pass => StatusBadge.Severity.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled outcome."),
        };
    }
}
