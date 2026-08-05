using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The classifier is the single owner of "does this Graph response prove the current platform is
    ///     registered". Before 4.0.3 the Editor and the runtime adapter each carried a copy and they
    ///     disagreed: an app-id-proven body with no supported_platforms field graded red in the Editor
    ///     (4.0.1 trust patch) and yellow on device. Hosting the classifier in the engine-free Health
    ///     assembly is what makes the runtime half reachable from an edit-mode test at all.
    /// </summary>
    public class FacebookPlatformRegistrationTests
    {
        const string AppId = "123456";

        static FacebookRegistrationResult Classify(string body, FacebookPlatform platform = FacebookPlatform.Android) =>
            FacebookPlatformRegistration.Classify(body, AppId, platform);

        /// <summary>
        ///     Graph omits supported_platforms entirely rather than returning an empty list, so absence on a
        ///     body proven to be THIS app means zero platforms registered. This is the case the runtime
        ///     copy graded yellow; it is red now.
        /// </summary>
        [Test]
        public void AbsentField_WithMatchingAppId_IsNotRegistered()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.NotRegistered,
                Classify("{\"id\":\"123456\"}").Verdict);
        }

        /// <summary>
        ///     Without the id proof, absence proves nothing: a 200-wrapped error, a captive portal, or a
        ///     permission-stripped response would otherwise condemn the app on a fact never observed.
        /// </summary>
        [Test]
        public void AbsentField_WithWrongAppId_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"999999\"}").Verdict);
        }

        [Test]
        public void AbsentField_WithNoAppId_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"name\":\"Some App\"}").Verdict);
        }

        [Test]
        public void CaptivePortalBody_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"status\":\"ok\"}").Verdict);
        }

        [Test]
        public void GraphErrorBody_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"error\":{\"message\":\"Invalid application ID\",\"code\":190}}").Verdict);
        }

        [Test]
        public void UnparseableBody_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified, Classify("not json at all").Verdict);
        }

        [Test]
        public void EmptyBody_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified, Classify("").Verdict);
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified, Classify(null).Verdict);
        }

        /// <summary>
        ///     A response cut short mid-transfer is missing BYTES, not missing PLATFORMS. The parser used
        ///     to hand back the partial object it had built, so a body truncated just before
        ///     supported_platforms looked exactly like an id-proven app with zero platforms and graded
        ///     red on device. Truncation must read as unproven.
        /// </summary>
        [Test]
        public void TruncatedBody_WithMatchingAppId_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"123456\",\"supported_platf").Verdict);
        }

        [Test]
        public void TruncatedBody_RightAfterAppId_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"123456\",").Verdict);
        }

        /// <summary>Truncation INSIDE the platforms list must not be read as a complete short list.</summary>
        [Test]
        public void TruncatedInsidePlatformList_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\"", FacebookPlatform.iOS).Verdict);
        }

        /// <summary>
        ///     Same truncation, asking about the platform that IS in the partial list. Still Unverified:
        ///     the list was never proven complete, so neither answer it could give is trustworthy.
        /// </summary>
        [Test]
        public void TruncatedInsidePlatformList_DoesNotGreenTheListedPlatform()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\"").Verdict);
        }

        /// <summary>A supported_platforms that is not a list proves nothing about registration.</summary>
        [Test]
        public void NonListSupportedPlatforms_IsUnverified()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Unverified,
                Classify("{\"id\":\"123456\",\"supported_platforms\":\"ANDROID\"}").Verdict);
        }

        /// <summary>
        ///     Graph vocabulary trap: there is no "IOS" value. A correctly-provisioned iOS app registers as
        ///     IPHONE and/or IPAD. Comparing against "IOS" shipped once as a false "platform missing".
        /// </summary>
        [Test]
        public void IphoneRegistersIos()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"IPHONE\"]}", FacebookPlatform.iOS).Verdict);
        }

        [Test]
        public void IpadRegistersIos()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"IPAD\"]}", FacebookPlatform.iOS).Verdict);
        }

        [Test]
        public void AndroidOnlyApp_DoesNotRegisterIos()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.NotRegistered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\"]}", FacebookPlatform.iOS).Verdict);
        }

        [Test]
        public void AndroidIsRegistered()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\"]}").Verdict);
        }

        [Test]
        public void PlatformMatchIsCaseInsensitive()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"android\"]}").Verdict);
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"iphone\"]}", FacebookPlatform.iOS).Verdict);
        }

        /// <summary>The Editor's verified detail names both platforms, so the flag must track the OTHER one.</summary>
        [Test]
        public void OtherPlatformFlag_TracksTheOppositePlatform()
        {
            FacebookRegistrationResult both =
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\",\"IPHONE\"]}");
            Assert.AreEqual(FacebookRegistrationVerdict.Registered, both.Verdict);
            Assert.IsTrue(both.OtherPlatformRegistered);

            FacebookRegistrationResult androidOnly =
                Classify("{\"id\":\"123456\",\"supported_platforms\":[\"ANDROID\"]}");
            Assert.AreEqual(FacebookRegistrationVerdict.Registered, androidOnly.Verdict);
            Assert.IsFalse(androidOnly.OtherPlatformRegistered);
        }

        /// <summary>An app id is not required to be present when the field IS readable.</summary>
        [Test]
        public void PresentPlatformList_DoesNotNeedIdProof()
        {
            Assert.AreEqual(FacebookRegistrationVerdict.Registered,
                Classify("{\"supported_platforms\":[\"ANDROID\"]}").Verdict);
        }

        [Test]
        public void GraphErrorMessage_IsExtracted()
        {
            Assert.IsTrue(FacebookPlatformRegistration.TryGetGraphErrorMessage(
                "{\"error\":{\"message\":\"Application has been deleted\"}}", out string message));
            Assert.AreEqual("Application has been deleted", message);
        }

        [Test]
        public void GraphErrorMessage_AbsentOnCleanBody()
        {
            Assert.IsFalse(FacebookPlatformRegistration.TryGetGraphErrorMessage(
                "{\"id\":\"123456\"}", out _));
        }
    }
}
