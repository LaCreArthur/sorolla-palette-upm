using System;
using System.Collections.Generic;

namespace Sorolla.Palette.Health
{
    /// <summary>The build target a Graph response is graded against.</summary>
    internal enum FacebookPlatform
    {
        iOS,
        Android,
    }

    /// <summary>
    ///     What a Graph response proves about one platform's registration.
    ///     Three verdicts, not four: no consumer distinguishes "the app has zero platforms" from "this
    ///     platform is missing" - both mean every native call from this build gets rejected, and both
    ///     carry the same remedy.
    /// </summary>
    internal enum FacebookRegistrationVerdict
    {
        /// <summary>The response does not prove anything about this app's platforms.</summary>
        Unverified,

        /// <summary>Proven to be this app's object, and it does not carry this platform.</summary>
        NotRegistered,

        /// <summary>This platform is registered.</summary>
        Registered,
    }

    internal readonly struct FacebookRegistrationResult
    {
        internal readonly FacebookRegistrationVerdict Verdict;

        /// <summary>Whether the OTHER platform is registered. Only meaningful when the verdict is not
        /// Unverified; the Editor's verified detail names both platforms.</summary>
        internal readonly bool OtherPlatformRegistered;

        internal FacebookRegistrationResult(FacebookRegistrationVerdict verdict, bool otherPlatformRegistered)
        {
            Verdict = verdict;
            OtherPlatformRegistered = otherPlatformRegistered;
        }
    }

    /// <summary>
    ///     The SOLE owner of "does this Graph response prove the current platform is registered on this
    ///     FB app". The Editor validator and the runtime adapter both grade the same response, and before
    ///     4.0.3 they carried divergent copies of this logic: the 4.0.1 trust patch taught the Editor that
    ///     an absent supported_platforms field means zero platforms, but the runtime copy still read it as
    ///     "unreadable" and graded yellow where the Editor graded red. One owner, one answer.
    ///
    ///     Semantics (the post-4.0.1 Editor rules, now applied to both sides):
    ///     - Graph omits supported_platforms entirely rather than returning an empty list, so field
    ///       ABSENCE means zero platforms registered - but only once the body is proven to be THIS app's
    ///       object. That id-proof precondition lives inside this classifier rather than in a separate
    ///       pre-gate each caller had to remember. Without it, a 200-wrapped {"error":{...}}, a
    ///       captive-portal {"status":"ok"}, or a permission-stripped response would grade as "no platform
    ///       registered" and condemn an app on a fact never observed.
    ///     - A supported_platforms value that is not a list, and any body that does not parse, are
    ///       Unverified.
    ///
    ///     Graph vocabulary trap: supported_platforms uses IPHONE / IPAD / ANDROID. There is no "IOS"
    ///     value - a correctly-provisioned iOS app registers as IPHONE and/or IPAD. This shipped once as a
    ///     false "platform missing" report. This class is the only place that vocabulary exists; callers
    ///     pass a <see cref="FacebookPlatform"/> and keep their own human-facing display names.
    /// </summary>
    internal static class FacebookPlatformRegistration
    {
        public static FacebookRegistrationResult Classify(string body, string appId, FacebookPlatform platform)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(appId))
                return Unverified();

            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json))
                return Unverified();

            if (!json.TryGetValue("supported_platforms", out object raw))
            {
                // Absence only counts as zero platforms on a body proven to be this app's object.
                return ProvesAppId(json, appId)
                    ? new FacebookRegistrationResult(FacebookRegistrationVerdict.NotRegistered, false)
                    : Unverified();
            }

            if (!(raw is List<object> list))
                return Unverified();

            var platforms = new List<string>();
            foreach (object entry in list)
            {
                if (entry is string s)
                    platforms.Add(s);
            }

            bool registered = IsRegistered(platforms, platform);
            bool otherRegistered = IsRegistered(platforms, Other(platform));
            return new FacebookRegistrationResult(
                registered ? FacebookRegistrationVerdict.Registered : FacebookRegistrationVerdict.NotRegistered,
                otherRegistered);
        }

        static FacebookRegistrationResult Unverified() =>
            new FacebookRegistrationResult(FacebookRegistrationVerdict.Unverified, false);

        static FacebookPlatform Other(FacebookPlatform platform) =>
            platform == FacebookPlatform.iOS ? FacebookPlatform.Android : FacebookPlatform.iOS;

        static bool ProvesAppId(Dictionary<string, object> json, string appId) =>
            json.TryGetValue("id", out object rawId)
            && rawId is string id
            && string.Equals(id, appId, StringComparison.Ordinal);

        static bool IsRegistered(List<string> supportedPlatforms, FacebookPlatform platform)
        {
            foreach (string entry in supportedPlatforms)
            {
                if (platform == FacebookPlatform.iOS)
                {
                    if (string.Equals(entry, "IPHONE", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry, "IPAD", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (string.Equals(entry, "ANDROID", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     The Graph error message, when the body carries one. Shared because both surfaces split the
        ///     same three credential failures out of the errorCode 190 umbrella - only the remedy wording
        ///     differs (an asset-file fix in the Editor, a Meta-dashboard fix on device), so that stays
        ///     with each caller.
        /// </summary>
        public static bool TryGetGraphErrorMessage(string body, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(body)) return false;
            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json)) return false;
            if (!json.TryGetValue("error", out object rawError) || !(rawError is Dictionary<string, object> error)) return false;
            if (!error.TryGetValue("message", out object rawMessage) || !(rawMessage is string s)) return false;
            message = s;
            return true;
        }
    }
}
