using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that the Facebook app is registered for the active build target's platform and
        ///     that the appId/clientToken pair is accepted by the Graph API. Async and non-blocking:
        ///     kicks off a probe if needed and reports whatever <see cref="FacebookPlatformValidator"/>
        ///     currently has cached, never waiting on the network here.
        /// </summary>
        static List<ValidationResult> CheckFacebookPlatformConfig()
        {
            var results = new List<ValidationResult>();
            ReadinessCheck category = ReadinessChecks.FacebookPlatformConfig;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                results.Add(Skipped(category, "Select Android or iOS to check Facebook platform configuration"));
                return results;
            }

            if (!SdkDetector.IsInstalled(SdkId.Facebook))
            {
                results.Add(Skipped(category, "Facebook not installed"));
                return results;
            }

            if (!SdkConfigDetector.TryGetFacebookCredentials(out string appId, out string clientToken))
            {
                results.Add(GradeFacebookPlatform(false, default, null));
                return results;
            }

            FacebookPlatformValidator.EnsureChecked(appId, clientToken);
            FacebookPlatformValidator.ProbeResult probe = FacebookPlatformValidator.Current;

            // The cached result carries the app id and platform it was probed for. EnsureChecked starts a
            // fresh probe when either changed, but Current still holds the OLD settled result until that one
            // lands - so grading it here would judge this project by another app id's or another build
            // target's answer.
            bool probeIsCurrent = probe.AppId == appId
                && probe.PlatformName == FacebookPlatformValidator.ActivePlatformName();
            results.Add(GradeFacebookPlatform(true, probe.State, probe.Detail, probeIsCurrent));

            return results;
        }

        /// <summary>
        ///     Grades the Facebook platform row from credential presence plus the cached probe state.
        ///     Transport-free so the severities are testable.
        /// </summary>
        internal static ValidationResult GradeFacebookPlatform(
            bool hasCredentials,
            FacebookPlatformValidator.ProbeState state,
            string detail,
            bool probeIsCurrent = true)
        {
            ReadinessCheck category = ReadinessChecks.FacebookPlatformConfig;

            // Absent credentials used to skip the row, which rendered as a neutral non-failure: a game
            // shipping with no Facebook app id lost all Facebook attribution with nothing red anywhere
            // (4.0.1 trust patch). Facebook is a core capability in both modes, so this is an Error.
            if (!hasCredentials)
            {
                return Error(
                    category,
                    "Facebook app id / client token are not set in FacebookSettings.asset.\n" +
                    "  Facebook init fails; analytics and attribution never reach Facebook.",
                    "Enter the app id + client token from the Facebook developer console in FacebookSettings.asset");
            }

            if (state == FacebookPlatformValidator.ProbeState.NotStarted ||
                state == FacebookPlatformValidator.ProbeState.Pending)
            {
                return Unverifiable(category, "Checking Facebook app platform registration...");
            }

            if (!probeIsCurrent)
            {
                return Unverifiable(category,
                    "The cached Facebook result was probed for a different app id or build target.",
                    "Click Refresh to re-check this app id against the active build target");
            }

            switch (state)
            {
                case FacebookPlatformValidator.ProbeState.Unreachable:
                    return Unverifiable(category, detail,
                        "Retry from a network that can reach graph.facebook.com, then click Refresh");

                case FacebookPlatformValidator.ProbeState.PlatformMissing:
                    return Error(category, detail, "FB console -> Settings -> Basic -> Add Platform");

                case FacebookPlatformValidator.ProbeState.CredentialInvalid:
                    // Fix hint omits the "open FacebookSettings.asset" step (product-audit fix cycle
                    // residual, 2026-07-21): the row's "Open FB Settings" button already opens it.
                    return Error(category, detail,
                        "Compare the app id + client token against the Facebook developer console");

                case FacebookPlatformValidator.ProbeState.Verified:
                    return Valid(category, detail);

                default:
                    return Unverifiable(category, "Checking Facebook app platform registration...");
            }
        }
    }
}
