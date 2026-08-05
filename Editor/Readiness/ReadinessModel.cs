using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor
{
    internal enum ReadinessOutcome
    {
        Incomplete,
        Fail,
        Warn,
        Pass,
    }

    internal enum ReadinessRequirement
    {
        Unknown,
        NotApplicable,
        Optional,
        Required,
    }

    internal enum ReadinessDisposition
    {
        Evaluated,
        Omitted,
        OptionalSkipped,
        NotApplicable,
    }

    internal enum ReadinessPlatform
    {
        Unknown,
        Android,
        iOS,
    }

    internal enum ReadinessGroup
    {
        GameAnalytics,
        Facebook,
        Firebase,
        AppLovinMax,
        Adjust,
        BuildAndProject,
    }

    internal readonly struct ReadinessRequirementDecision
    {
        internal readonly ReadinessRequirement Value;
        internal readonly string Reason;

        internal ReadinessRequirementDecision(ReadinessRequirement value, string reason)
        {
            Value = value;
            Reason = reason;
        }
    }

    internal sealed class ReadinessContext
    {
        internal EvalMode Mode;
        internal ReadinessPlatform Platform;
        internal SdkModule InstalledModules;
        internal bool ModulesResolved = true;
    }

    internal sealed class ReadinessCheck
    {
        internal readonly string Id;
        internal readonly string Label;
        internal readonly ReadinessGroup Group;
        internal readonly bool ReleaseOnly;
        internal readonly SdkModule Capability;
        internal readonly Func<ReadinessContext, ReadinessRequirementDecision> Requirement;

        internal ReadinessCheck(
            string id,
            string label,
            ReadinessGroup group,
            Func<ReadinessContext, ReadinessRequirementDecision> requirement,
            SdkModule capability = SdkModule.None,
            bool releaseOnly = false)
        {
            Id = id;
            Label = label;
            Group = group;
            Requirement = requirement;
            Capability = capability;
            ReleaseOnly = releaseOnly;
        }
    }

    /// <summary>
    ///     ONE thing a check observed, with the action that resolves it. A check that observed five
    ///     problems produces five of these, so the studio never gets four diagnoses collapsed into one
    ///     line with a fix that only answers the fifth.
    /// </summary>
    internal sealed class ReadinessFinding
    {
        internal readonly ReadinessOutcome Outcome;
        /// <summary>What was observed, in full. Never truncated to a first line: the lines after the
        /// first are evidence, not decoration.</summary>
        internal readonly string Evidence;
        /// <summary>The action paired with THIS finding. On a passing finding it is a caveat naming what
        /// the pass did not establish, not homework.</summary>
        internal readonly string Fix;
        /// <summary>The producer declined to verify (Skipped) somewhere nothing was required - a neutral
        /// notice, never an affirmative pass.</summary>
        internal readonly bool Informational;

        internal ReadinessFinding(ReadinessOutcome outcome, string evidence, string fix, bool informational)
        {
            Outcome = outcome;
            Evidence = evidence;
            Fix = fix;
            Informational = informational;
        }
    }

    /// <summary>
    ///     One check's stable identity, holding every finding that check produced, in the order produced.
    ///     Outcome is DERIVED from the findings rather than stored, so a row can never disagree with the
    ///     evidence rendered under it.
    /// </summary>
    internal sealed class ReadinessRow
    {
        internal ReadinessCheck Check;
        internal ReadinessRequirement Requirement;
        internal string RequirementReason;
        internal ReadinessDisposition Disposition;
        internal IReadOnlyList<ReadinessFinding> Findings = Array.Empty<ReadinessFinding>();

        /// <summary>The worst finding. A row with NO findings is an inert row (not applicable here, or an
        /// optional check nothing reported on): it never voted, which is why the renderers label it
        /// explicitly instead of printing this default.</summary>
        internal ReadinessOutcome Outcome => Findings.Count == 0
            ? ReadinessOutcome.Pass
            : WorstOf(Findings.Select(f => f.Outcome));

        internal bool Informational => Findings.Count > 0 && Findings.All(f => f.Informational);

        /// <summary>The ONE severity order in the readiness model: a proven failure outranks an unproven
        /// one, which outranks a caveat, which outranks a pass.</summary>
        internal static ReadinessOutcome WorstOf(IEnumerable<ReadinessOutcome> outcomes)
        {
            ReadinessOutcome worst = ReadinessOutcome.Pass;
            foreach (ReadinessOutcome outcome in outcomes)
            {
                if (outcome == ReadinessOutcome.Fail) return ReadinessOutcome.Fail;
                if (outcome == ReadinessOutcome.Incomplete) worst = ReadinessOutcome.Incomplete;
                else if (outcome == ReadinessOutcome.Warn && worst == ReadinessOutcome.Pass)
                    worst = ReadinessOutcome.Warn;
            }
            return worst;
        }
    }

    internal sealed class ReadinessReport
    {
        internal IReadOnlyList<ReadinessRow> Rows = Array.Empty<ReadinessRow>();
        internal IReadOnlyList<string> IntegrityErrors = Array.Empty<string>();
        internal ReadinessOutcome Outcome;
        internal ReadinessContext Context;
        internal Greenlight.GreenlightReportExport.Fingerprint Fingerprint;

        /// <summary>
        ///     The rows this report GRADES as failures - the launch blockers. Builds are never withheld
        ///     over them (<see cref="BuildValidatorPreprocessor" /> logs them and lets the build continue,
        ///     because required facts like store ids and vendor platform registration can only exist after
        ///     a first build reaches the stores). Reads the evaluated model rather than raw producer
        ///     results on purpose - an observation the report discards (a gate that does not apply here)
        ///     never fails with nothing on screen to explain it, and an unproven result (Incomplete) never
        ///     fails at all.
        /// </summary>
        internal IReadOnlyList<ReadinessRow> FailingRows =>
            Rows.Where(r => r.Disposition == ReadinessDisposition.Evaluated &&
                            r.Outcome == ReadinessOutcome.Fail).ToList();

        internal int FailCount => FailingRows.Count;
        internal int WarnCount => Rows.Count(r => r.Disposition == ReadinessDisposition.Evaluated &&
                                                  r.Outcome == ReadinessOutcome.Warn);
        internal int WaitCount => IntegrityErrors.Count +
                                  Rows.Count(r => r.Outcome == ReadinessOutcome.Incomplete);
        /// <summary>Evaluated rows that actually passed. A Skipped observation is NOT counted here: where
        /// the check was required it grades Incomplete (and lands in <see cref="WaitCount" />), and where it
        /// was not required the row's own Pass outcome already covers it.</summary>
        internal int PassCount => Rows.Count(r => r.Disposition == ReadinessDisposition.Evaluated &&
                                                  r.Outcome == ReadinessOutcome.Pass);
    }
}
