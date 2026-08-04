using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check Firebase module coherence - FirebaseApp required if other modules installed.
        /// </summary>
        static void CheckFirebaseCoherence(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            var firebaseModules = new[]
            {
                SdkId.FirebaseAnalytics,
                SdkId.FirebaseCrashlytics,
                SdkId.FirebaseRemoteConfig,
            };

            bool hasFirebaseApp = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseApp].PackageId);
            var installedModules = firebaseModules
                .Where(id => dependencies.ContainsKey(SdkRegistry.All[id].PackageId))
                .Select(id => SdkRegistry.All[id].Name)
                .ToList();

            if (installedModules.Count > 0 && !hasFirebaseApp)
            {
                results.Add(Error(
                    ReadinessChecks.FirebaseCoherence,
                    $"Firebase modules installed without FirebaseApp:\n  {string.Join(", ", installedModules)}",
                    "Install com.google.firebase.app or remove Firebase modules"));
            }
            // FirebaseApp counts as Firebase being installed, matching HasAnyFirebaseModule: a project with
            // FirebaseApp alone ships the native SDK and needs the config file, so this row must not report
            // it as "Firebase not installed" while the config row grades it.
            else if (HasAnyFirebaseModule(dependencies))
            {
                results.Add(Valid(ReadinessChecks.FirebaseCoherence, "Firebase modules OK"));
            }
            else
            {
                // No Firebase at all: an absence notice, not a pass - nothing was verified here. There is
                // deliberately no "required in Full mode" Warning branch: the Required SDKs check owns that
                // failure, and this row's own gate resolves NotApplicable when the suite is absent, so a
                // warning produced here would render nowhere at all.
                results.Add(Skipped(ReadinessChecks.FirebaseCoherence, "Firebase not installed"));
            }
        }

        /// <summary>
        ///     Check the active platform's Firebase config file: not just present, but carrying the matching
        ///     application id (a copied wrong-game google-services.json / GoogleService-Info.plist is a
        ///     silent-data-corruption source Firebase cannot report at runtime). Honest limit: the bundle-id
        ///     match cannot prove Firebase PROJECT identity (see <see cref="FirebaseConfigMatch"/>).
        ///
        ///     Only the ACTIVE build target is judged (2026-07-23). Each platform keeps its own check category
        ///     and gate - that split (2026-07-22) is what lets one platform's file be judged without the other
        ///     hiding it - but the gate for the platform this build is not for resolves NotApplicable in the
        ///     catalog, so no observation may be produced for it: a report judges the platform in front of it.
        ///     The previous shape graded the other platform too, demoted to a Warning; that warning could never
        ///     be cleared by a game deliberately shipping one platform, so it permanently blocked a green
        ///     verdict. The other platform's row still prints in the copied report as NotApplicable, and its
        ///     checks return in full the moment the build target switches.
        /// </summary>
        static void CheckFirebaseConfigFiles(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            ReadinessCheck gate = FirebaseConfigGate();
            if (gate == null)
                return;

            // This producer OBSERVES; it never asks whether its row is gradable here. The catalog gate owns
            // that alone, and a finding it does not apply to is discarded by the evaluator - which is also
            // the only thing the pre-build block reads, so a discarded finding cannot fail a build.
            results.Add(!HasAnyFirebaseModule(dependencies)
                ? Skipped(gate, "Firebase not installed, config check skipped")
                : gate == ReadinessChecks.FirebaseConfigAndroid
                    ? CheckAndroidConfig()
                    : CheckIosConfig());
        }

        /// <summary>
        ///     The active build target's Firebase config gate, or null off-mobile where neither applies.
        ///     A report judges the platform in front of it: the other platform's gate resolves NotApplicable
        ///     in the catalog, so no observation may be produced against it.
        /// </summary>
        internal static ReadinessCheck FirebaseConfigGate() =>
            EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.Android => ReadinessChecks.FirebaseConfigAndroid,
                BuildTarget.iOS => ReadinessChecks.FirebaseConfigIos,
                _ => null,
            };

        static readonly SdkId[] s_firebaseModules =
        {
            SdkId.FirebaseApp,
            SdkId.FirebaseAnalytics,
            SdkId.FirebaseCrashlytics,
            SdkId.FirebaseRemoteConfig,
        };

        /// <summary>
        ///     The config file is read by every Firebase module, not just Analytics. Keying applicability on
        ///     Analytics alone skipped the config check for a project running Crashlytics or Remote Config
        ///     without Analytics, so a wrong-game google-services.json passed unseen (4.0.1 trust patch).
        /// </summary>
        internal static bool HasAnyFirebaseModule(Dictionary<string, object> dependencies) =>
            s_firebaseModules.Any(id => dependencies.ContainsKey(SdkRegistry.All[id].PackageId));

        static ValidationResult CheckAndroidConfig()
        {
            ReadinessCheck category = ReadinessChecks.FirebaseConfigAndroid;
            List<string> candidates = SdkConfigDetector.FirebaseAndroidConfigPaths();
            if (candidates.Count == 0)
                return MissingConfig(category,
                    "Assets/google-services.json not found.\n" +
                    "  Firebase Android (Analytics/Crashlytics/Remote Config) will fail to initialize on this platform.",
                    "Download from Firebase Console > Project Settings > Android app and place in Assets/");
            if (candidates.Count > 1)
                return Unverifiable(category,
                    $"Multiple google-services.json files found - cannot determine which one ships:\n  {string.Join("\n  ", candidates)}",
                    "Keep exactly one google-services.json (in Assets/) so the active app's config is unambiguous.");

            if (!TryReadFile(candidates[0], out string json, out ValidationResult readError, category))
                return readError;

            string appId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            switch (FirebaseConfigMatch.MatchAndroid(json, appId, out IReadOnlyCollection<string> found))
            {
                case FirebaseConfigMatchResult.Match:
                    return Valid(category, $"google-services.json matches the Android application id ({appId}).");
                case FirebaseConfigMatchResult.Mismatch:
                    return Error(category,
                        "google-services.json is for a different app (wrong google-services.json copied in?).\n" +
                        $"  Android application id: {appId}\n" +
                        $"  Config package name(s): {(found.Count == 0 ? "(none found)" : string.Join(", ", found))}",
                        "Download the google-services.json for THIS app from Firebase Console > Project Settings, or correct the Android application id.");
                default:
                    return Unverifiable(category,
                        $"{candidates[0]} could not be parsed as a google-services.json.",
                        "Re-download the file from Firebase Console; it may be truncated or the wrong file.");
            }
        }

        static ValidationResult CheckIosConfig()
        {
            ReadinessCheck category = ReadinessChecks.FirebaseConfigIos;
            List<string> candidates = SdkConfigDetector.FirebaseIosConfigPaths();
            if (candidates.Count == 0)
                return MissingConfig(category,
                    "GoogleService-Info.plist not found.\n" +
                    "  Firebase iOS (Analytics/Crashlytics/Remote Config) will fail to initialize on this platform.",
                    "Download from Firebase Console > Project Settings > iOS app and place in Assets/");
            if (candidates.Count > 1)
                return Unverifiable(category,
                    $"Multiple GoogleService-Info.plist files found - cannot determine which one ships:\n  {string.Join("\n  ", candidates)}",
                    "Keep exactly one GoogleService-Info.plist so the active app's config is unambiguous.");

            if (!TryReadFile(candidates[0], out string plist, out ValidationResult readError, category))
                return readError;

            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            switch (FirebaseConfigMatch.MatchIos(plist, bundleId, out string found))
            {
                case FirebaseConfigMatchResult.Match:
                    return Valid(category, $"GoogleService-Info.plist matches the iOS bundle id ({bundleId}).");
                case FirebaseConfigMatchResult.Mismatch:
                    return Error(category,
                        "GoogleService-Info.plist is for a different app (wrong GoogleService-Info.plist copied in?).\n" +
                        $"  iOS bundle id: {bundleId}\n" +
                        $"  Config BUNDLE_ID: {found ?? "(none found)"}",
                        "Download the GoogleService-Info.plist for THIS app from Firebase Console > Project Settings, or correct the iOS bundle id.");
                default:
                    return Unverifiable(category,
                        $"{candidates[0]} could not be parsed as a GoogleService-Info.plist.",
                        "Re-download the file from Firebase Console; it may be truncated or the wrong file.");
            }
        }

        static bool TryReadFile(string path, out string contents, out ValidationResult error, ReadinessCheck category)
        {
            error = null;
            try
            {
                contents = File.ReadAllText(path);
                return true;
            }
            catch (Exception e)
            {
                contents = null;
                error = Unverifiable(category, $"Could not read {path}: {e.Message}",
                    "Confirm the file is readable, then re-run validation.");
                return false;
            }
        }

        /// <summary>
        ///     A missing config file breaks Firebase initialization, so it is an Error in Full mode where
        ///     Firebase ships, and a warning in Prototype where it is optional. Whether the row is graded at
        ///     all is not this producer's question - the catalog gate owns it, and the evaluated model is
        ///     what the build block reads. (A config file belonging to a DIFFERENT app is a proven defect in
        ///     either mode, so those sites call Error directly.)
        /// </summary>
        static ValidationResult MissingConfig(ReadinessCheck category, string message, string fix) =>
            SorollaSettings.IsPrototype
                ? Warning(category, message, fix)
                : Error(category, message, fix);
    }
}
