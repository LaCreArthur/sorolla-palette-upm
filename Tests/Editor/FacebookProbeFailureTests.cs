using NUnit.Framework;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class FacebookProbeFailureTests
    {
        [Test]
        public void Detail_DeletedApp_ProvidesReplacementFix()
        {
            const string raw = "{\"error\":{\"message\":\"Error validating application. Application has been deleted.\",\"type\":\"OAuthException\",\"code\":190}}";

            string detail = FacebookProbeFailure.Detail("123456", "HTTP/1.1 400 Bad Request", raw);

            Assert.That(detail, Does.Contain("has been deleted"));
            Assert.That(detail, Does.Contain("Create a replacement Meta app"));
            Assert.That(detail, Does.Not.Contain("400 Bad Request"));
        }

        [Test]
        public void Detail_ClientTokenMismatch_ProvidesTokenFix()
        {
            const string raw = "{\"error\":{\"message\":\"Invalid OAuth access token signature.\",\"type\":\"OAuthException\",\"code\":190}}";

            string detail = FacebookProbeFailure.Detail("123456", "HTTP/1.1 400 Bad Request", raw);

            Assert.That(detail, Does.Contain("Client Token does not match"));
            Assert.That(detail, Does.Contain("Settings -> Advanced"));
        }

        [Test]
        public void Detail_WithoutGraphBody_PreservesTransportError()
        {
            string detail = FacebookProbeFailure.Detail("123456", "HTTP/1.1 503 Service Unavailable", null);

            Assert.AreEqual("HTTP/1.1 503 Service Unavailable", detail);
        }

        [Test]
        public void Detail_WithNonJsonBody_PreservesTransportError()
        {
            string detail = FacebookProbeFailure.Detail(
                "123456", "HTTP/1.1 502 Bad Gateway", "<html>proxy error</html>");

            Assert.AreEqual("HTTP/1.1 502 Bad Gateway", detail);
        }
    }
}
