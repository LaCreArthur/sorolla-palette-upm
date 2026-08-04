using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Editor
{
    internal static class ReadinessEvaluator
    {
        internal static ReadinessReport Evaluate(
            ReadinessContext context,
            IReadOnlyList<BuildValidator.ValidationResult> observations)
        {
            var errors = new List<string>();
            var rows = new List<ReadinessRow>(ReadinessChecks.All.Count);
            if (context == null)
            {
                errors.Add("Readiness context is unavailable.");
                context = new ReadinessContext { ModulesResolved = false };
            }
            if (!context.ModulesResolved)
                errors.Add("Installed package state could not be read from Packages/manifest.json.");

            var byCheck = (observations ?? Array.Empty<BuildValidator.ValidationResult>())
                .Where(r => r?.Check != null)
                .GroupBy(r => r.Check)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (ReadinessCheck check in ReadinessChecks.All)
            {
                ReadinessRequirementDecision decision = check.Requirement(context);
                byCheck.TryGetValue(check, out List<BuildValidator.ValidationResult> matches);
                bool nothingObserved = matches == null || matches.Count == 0;
                var row = new ReadinessRow
                {
                    Check = check,
                    Requirement = decision.Value,
                    RequirementReason = decision.Reason,
                };

                switch (decision.Value)
                {
                    // Inert rows keep no findings at all: the gate does not apply here, or nothing optional
                    // reported. They carry the model's default Pass outcome because they never voted, so
                    // BOTH renderers are required to key off the disposition, not that outcome - see
                    // CheckRow.StatusFor and GreenlightReportExport.ToText.
                    case ReadinessRequirement.NotApplicable:
                        row.Disposition = ReadinessDisposition.NotApplicable;
                        break;
                    case ReadinessRequirement.Optional when nothingObserved:
                        row.Disposition = ReadinessDisposition.OptionalSkipped;
                        break;
                    case ReadinessRequirement.Required when nothingObserved:
                        row.Disposition = ReadinessDisposition.Omitted;
                        row.Findings = new[] { NoObservation(check) };
                        break;
                    default:
                        row.Disposition = ReadinessDisposition.Evaluated;
                        // EVERY observation is retained, in the order the producer emitted it - the row's
                        // outcome derives from the worst of them, and each keeps its own fix.
                        row.Findings = nothingObserved
                            ? new[] { NoObservation(check) }
                            : matches.Select(m => ToFinding(m, decision.Value)).ToArray();
                        break;
                }
                rows.Add(row);
            }

            foreach (BuildValidator.ValidationResult result in observations ?? Array.Empty<BuildValidator.ValidationResult>())
                if (result?.Check != null && !ReadinessChecks.All.Contains(result.Check))
                    errors.Add($"Unknown readiness check '{result.Check.Id}'.");

            return new ReadinessReport
            {
                Rows = rows,
                IntegrityErrors = errors,
                Outcome = Aggregate(rows, errors.Count > 0),
                Context = context,
                Fingerprint = Greenlight.GreenlightReportExport.Fingerprint.Capture(),
            };
        }

        /// <summary>What a studio is told when a check the catalog requires produced nothing to grade -
        /// including a check that threw before it could report. The residue is stated ("nothing was
        /// verified"), and the action is bounded: one retry, then escalate, never an open refresh loop.</summary>
        internal const string NoResultAction =
            "Click Refresh once. If the row still has no result, use Copy Report and send it to Sorolla.";

        static ReadinessFinding NoObservation(ReadinessCheck check) => new ReadinessFinding(
            ReadinessOutcome.Incomplete,
            $"No result was produced for the {check.Label} check, so nothing was verified here.",
            NoResultAction,
            informational: false);

        static ReadinessFinding ToFinding(
            BuildValidator.ValidationResult result, ReadinessRequirement requirement)
        {
            ReadinessOutcome outcome = ToOutcome(result.Status, requirement);
            // A producer skip on a REQUIRED row is not a pass - but the skip message itself already names
            // what the studio must do ("Select Android or iOS to check GameAnalytics credentials"), so that
            // message IS the action. NoResultAction stays reserved for a genuinely absent observation: a
            // state the studio caused must never tell them to send a report to Sorolla.
            string fix = string.IsNullOrEmpty(result.Fix) &&
                         result.Status == BuildValidator.ValidationStatus.Skipped &&
                         outcome == ReadinessOutcome.Incomplete
                ? result.Message
                : result.Fix;
            return new ReadinessFinding(outcome, result.Message, fix,
                informational: result.Status == BuildValidator.ValidationStatus.Skipped &&
                               outcome == ReadinessOutcome.Pass);
        }

        /// <summary>
        ///     Producer status graded against what the catalog asked of this row.
        ///     Skipped is an ABSENCE of verdict, not a pass: where the row is Required it is exactly the
        ///     "required check produced no observation" case, so it grades Incomplete rather than counting
        ///     toward green. Where the requirement itself is Unknown (no config, unreadable manifest) only a
        ///     proven Error survives as a failure; nothing else can be trusted as a pass.
        /// </summary>
        static ReadinessOutcome ToOutcome(
            BuildValidator.ValidationStatus status, ReadinessRequirement requirement)
        {
            if (requirement == ReadinessRequirement.Unknown)
                return status == BuildValidator.ValidationStatus.Error
                    ? ReadinessOutcome.Fail
                    : ReadinessOutcome.Incomplete;

            if (status == BuildValidator.ValidationStatus.Skipped)
                return requirement == ReadinessRequirement.Required
                    ? ReadinessOutcome.Incomplete
                    : ReadinessOutcome.Pass;

            return status switch
            {
                BuildValidator.ValidationStatus.Error => ReadinessOutcome.Fail,
                BuildValidator.ValidationStatus.Warning => ReadinessOutcome.Warn,
                BuildValidator.ValidationStatus.Unverifiable => ReadinessOutcome.Incomplete,
                BuildValidator.ValidationStatus.Valid => ReadinessOutcome.Pass,
                _ => ReadinessOutcome.Incomplete,
            };
        }

        static ReadinessOutcome Aggregate(IReadOnlyList<ReadinessRow> rows, bool integrityError)
        {
            var considered = rows.Where(r =>
                r.Disposition != ReadinessDisposition.NotApplicable &&
                r.Disposition != ReadinessDisposition.OptionalSkipped).ToList();
            ReadinessOutcome worst = ReadinessRow.WorstOf(considered.Select(r => r.Outcome));
            if (worst == ReadinessOutcome.Fail)
                return ReadinessOutcome.Fail;
            // Nothing voted at all is not a pass either.
            return integrityError || worst == ReadinessOutcome.Incomplete || considered.Count == 0
                ? ReadinessOutcome.Incomplete
                : worst;
        }
    }
}
