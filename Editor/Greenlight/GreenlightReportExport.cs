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
    ///     its stable id, requirement + reason, disposition, outcome, and EVERY finding that row retained
    ///     with the action paired to it -
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
                // Never print an affirmative [Pass] for a result that was not evaluated evidence. Three
                // cases: a gate that does not apply to the platform/mode this report judged, an optional
                // gate nothing reported on, and a deliberate producer skip. The first two carry the model's
                // default Pass outcome precisely because they never voted, which is exactly why they must
                // not read as one.
                string outcomeLabel =
                    row.Disposition == ReadinessDisposition.NotApplicable ? "NotApplicable"
                    : row.Disposition == ReadinessDisposition.OptionalSkipped ? "OptionalSkipped"
                    : row.Informational ? "Skipped"
                    : OutcomeLabel(row.Outcome);
                sb.AppendLine($"[{outcomeLabel}] {row.Check.Id} " +
                              $"req={row.Requirement} disp={row.Disposition}");
                if (!string.IsNullOrEmpty(row.RequirementReason))
                    sb.AppendLine($"    reason: {row.RequirementReason}");

                // EVERY finding, each with its own severity and its own action. A check that observed five
                // problems prints five, because the window renders five: same model, two renderings.
                foreach (ReadinessFinding finding in row.Findings)
                {
                    AppendBlock(sb, $"[{FindingLabel(finding)}] evidence", finding.Evidence);
                    // A passing finding's action is a caveat naming what the pass did NOT establish, so it
                    // is not labelled as a fix a studio still owes. When the evidence IS the instruction,
                    // both slots hold the same text - print it once.
                    if (finding.Fix != finding.Evidence)
                        AppendBlock(sb, finding.Outcome == ReadinessOutcome.Pass ? "note" : "fix", finding.Fix);
                }
            }

            foreach (string error in report?.IntegrityErrors ?? Array.Empty<string>())
                sb.AppendLine($"[INTEGRITY] {error}");

            return sb.ToString();
        }

        /// <summary>A deliberate skip prints as "Skipped", never as an affirmative Pass, at the finding level
        /// too - the same rule the row line follows.</summary>
        static string FindingLabel(ReadinessFinding finding) =>
            finding.Informational ? "Skipped" : OutcomeLabel(finding.Outcome);

        /// <summary>One labelled block, with continuation lines indented under it - a multi-line diagnosis
        /// stays one readable unit instead of being truncated to its first line.</summary>
        static void AppendBlock(StringBuilder sb, string label, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Split('\n');
            sb.AppendLine($"    {label}: {lines[0].TrimEnd()}");
            for (var i = 1; i < lines.Length; i++)
                sb.AppendLine($"      {lines[i].Trim()}");
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
