using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     The AUDITABLE canonical report export. The Editor greenlight's flattened display rows
    ///     hide inert rows; this exporter renders every readiness row, including NotApplicable and
    ///     OptionalSkipped, with
    ///     its stable id, definition version, requirement + reason, disposition, outcome, evidence, and fix -
    ///     plus a build/context fingerprint so a pasted result can be tied to the
    ///     exact game, build, mode, platform, phase, and SDK COMMIT that produced it. One readable text
    ///     rendering, clipboard as the transport: one report beats two that can disagree.
    /// </summary>
    static class GreenlightReportExport
    {
        internal const string Schema = "sorolla.greenlight-report/1";

        /// <summary>Identity + context a report was produced under, so a copied result is never ambiguous about
        /// which build it describes. Captured at evaluation time.</summary>
        internal readonly struct Fingerprint
        {
            public readonly string SdkVersion;
            public readonly string SdkCommit;
            public readonly string ApplicationId;
            public readonly string Platform;
            public readonly string Mode;
            public readonly string AppVersion;
            public readonly string GeneratedAtUtc;

            Fingerprint(string sdk, string sdkCommit, string appId, string platform, string mode, string appVersion,
                string generatedAtUtc)
            {
                SdkVersion = sdk; SdkCommit = sdkCommit; ApplicationId = appId; Platform = platform; Mode = mode;
                AppVersion = appVersion;
                GeneratedAtUtc = generatedAtUtc;
            }

            internal static Fingerprint Capture() =>
                new Fingerprint(
                    Palette.SdkVersion,
                    SdkProvenance.ResolveSdkCommit(),
                    Application.identifier,
                    PlatformName(EditorUserBuildSettings.activeBuildTarget),
                    ModeName(SorollaSettings.Mode),
                    Application.version,
                    DateTime.UtcNow.ToString("o"));

            static string PlatformName(BuildTarget target) => target switch
            {
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "IPhonePlayer",
                _ => target.ToString(),
            };

            static string ModeName(SorollaMode mode) => mode switch
            {
                SorollaMode.Full => "full",
                SorollaMode.Prototype => "prototype",
                _ => "unknown",
            };
        }

        /// <summary>Human-readable rendering of the same canonical report - includes disposition + requirement
        /// so an inert row is not mistaken for an evaluated PASS.</summary>
        internal static string ToText(ReadinessReport report)
        {
            Fingerprint fingerprint = report?.Fingerprint ?? Fingerprint.Capture();
            var sb = new StringBuilder();
            sb.AppendLine($"Palette Greenlight Report ({Schema})");
            sb.AppendLine($"integration: {OutcomeLabel(report?.Outcome ?? ReadinessOutcome.Incomplete)}");
            sb.AppendLine($"sdk: {fingerprint.SdkVersion} (commit {fingerprint.SdkCommit}) | " +
                          $"app: {fingerprint.ApplicationId} {fingerprint.AppVersion} | " +
                          $"platform: {fingerprint.Platform} | mode: {fingerprint.Mode}");
            sb.AppendLine($"generated: {fingerprint.GeneratedAtUtc}");
            sb.AppendLine();

            foreach (ReadinessRow row in report?.Rows ?? Array.Empty<ReadinessRow>())
            {
                // Never print an affirmative [Pass] for a result that was not evaluated evidence. Two cases:
                // a deliberate skip/absence, and a gate that does not
                // apply to the platform this report judged - the latter carries the default Pass
                // outcome because it never voted, which is exactly why it must not read as one.
                string outcomeLabel =
                    row.Disposition == ReadinessDisposition.NotApplicable ? "NotApplicable"
                    : row.Informational ? "Skipped"
                    : OutcomeLabel(row.Outcome);
                sb.AppendLine($"[{outcomeLabel}] {row.Check.Id} " +
                              $"req={row.Requirement} disp={row.Disposition}");
                if (!string.IsNullOrEmpty(row.RequirementReason))
                    sb.AppendLine($"    reason: {row.RequirementReason}");
                if (!string.IsNullOrEmpty(row.Evidence))
                    sb.AppendLine($"    evidence: {row.Evidence}");
                if (!string.IsNullOrEmpty(row.Fix))
                    sb.AppendLine($"    fix: {row.Fix}");
            }

            foreach (string error in report?.IntegrityErrors ?? Array.Empty<string>())
                sb.AppendLine($"[INTEGRITY] {error}");

            return sb.ToString();
        }

        static string OutcomeLabel(ReadinessOutcome outcome) => outcome switch
        {
            ReadinessOutcome.Fail => "Fail",
            ReadinessOutcome.Incomplete => "Incomplete",
            ReadinessOutcome.Warn => "PassWithCaveats",
            ReadinessOutcome.Pass => "Pass",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled outcome."),
        };
    }
}
