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
                BuildValidator.ValidationResult observed = Worst(matches);
                var row = new ReadinessRow
                {
                    Check = check,
                    Requirement = decision.Value,
                    RequirementReason = decision.Reason,
                };

                switch (decision.Value)
                {
                    case ReadinessRequirement.NotApplicable:
                        row.Disposition = ReadinessDisposition.NotApplicable;
                        row.Outcome = ReadinessOutcome.Pass;
                        break;
                    case ReadinessRequirement.Optional when observed == null:
                        row.Disposition = ReadinessDisposition.OptionalSkipped;
                        row.Outcome = ReadinessOutcome.Pass;
                        break;
                    case ReadinessRequirement.Required when observed == null:
                        row.Disposition = ReadinessDisposition.Omitted;
                        row.Outcome = ReadinessOutcome.Incomplete;
                        break;
                    case ReadinessRequirement.Unknown:
                        row.Disposition = ReadinessDisposition.Evaluated;
                        row.Outcome = observed?.Status == BuildValidator.ValidationStatus.Error
                            ? ReadinessOutcome.Fail
                            : ReadinessOutcome.Incomplete;
                        Copy(observed, row);
                        break;
                    default:
                        row.Disposition = ReadinessDisposition.Evaluated;
                        if (observed != null)
                        {
                            row.Outcome = ToOutcome(observed.Status);
                            row.Informational = observed.Status == BuildValidator.ValidationStatus.Skipped;
                        }
                        else
                        {
                            row.Outcome = ReadinessOutcome.Incomplete;
                        }
                        Copy(observed, row);
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

        static BuildValidator.ValidationResult Worst(List<BuildValidator.ValidationResult> matches) =>
            matches?.OrderBy(result => Priority(result.Status)).FirstOrDefault();

        static int Priority(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => 0,
            BuildValidator.ValidationStatus.Unverifiable => 1,
            BuildValidator.ValidationStatus.Warning => 2,
            BuildValidator.ValidationStatus.Valid => 3,
            BuildValidator.ValidationStatus.Skipped => 4,
            _ => -1,
        };

        static void Copy(BuildValidator.ValidationResult result, ReadinessRow row)
        {
            if (result == null) return;
            row.Evidence = FirstLine(result.Message);
            row.Fix = result.Fix;
        }

        static string FirstLine(string message) =>
            string.IsNullOrEmpty(message) ? "" : message.Split('\n')[0];

        internal static ReadinessOutcome ToOutcome(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => ReadinessOutcome.Fail,
            BuildValidator.ValidationStatus.Warning => ReadinessOutcome.Warn,
            BuildValidator.ValidationStatus.Unverifiable => ReadinessOutcome.Incomplete,
            BuildValidator.ValidationStatus.Valid => ReadinessOutcome.Pass,
            BuildValidator.ValidationStatus.Skipped => ReadinessOutcome.Pass,
            _ => ReadinessOutcome.Incomplete,
        };

        static ReadinessOutcome Aggregate(IReadOnlyList<ReadinessRow> rows, bool integrityError)
        {
            var considered = rows.Where(r =>
                r.Disposition != ReadinessDisposition.NotApplicable &&
                r.Disposition != ReadinessDisposition.OptionalSkipped).ToList();
            if (considered.Any(r => r.Outcome == ReadinessOutcome.Fail))
                return ReadinessOutcome.Fail;
            if (integrityError || considered.Any(r => r.Outcome == ReadinessOutcome.Incomplete) ||
                !considered.Any(r => r.Outcome == ReadinessOutcome.Pass || r.Outcome == ReadinessOutcome.Warn))
                return ReadinessOutcome.Incomplete;
            return considered.Any(r => r.Outcome == ReadinessOutcome.Warn)
                ? ReadinessOutcome.Warn
                : ReadinessOutcome.Pass;
        }
    }
}
