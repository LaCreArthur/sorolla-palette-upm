using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Networking;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Validates a Facebook app's platform registration + credential pair via the Graph API
    ///     (GET /{app-id}?fields=supported_platforms). Async, non-blocking, result cached until the
    ///     app id/client token pair changes. Never called from a build-blocking synchronous path -
    ///     <see cref="BuildValidator.RunAllChecks"/> only reads the cached <see cref="Current"/> result and
    ///     kicks off a fresh probe if needed.
    ///
    ///     The one response describes BOTH platforms, but only the ACTIVE build target is GRADED
    ///     (2026-07-23, superseding the 2026-07-22 both-platforms ruling): a report judges the platform in
    ///     front of it, so an unregistered other platform can no longer hold a one-platform game below green.
    ///     The other platform is not hidden - the verified result states both platforms' registration - and it
    ///     becomes the graded one as soon as the build target switches, which is what catches the
    ///     boulder-evolution failure (an Android-only FB app whose iOS attribution silently stopped) at the
    ///     point where it matters: building for iOS.
    /// </summary>
    static class FacebookPlatformValidator
    {
        internal enum ProbeState
        {
            NotStarted,
            Pending,
            Verified,
            /// <summary>The platform this build targets is not registered on the FB app.</summary>
            PlatformMissing,
            CredentialInvalid,
            Unreachable,
        }

        internal readonly struct ProbeResult
        {
            internal readonly ProbeState State;
            internal readonly string AppId;
            internal readonly string PlatformName;
            internal readonly string Detail;
            internal readonly double TimestampSeconds;

            internal ProbeResult(ProbeState state, string appId, string platformName, string detail, double timestampSeconds)
            {
                State = state;
                AppId = appId;
                PlatformName = platformName;
                Detail = detail ?? "";
                TimestampSeconds = timestampSeconds;
            }
        }

        const int TimeoutSeconds = 3;

        // The configuration the CURRENT result describes - Pending while its probe is in flight, settled
        // once it lands. The key answers "does this configuration still need a probe"; the monotonic id
        // answers "is this arriving answer still the one we are waiting for". They are different
        // questions: an app id edited away and back inside the timeout window produces the SAME key
        // twice, so keying the settle on configuration would let the first, slower request publish last
        // and win. Only the newest request's id can publish.
        static ProbeResult s_lastResult = new ProbeResult(ProbeState.NotStarted, null, null, null, 0);
        static string s_lastRequestKey;
        static int s_lastRequestId;

        /// <summary>Fired on the main thread once a probe settles, so the window can refresh Build Health.</summary>
        internal static event Action OnProbeSettled;

        internal static ProbeResult Current => s_lastResult;

        // Graph vocabulary trap: FB Graph API supported_platforms uses IPHONE / IPAD / ANDROID.
        // There is no "IOS" value - a correctly-provisioned iOS app registers as IPHONE and/or
        // IPAD. This shipped once as a false "platform missing" report; do not compare against
        // "IOS" again. Human-facing messages still say "iOS" (ActivePlatformName below).
        static readonly string[] s_iosGraphPlatforms = { "IPHONE", "IPAD" };

        // Normal-case display string (acceptance-pass follow-up, 2026-07-21: "ANDROID" was the one
        // outlier shouting case among every other studio-facing platform label). The Graph API's own
        // vocabulary is still all-caps ("ANDROID"), so the comparison below is case-insensitive rather
        // than normalizing this string back to match it.
        internal static string ActivePlatformName() => EditorUserBuildSettings.activeBuildTarget switch
        {
            BuildTarget.iOS => "iOS",
            BuildTarget.Android => "Android",
            _ => null,
        };

        // Derived from the platform this result GRADES, not from the current editor target: the cached
        // result names its own platform, and the two disagree the moment the build target switches.
        static string OtherPlatformName(string platformName) => platformName == "iOS" ? "Android" : "iOS";

        static bool IsRegistered(List<string> supportedPlatforms, string platformName) =>
            platformName == "iOS"
                ? s_iosGraphPlatforms.Any(supportedPlatforms.Contains)
                : supportedPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Kicks off a Graph API probe for this app id/client token/active-platform combination if
        ///     one has not already run or is not already in flight. No-ops otherwise; read
        ///     <see cref="Current"/> for the (possibly still pending) result. The active platform is part of
        ///     the cache key even though one response covers both platforms: switching build target changes
        ///     which platform is graded, so the cached verdict no longer applies.
        /// </summary>
        internal static void EnsureChecked(string appId, string clientToken)
        {
            string platformName = ActivePlatformName();
            if (platformName == null)
                return;

            int requestId = BeginProbe(appId, clientToken, platformName);
            if (requestId == NoProbeNeeded)
                return;

            string accessToken = Uri.EscapeDataString(appId + "|" + clientToken);
            string url = $"https://graph.facebook.com/{Uri.EscapeDataString(appId)}?fields=supported_platforms&access_token={accessToken}";

            var request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ =>
            {
                ProbeResult settled = Evaluate(request, appId, platformName);
                request.Dispose();
                Settle(requestId, settled);
            };
        }

        /// <summary>Returned by <see cref="BeginProbe"/> when the current configuration is already covered
        /// (settled, or in flight) and no request is needed. Ids start at 1.</summary>
        internal const int NoProbeNeeded = 0;

        /// <summary>
        ///     Claims the cache for this configuration and returns the id its probe must settle under, or
        ///     <see cref="NoProbeNeeded"/> when the current configuration is already covered.
        ///
        ///     A configuration change while a request is in flight STARTS the new probe instead of being
        ///     dropped: the old guard returned early whenever anything was in flight, so a newer app id or
        ///     build target was silently discarded and the older probe settled over it. Issuing a fresh id
        ///     here is what makes every superseded answer identifiable when it arrives - see
        ///     <see cref="Settle"/>.
        /// </summary>
        internal static int BeginProbe(string appId, string clientToken, string platformName)
        {
            string key = $"{appId}|{clientToken}|{platformName}";
            if (s_lastRequestKey == key)
                return NoProbeNeeded;

            s_lastRequestKey = key;
            s_lastRequestId++;
            s_lastResult = new ProbeResult(ProbeState.Pending, appId, platformName,
                "Checking Facebook app platform registration...", EditorApplication.timeSinceStartup);
            return s_lastRequestId;
        }

        /// <summary>
        ///     Publishes a settled probe, unless a newer request has been issued while it was in flight -
        ///     that result answers a question the project no longer asks, and the request that replaced it
        ///     is already Pending and will publish its own answer.
        /// </summary>
        internal static void Settle(int requestId, ProbeResult result)
        {
            if (requestId != s_lastRequestId)
                return;

            s_lastResult = result;
            OnProbeSettled?.Invoke();
        }

        static ProbeResult Evaluate(UnityWebRequest request, string appId, string platformName)
        {
            bool networkOrProtocolError = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError;

            return EvaluateResponse(networkOrProtocolError, request.responseCode,
                request.downloadHandler?.text, appId, platformName, EditorApplication.timeSinceStartup);
        }

        /// <summary>
        ///     Transport-free grading of one Graph response, so the response semantics are testable
        ///     without a live request.
        /// </summary>
        internal static ProbeResult EvaluateResponse(
            bool networkOrProtocolError, long responseCode, string body, string appId, string platformName, double now)
        {
            if (networkOrProtocolError)
            {
                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    "Could not reach the Facebook Graph API (offline, or the endpoint is blocked). Re-run the check (Refresh) when online.", now);
            }

            if (responseCode != 200)
            {
                if (LooksLikeCredentialError(body))
                {
                    return new ProbeResult(ProbeState.CredentialInvalid, appId, platformName,
                        CredentialErrorDetail(appId, body), now);
                }

                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    $"Facebook Graph API request failed (HTTP {responseCode}). Re-run the check (Refresh) when online.", now);
            }

            if (!TryGetSupportedPlatforms(body, appId, out List<string> supportedPlatforms))
            {
                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    "Facebook Graph API response could not be parsed. Re-run the check (Refresh) when online.",
                    now);
            }

            string otherName = OtherPlatformName(platformName);
            bool otherRegistered = IsRegistered(supportedPlatforms, otherName);

            if (!IsRegistered(supportedPlatforms, platformName))
            {
                return new ProbeResult(ProbeState.PlatformMissing, appId, platformName,
                    $"FB app {appId} has no {platformName} platform registered in the FB console.\n" +
                    $"  Every native Graph/Login/attribution call from {platformName} will be rejected.", now);
            }

            // The verified detail names both platforms - the same zero-cost awareness the GameAnalytics group
            // caption carries. It states the other platform's registration without grading it: this build is
            // for one platform, and that is what the row judges.
            string detail = otherRegistered
                ? $"Facebook app {appId} has Android + iOS platforms registered."
                : $"Facebook app {appId}: {platformName} registered · {otherName} not registered.";
            return new ProbeResult(ProbeState.Verified, appId, platformName, detail, now);
        }

        static bool LooksLikeCredentialError(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            return body.Contains("OAuthException") || body.Contains("Invalid OAuth access token")
                || body.Contains("invalid_token") || body.Contains("Invalid access token");
        }

        // The Graph API collapses three distinct credential failures under the same errorCode 190
        // (OAuthException) umbrella. The message text is the only thing that tells them apart -
        // confirmed against 6 real app ids in the greenlight probe spike (2026-07-10). Split them
        // here so the studio-facing fix text names the actual cause instead of a generic "rejected".
        static string CredentialErrorDetail(string appId, string body)
        {
            string graphMessage = TryGetErrorMessage(body, out string msg) ? msg : null;

            string cause = graphMessage != null && graphMessage.Contains("Application has been deleted")
                ? $"Facebook app {appId} has been deleted from the developer console."
                : graphMessage != null && graphMessage.Contains("Invalid OAuth access token signature")
                    ? $"Facebook app {appId} exists, but the client token in FacebookSettings.asset does not match it."
                    : graphMessage != null && graphMessage.Contains("Invalid application ID")
                        ? $"App id {appId} in FacebookSettings.asset does not match any Facebook app."
                        : $"Facebook appId/clientToken pair for app {appId} was rejected by the Graph API.";

            return cause + "\n  Facebook init will report AuthError; analytics and attribution silently stop reaching Facebook.";
        }

        static bool TryGetErrorMessage(string body, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(body)) return false;
            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json)) return false;
            if (!json.TryGetValue("error", out object rawError) || !(rawError is Dictionary<string, object> error)) return false;
            if (!error.TryGetValue("message", out object rawMessage) || !(rawMessage is string s)) return false;
            message = s;
            return true;
        }

        /// <summary>
        ///     A 200 response whose body carries no supported_platforms field means the app has NO
        ///     platform registered at all - Graph omits the field entirely rather than returning an empty
        ///     list. Reading that as a parse failure reported an unregistered app as merely unreachable,
        ///     which is a false green (4.0.1 trust patch).
        ///
        ///     Field ABSENCE is only evidence of zero platforms when the body is proven to be THIS app's
        ///     object, which is what the id check below establishes. Without it, any 200 that is not the
        ///     app object - a 200-wrapped {"error":{...}}, a captive-portal {"status":"ok"}, or a
        ///     permission-stripped response - would grade as "no platform registered" and fail the report on
        ///     a fact never observed. Those stay parse failures, and so does a supported_platforms value
        ///     that is not a list.
        /// </summary>
        static bool TryGetSupportedPlatforms(string body, string appId, out List<string> platforms)
        {
            platforms = new List<string>();
            if (string.IsNullOrEmpty(body)) return false;

            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json)) return false;
            if (!json.TryGetValue("supported_platforms", out object raw))
                return json.TryGetValue("id", out object rawId) && rawId is string id && id == appId;
            if (!(raw is List<object> list)) return false;

            foreach (object entry in list)
            {
                if (entry is string s)
                    platforms.Add(s);
            }

            return true;
        }
    }
}
