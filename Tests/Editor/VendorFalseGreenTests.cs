using System.Collections.Generic;
using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Regressions for the 4.0.1 trust patch: each of these configurations used to read as a
    ///     non-failure while the integration was provably broken.
    /// </summary>
    [TestFixture]
    public class VendorFalseGreenTests
    {
        static BuildValidator.ValidationResult GradeProbe(string body, string platformName = "Android")
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, body, "123456", platformName, 0);
            return BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail);
        }

        [Test]
        public void Facebook_MissingCredentials_IsError()
        {
            BuildValidator.ValidationResult result =
                BuildValidator.GradeFacebookPlatform(false, default, null);

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            Assert.That(result.Fix, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Facebook_SuccessWithoutSupportedPlatformsField_IsPlatformMissingError()
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, "{\"id\":\"123456\"}", "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.PlatformMissing, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Error,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        /// <summary>
        ///     Field absence only means "zero platforms" on a body proven to be THIS app's object. These
        ///     three 200s are not that, and grading them as zero-platform would block a build on a fact
        ///     never observed.
        /// </summary>
        [TestCase("{\"error\":{\"message\":\"Invalid OAuth access token\",\"code\":190}}", TestName = "GraphErrorWrappedIn200")]
        [TestCase("{\"status\":\"ok\"}", TestName = "CaptivePortalBody")]
        [TestCase("{\"id\":\"999999\"}", TestName = "DifferentAppObject")]
        public void Facebook_SuccessBodyThatIsNotThisAppObject_StaysIncomplete(string body)
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, body, "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Unreachable, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Unverifiable,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        [Test]
        public void Facebook_StaleProbeForAnotherAppOrTarget_IsIncompleteNotAFailure()
        {
            BuildValidator.ValidationResult result = BuildValidator.GradeFacebookPlatform(
                true, FacebookPlatformValidator.ProbeState.PlatformMissing, "stale detail", probeIsCurrent: false);

            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, result.Status);
            Assert.That(result.Message, Does.Not.Contain("stale detail"));
        }

        [Test]
        public void Facebook_SuccessWithEmptySupportedPlatforms_IsPlatformMissingError()
        {
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Error,
                GradeProbe("{\"id\":\"123456\",\"supported_platforms\":[]}").Status);
        }

        [Test]
        public void Facebook_ActivePlatformRegistered_PassesWithoutGradingTheOtherPlatform()
        {
            BuildValidator.ValidationResult android =
                GradeProbe("{\"supported_platforms\":[\"ANDROID\"]}");
            BuildValidator.ValidationResult ios =
                GradeProbe("{\"supported_platforms\":[\"ANDROID\"]}", "iOS");

            Assert.AreEqual(BuildValidator.ValidationStatus.Valid, android.Status);
            Assert.That(android.Message, Does.Contain("iOS not registered"));
            Assert.AreEqual(BuildValidator.ValidationStatus.Error, ios.Status);
        }

        [Test]
        public void Facebook_UnparseableBody_GradesIncompleteNotAFailure()
        {
            FacebookPlatformValidator.ProbeResult probe = FacebookPlatformValidator.EvaluateResponse(
                false, 200, "<html>proxy interception</html>", "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Unreachable, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Unverifiable,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        [Test]
        public void Facebook_TransportFailure_IsUnverifiableWithARetryAction()
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(true, 0, null, "123456", "Android", 0);
            BuildValidator.ValidationResult result =
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail);

            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, result.Status);
            Assert.That(result.Fix, Does.Contain("graph.facebook.com"));
        }

        [Test]
        public void Max_MissingActivePlatformAdMobAppId_IsErrorNamingTheField()
        {
            BuildValidator.ValidationResult result = BuildValidator.GradeMaxAdMobAppId("", "Android");

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            Assert.That(result.Fix, Does.Contain("AdMob App ID"));
            Assert.That(result.Fix, Does.Contain("Android"));
        }

        [Test]
        public void Max_PresentAdMobAppId_ProducesNoFinding()
        {
            Assert.IsNull(BuildValidator.GradeMaxAdMobAppId("ca-app-pub-123~456", "Android"));
        }

        /// <summary>
        ///     Null is an unread property (AppLovin version drift), not an empty field: it must not block a
        ///     build by claiming the studio left the id blank.
        /// </summary>
        [Test]
        public void Max_UnreadableAdMobAppId_IsIncompleteNamingTheFailedRead()
        {
            BuildValidator.ValidationResult result = BuildValidator.GradeMaxAdMobAppId(null, "Android");

            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, result.Status);
            Assert.That(result.Message, Does.Contain("Could not read"));
            Assert.That(result.Message, Does.Not.Contain("is empty"));
        }

        [Test]
        public void Firebase_ConfigCheckApplies_ToAnyInstalledFirebaseModule()
        {
            var crashlyticsOnly = new Dictionary<string, object>
            {
                { SdkRegistry.All[SdkId.FirebaseApp].PackageId, "12.0.0" },
                { SdkRegistry.All[SdkId.FirebaseCrashlytics].PackageId, "12.0.0" },
            };

            Assert.IsTrue(BuildValidator.HasAnyFirebaseModule(crashlyticsOnly));
            Assert.IsFalse(BuildValidator.HasAnyFirebaseModule(new Dictionary<string, object>()));
        }

        /// <summary>
        ///     Asserts the extracted pre-build predicate itself. That BuildValidatorPreprocessor consumes
        ///     exactly this predicate is hand-verified (its one-line call site), not asserted here:
        ///     OnPreprocessBuild needs a BuildReport and live project state. Accepted limitation.
        /// </summary>
        [Test]
        public void CachedVendorErrors_Block_WhileUnreachableProbesDoNot()
        {
            FacebookPlatformValidator.ProbeResult unreachable =
                FacebookPlatformValidator.EvaluateResponse(true, 0, null, "123456", "Android", 0);

            var results = new List<BuildValidator.ValidationResult>
            {
                BuildValidator.GradeFacebookPlatform(false, default, null),
                BuildValidator.GradeMaxAdMobAppId("", "Android"),
                BuildValidator.GradeFacebookPlatform(true, unreachable.State, unreachable.Detail),
            };

            List<BuildValidator.ValidationResult> blocking = BuildValidator.BlockingErrors(results);

            Assert.AreEqual(2, blocking.Count);
            Assert.IsTrue(blocking.TrueForAll(r => r.Status == BuildValidator.ValidationStatus.Error));
        }
    }
}
