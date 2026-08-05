using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Auto-run validation before builds.
    /// </summary>
    public class BuildValidatorPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport buildReport)
        {
            Debug.Log("[Palette BuildValidator] Running pre-build validation...");

            // Only synchronous repairs are safe here. Package resolution belongs to the editor workflow;
            // missing packages remain visible build errors instead of starting asynchronous UPM work mid-build.
            var fixes = BuildValidator.RunSafeAutoFixes();
            foreach (string fix in fixes)
                Debug.Log($"[Palette BuildValidator] Auto-fix: {fix}");

            // Failures are logged loudly but NEVER stop the build (2026-08-05, Arthur). A red required row
            // is a LAUNCH blocker, not a build blocker: several required facts (Facebook platform
            // registration, store ids, AdMob app registration) can only exist after a first build reaches
            // the stores, so withholding the binary deadlocks a first release. The binary is also identical
            // whether a vendor console is configured or not - blocking it fixes nothing. The report reads
            // the EVALUATED model so a discarded producer result can never surface here invisibly.
            ReadinessReport readiness = Greenlight.GreenlightEvaluator.Evaluate(BuildValidator.RunAllChecks());
            IReadOnlyList<ReadinessRow> failing = readiness.FailingRows;

            if (failing.Count > 0)
            {
                foreach (ReadinessRow row in failing)
                foreach (ReadinessFinding finding in row.Findings)
                    if (finding.Outcome == ReadinessOutcome.Fail)
                        Debug.LogError($"[Palette BuildValidator] ERROR: {row.Check.Id}: {finding.Evidence}\n" +
                                       $"  Fix: {finding.Fix}");

                Debug.LogError(
                    $"[Palette BuildValidator] Launch readiness FAILED with {failing.Count} failing check(s). " +
                    "The build continues so it can reach stores and internal QA, but do NOT launch " +
                    "campaigns on it. Open Tools > Sorolla Palette SDK for details.");
            }

            // Release-readiness warnings (no release keystore, Adjust still in sandbox) are about store
            // submission, and a development build is by definition not that - warning on every dev/QA build
            // is the noise that trains people to ignore the log. The build tells us which kind it is, so
            // nothing has to be configured or remembered (2026-07-22, replacing the deleted profile knob).
            bool releaseBuild = (buildReport.summary.options & BuildOptions.Development) == 0;

            // Per FINDING, not per row outcome: a row whose worst finding is Incomplete can still carry a
            // real warning underneath it, and filtering on the row's outcome silently dropped it from the
            // build log. Every evaluated row is searched.
            var warnings = readiness.Rows
                .Where(r => r.Disposition == ReadinessDisposition.Evaluated)
                .Where(r => releaseBuild || !r.Check.ReleaseOnly)
                .SelectMany(r => r.Findings
                    .Where(f => f.Outcome == ReadinessOutcome.Warn)
                    .Select(f => $"{r.Check.Id}: {f.Evidence}"))
                .ToList();
            foreach (string warning in warnings)
                Debug.LogWarning($"[Palette BuildValidator] WARNING: {warning}");

            if (warnings.Count > 0)
                Debug.Log($"[Palette BuildValidator] Pre-build validation passed with {warnings.Count} warning(s)");
            else
                Debug.Log("[Palette BuildValidator] Pre-build validation passed");
        }
    }
}
