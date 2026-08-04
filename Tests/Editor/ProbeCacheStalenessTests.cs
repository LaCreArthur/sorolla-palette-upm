using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The vendor probe caches own their own staleness. Both validators used to return early whenever
    ///     ANY request was in flight, so a configuration edited mid-probe was silently discarded and the
    ///     older request settled over it - the window then graded the new app id (or the corrected GA key
    ///     pair) by the previous one's answer, and a build blocked on it. The consumer rows used to carry a
    ///     guard for that; these tests pin the fix where the state actually lives.
    ///
    ///     Each test uses its own credential values: the cache is process-wide static state, so distinct
    ///     values are what keeps the fixtures independent of ordering.
    /// </summary>
    [TestFixture]
    public class ProbeCacheStalenessTests
    {
        [Test]
        public void Facebook_SameConfiguration_DoesNotStartASecondProbe()
        {
            Assert.AreNotEqual(FacebookPlatformValidator.NoProbeNeeded,
                FacebookPlatformValidator.BeginProbe("fb-same", "token", "Android"));
            Assert.AreEqual(FacebookPlatformValidator.NoProbeNeeded,
                FacebookPlatformValidator.BeginProbe("fb-same", "token", "Android"),
                "A probe is already in flight for this exact configuration.");
        }

        [Test]
        public void Facebook_AppIdChangedMidFlight_ProbesTheNewOneAndDropsTheOldAnswer()
        {
            int first = FacebookPlatformValidator.BeginProbe("fb-old", "token", "Android");
            int second = FacebookPlatformValidator.BeginProbe("fb-new", "token", "Android");

            Assert.AreNotEqual(FacebookPlatformValidator.NoProbeNeeded, second,
                "A configuration change mid-flight must start its own probe.");
            Assert.AreEqual("fb-new", FacebookPlatformValidator.Current.AppId);

            // The superseded request lands late with a verdict about fb-old.
            FacebookPlatformValidator.Settle(first, Probe(
                FacebookPlatformValidator.ProbeState.PlatformMissing, "fb-old", "Android", "stale detail"));

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Pending,
                FacebookPlatformValidator.Current.State, "The stale answer must not be published.");
            Assert.AreEqual("fb-new", FacebookPlatformValidator.Current.AppId);

            FacebookPlatformValidator.Settle(second, Probe(
                FacebookPlatformValidator.ProbeState.Verified, "fb-new", "Android", "fresh detail"));

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Verified,
                FacebookPlatformValidator.Current.State);
            Assert.AreEqual("fresh detail", FacebookPlatformValidator.Current.Detail);
        }

        /// <summary>
        ///     The reason the settle is keyed on a request id and not on the configuration: an app id or
        ///     token typed away and back inside the 3s timeout produces the same configuration string
        ///     twice, so a configuration-keyed settle would accept the FIRST request's slow answer as
        ///     current. That answer was probed before the round trip and is stale evidence; the newest
        ///     request is the one that speaks.
        /// </summary>
        [Test]
        public void Facebook_ConfigurationFlippedBackMidFlight_StillOnlyPublishesTheNewestRequest()
        {
            int firstA = FacebookPlatformValidator.BeginProbe("fb-flip", "token", "Android");
            FacebookPlatformValidator.BeginProbe("fb-flip", "other-token", "Android");
            int secondA = FacebookPlatformValidator.BeginProbe("fb-flip", "token", "Android");

            Assert.AreNotEqual(firstA, secondA, "Returning to an earlier configuration is a NEW request.");

            FacebookPlatformValidator.Settle(firstA, Probe(
                FacebookPlatformValidator.ProbeState.CredentialInvalid, "fb-flip", "Android", "first answer"));

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Pending,
                FacebookPlatformValidator.Current.State,
                "The first request's answer is stale even though its configuration is current again.");

            FacebookPlatformValidator.Settle(secondA, Probe(
                FacebookPlatformValidator.ProbeState.Verified, "fb-flip", "Android", "newest answer"));

            Assert.AreEqual("newest answer", FacebookPlatformValidator.Current.Detail);
        }

        /// <summary>
        ///     The build target is part of the Facebook configuration: one Graph response describes both
        ///     platforms, but only the active one is graded, so a target switch invalidates the verdict.
        /// </summary>
        [Test]
        public void Facebook_BuildTargetChangedMidFlight_ProbesAgain()
        {
            int android = FacebookPlatformValidator.BeginProbe("fb-target", "token", "Android");
            int ios = FacebookPlatformValidator.BeginProbe("fb-target", "token", "iOS");

            Assert.AreNotEqual(FacebookPlatformValidator.NoProbeNeeded, ios);
            Assert.AreEqual("iOS", FacebookPlatformValidator.Current.PlatformName);

            FacebookPlatformValidator.Settle(android, Probe(
                FacebookPlatformValidator.ProbeState.Verified, "fb-target", "Android", "android detail"));

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Pending,
                FacebookPlatformValidator.Current.State);
        }

        [Test]
        public void GameAnalytics_SameCredentials_DoNotStartASecondProbe()
        {
            Assert.AreNotEqual(GameAnalyticsCredentialValidator.NoProbeNeeded,
                GameAnalyticsCredentialValidator.BeginProbe("ga-same", "secret"));
            Assert.AreEqual(GameAnalyticsCredentialValidator.NoProbeNeeded,
                GameAnalyticsCredentialValidator.BeginProbe("ga-same", "secret"));
        }

        /// <summary>
        ///     The incident shape this prevents: a studio pastes a corrected secret key while the rejected
        ///     pair is still in flight, and the 401 for the OLD pair settles over the new one - the row keeps
        ///     saying the credentials are rejected after they were fixed.
        /// </summary>
        [Test]
        public void GameAnalytics_SecretKeyChangedMidFlight_ProbesTheNewPairAndDropsTheOldAnswer()
        {
            int rejected = GameAnalyticsCredentialValidator.BeginProbe("ga-fix", "wrong-secret");
            int corrected = GameAnalyticsCredentialValidator.BeginProbe("ga-fix", "right-secret");

            Assert.AreNotEqual(GameAnalyticsCredentialValidator.NoProbeNeeded, corrected);

            GameAnalyticsCredentialValidator.Settle(rejected, GaProbe(
                GameAnalyticsCredentialValidator.ProbeState.CredentialInvalid, "ga-fix", "rejected"));

            Assert.AreEqual(GameAnalyticsCredentialValidator.ProbeState.Pending,
                GameAnalyticsCredentialValidator.Current.State);

            GameAnalyticsCredentialValidator.Settle(corrected, GaProbe(
                GameAnalyticsCredentialValidator.ProbeState.CredentialsValid, "ga-fix", "live pair"));

            Assert.AreEqual(GameAnalyticsCredentialValidator.ProbeState.CredentialsValid,
                GameAnalyticsCredentialValidator.Current.State);
        }

        /// <summary>Same flip-back race as Facebook's: a typo corrected back to the original key pair
        /// inside the timeout must not let the first request's answer win.</summary>
        [Test]
        public void GameAnalytics_CredentialsFlippedBackMidFlight_StillOnlyPublishesTheNewestRequest()
        {
            int firstA = GameAnalyticsCredentialValidator.BeginProbe("ga-flip", "secret");
            GameAnalyticsCredentialValidator.BeginProbe("ga-flip", "typo");
            int secondA = GameAnalyticsCredentialValidator.BeginProbe("ga-flip", "secret");

            Assert.AreNotEqual(firstA, secondA);

            GameAnalyticsCredentialValidator.Settle(firstA, GaProbe(
                GameAnalyticsCredentialValidator.ProbeState.Unreachable, "ga-flip", "first answer"));

            Assert.AreEqual(GameAnalyticsCredentialValidator.ProbeState.Pending,
                GameAnalyticsCredentialValidator.Current.State);

            GameAnalyticsCredentialValidator.Settle(secondA, GaProbe(
                GameAnalyticsCredentialValidator.ProbeState.CredentialsValid, "ga-flip", "newest answer"));

            Assert.AreEqual("newest answer", GameAnalyticsCredentialValidator.Current.Detail);
        }

        static FacebookPlatformValidator.ProbeResult Probe(
            FacebookPlatformValidator.ProbeState state, string appId, string platform, string detail) =>
            new FacebookPlatformValidator.ProbeResult(state, appId, platform, detail, 0);

        static GameAnalyticsCredentialValidator.ProbeResult GaProbe(
            GameAnalyticsCredentialValidator.ProbeState state, string gameKey, string detail) =>
            new GameAnalyticsCredentialValidator.ProbeResult(state, gameKey, detail, 0);
    }
}
