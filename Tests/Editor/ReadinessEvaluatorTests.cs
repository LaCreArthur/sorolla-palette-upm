using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    public class ReadinessEvaluatorTests
    {
        static ReadinessContext Context(
            EvalMode mode = EvalMode.Full,
            ReadinessPlatform platform = ReadinessPlatform.Android,
            SdkModule modules = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                SdkModule.AppLovinMax | SdkModule.Adjust) =>
            new ReadinessContext
            {
                Mode = mode,
                Platform = platform,
                InstalledModules = modules,
                ModulesResolved = true,
            };

        static BuildValidator.ValidationResult Result(
            ReadinessCheck check,
            BuildValidator.ValidationStatus status = BuildValidator.ValidationStatus.Valid) =>
            new BuildValidator.ValidationResult(status, "evidence", "fix", check);

        [Test]
        public void Catalog_HasTwentyFourUniqueStableIds()
        {
            Assert.AreEqual(24, ReadinessChecks.All.Count);
            Assert.AreEqual(24, ReadinessChecks.All.Select(check => check.Id).Distinct().Count());
        }

        [Test]
        public void RequiredMissingEvidence_IsIncomplete()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>());
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.RequiredSdks);

            Assert.AreEqual(ReadinessDisposition.Omitted, row.Disposition);
            Assert.AreEqual(ReadinessOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void FailureOutranksIncompleteAndWarning()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Error),
                Result(ReadinessChecks.VersionMismatches, BuildValidator.ValidationStatus.Warning),
            };

            Assert.AreEqual(ReadinessOutcome.Fail, ReadinessEvaluator.Evaluate(Context(), results).Outcome);
        }

        [Test]
        public void SeveralFindingsForOneCheck_UseTheWorstFinding()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks),
                Result(ReadinessChecks.VersionMismatches),
                Result(ReadinessChecks.VersionMismatches, BuildValidator.ValidationStatus.Warning),
            };

            ReadinessRow row = ReadinessEvaluator.Evaluate(Context(), results).Rows
                .Single(r => r.Check == ReadinessChecks.VersionMismatches);

            Assert.AreEqual(ReadinessOutcome.Warn, row.Outcome);
        }

        [Test]
        public void OptionalAbsentCapability_IsNotApplicable()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(EvalMode.Prototype, modules: SdkModule.GameAnalytics | SdkModule.Facebook),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            Assert.AreEqual(
                ReadinessDisposition.NotApplicable,
                report.Rows.Single(r => r.Check == ReadinessChecks.AdjustSettings).Disposition);
        }

        [Test]
        public void OppositePlatformCheck_IsNotApplicable()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(platform: ReadinessPlatform.Android),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            Assert.AreEqual(
                ReadinessDisposition.NotApplicable,
                report.Rows.Single(r => r.Check == ReadinessChecks.FirebaseConfigIos).Disposition);
        }

        [Test]
        public void GameAnalyticsResourceWhitelist_RemainsAdvisoryInPrototype()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(EvalMode.Prototype, modules: SdkModule.GameAnalytics | SdkModule.Facebook),
                new List<BuildValidator.ValidationResult>
                {
                    Result(ReadinessChecks.RequiredSdks),
                    Result(ReadinessChecks.GameAnalyticsResourceWhitelist, BuildValidator.ValidationStatus.Skipped),
                });

            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.GameAnalyticsResourceWhitelist);
            Assert.AreEqual(ReadinessRequirement.Optional, row.Requirement);
            Assert.AreEqual(ReadinessDisposition.Evaluated, row.Disposition);
            Assert.IsTrue(row.Informational);
        }

        [Test]
        public void Export_PreservesSchemaAndStableIds()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            string text = GreenlightReportExport.ToText(report);

            StringAssert.Contains("sorolla.greenlight-report/1", text);
            StringAssert.Contains("build.required_sdks", text);
            StringAssert.Contains("[NotApplicable] build.firebase_config_ios", text);
        }
    }
}
