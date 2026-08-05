using NUnit.Framework;
using Sorolla.Palette;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     "Not observed" on the purchase-verification row is ambiguous when consent denial is the reason
    ///     nothing was attempted: ConsentCoordinator disables Adjust from the same ad-storage decision it
    ///     records, so the verification call never happens and the row read as if a test purchase were
    ///     still owed. The wording is derived at render time from the two existing owners rather than
    ///     recorded as new state, so nothing races the adapter-outcome writer.
    /// </summary>
    public class PurchaseVerificationDetailTests
    {
        const string NotObserved = "Not observed";

        static string Detail(
            PurchaseVerificationState state,
            string recordedDetail = NotObserved,
            bool adjustApplicable = true,
            bool consentSignalsKnown = true,
            bool adStorageConsent = false) =>
            SorollaDiagnostics.PurchaseVerificationDetail(
                state, recordedDetail, adjustApplicable, consentSignalsKnown, adStorageConsent);

        [Test]
        public void NotObserved_WithDeniedConsent_SaysAdjustWasDisabled()
        {
            Assert.AreEqual("Not attempted - Adjust disabled by denied consent",
                Detail(PurchaseVerificationState.NotObserved));
        }

        /// <summary>Consent granted: nothing explains the absence, so the row must not invent a reason.</summary>
        [Test]
        public void NotObserved_WithGrantedConsent_StaysNotObserved()
        {
            Assert.AreEqual(NotObserved,
                Detail(PurchaseVerificationState.NotObserved, adStorageConsent: true));
        }

        /// <summary>Unknown signals are not a denial - claiming one would be a guess.</summary>
        [Test]
        public void NotObserved_WithUnknownSignals_StaysNotObserved()
        {
            Assert.AreEqual(NotObserved,
                Detail(PurchaseVerificationState.NotObserved, consentSignalsKnown: false));
        }

        /// <summary>Prototype mode has no Adjust at all; consent is not why nothing was verified.</summary>
        [Test]
        public void NotObserved_WithAdjustNotApplicable_StaysNotObserved()
        {
            Assert.AreEqual(NotObserved,
                Detail(PurchaseVerificationState.NotObserved, adjustApplicable: false));
        }

        /// <summary>A real verdict is never overwritten by the consent wording.</summary>
        [Test]
        public void VerifiedState_IsUnaffectedByDeniedConsent()
        {
            Assert.AreEqual("Verified", Detail(PurchaseVerificationState.Verified, "Verified"));
        }

        [Test]
        public void FailedState_IsUnaffectedByDeniedConsent()
        {
            Assert.AreEqual("some vendor code", Detail(PurchaseVerificationState.Failed, "some vendor code"));
        }

        [Test]
        public void EnvironmentMismatch_KeepsItsExplanation()
        {
            string detail = Detail(PurchaseVerificationState.EnvironmentMismatch, "env_mismatch");

            StringAssert.Contains("env_mismatch", detail);
            StringAssert.Contains("the purchase itself is not verified", detail);
        }
    }
}
