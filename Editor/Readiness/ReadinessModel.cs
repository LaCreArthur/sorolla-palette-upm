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

    internal sealed class ReadinessRow
    {
        internal ReadinessCheck Check;
        internal ReadinessOutcome Outcome;
        internal ReadinessRequirement Requirement;
        internal string RequirementReason;
        internal ReadinessDisposition Disposition;
        internal string Evidence;
        internal string Fix;
        internal bool Informational;
    }

    internal sealed class ReadinessReport
    {
        internal IReadOnlyList<ReadinessRow> Rows = Array.Empty<ReadinessRow>();
        internal IReadOnlyList<string> IntegrityErrors = Array.Empty<string>();
        internal ReadinessOutcome Outcome;
        internal ReadinessContext Context;
        internal Greenlight.GreenlightReportExport.Fingerprint Fingerprint;

        internal int FailCount => Rows.Count(r => r.Disposition == ReadinessDisposition.Evaluated &&
                                                  r.Outcome == ReadinessOutcome.Fail);
        internal int WarnCount => Rows.Count(r => r.Disposition == ReadinessDisposition.Evaluated &&
                                                  r.Outcome == ReadinessOutcome.Warn);
        internal int WaitCount => IntegrityErrors.Count + Rows.Count(r =>
            r.Disposition == ReadinessDisposition.Omitted || r.Outcome == ReadinessOutcome.Incomplete);
        internal int PassCount => Rows.Count(r => r.Disposition == ReadinessDisposition.Evaluated &&
                                                  (r.Outcome == ReadinessOutcome.Pass || r.Informational));
    }
}
