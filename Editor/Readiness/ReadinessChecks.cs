using System;
using System.Collections.Generic;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor
{
    internal static class ReadinessChecks
    {
        static ReadinessRequirementDecision Required(string reason) =>
            new ReadinessRequirementDecision(ReadinessRequirement.Required, reason);
        static ReadinessRequirementDecision Optional(string reason) =>
            new ReadinessRequirementDecision(ReadinessRequirement.Optional, reason);
        static ReadinessRequirementDecision NotApplicable(string reason) =>
            new ReadinessRequirementDecision(ReadinessRequirement.NotApplicable, reason);
        static ReadinessRequirementDecision Unknown(string reason) =>
            new ReadinessRequirementDecision(ReadinessRequirement.Unknown, reason);

        static readonly Func<ReadinessContext, ReadinessRequirementDecision> AlwaysRequired =
            _ => Required("required in both modes");
        static readonly Func<ReadinessContext, ReadinessRequirementDecision> AlwaysOptional =
            _ => Optional("advisory check");

        static Func<ReadinessContext, ReadinessRequirementDecision> Dependent(SdkModule module) => context =>
        {
            if (context.Mode == EvalMode.Unknown)
                return Unknown("SDK mode is unknown (no config)");
            if (!context.ModulesResolved)
                return Unknown("package manifest could not be resolved");

            CapabilityState state = CapabilityPolicy.Resolve(context.Mode, context.InstalledModules, module);
            if (!state.Applicable)
                return NotApplicable(state.Required
                    ? "required package is absent; the package check owns the failure"
                    : "capability is not included in this mode");
            return state.Required
                ? Required("included and required in Full mode")
                : Optional("included optional capability");
        };

        /// <summary>
        ///     The SOLE owner of Firebase applicability. The config producers never ask this question: they
        ///     observe, and the evaluator discards what does not apply. Exposed only so the three-way answer
        ///     is directly pinned by a test.
        /// </summary>
        internal static ReadinessRequirementDecision FirebaseSuite(ReadinessContext context)
        {
            if (context.Mode == EvalMode.Unknown)
                return Unknown("SDK mode is unknown (no config)");
            if (!context.ModulesResolved)
                return Unknown("package manifest could not be resolved");

            SdkModule included = context.InstalledModules & SdkModule.Firebase;
            if (context.Mode == EvalMode.Full)
                return included == SdkModule.Firebase
                    ? Required("complete suite included and required in Full mode")
                    : NotApplicable("required package suite is incomplete; the package check owns the failure");
            return included == SdkModule.None
                ? NotApplicable("capability is not included in Prototype")
                : Optional("included optional capability");
        }

        static Func<ReadinessContext, ReadinessRequirementDecision> OnPlatform(
            ReadinessPlatform platform,
            Func<ReadinessContext, ReadinessRequirementDecision> active) => context =>
            context.Platform == platform ? active(context)
            : context.Platform == ReadinessPlatform.Unknown
                ? NotApplicable("active build target is not a mobile platform")
                : NotApplicable($"judged only on {(platform == ReadinessPlatform.iOS ? "iOS" : "Android")}, which is not the active target");

        internal static readonly ReadinessCheck RequiredSdks =
            Check("build.required_sdks", "Required SDKs", ReadinessGroup.BuildAndProject, AlwaysRequired);
        internal static readonly ReadinessCheck VersionMismatches =
            Check("build.sdk_versions", "SDK Versions", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck ModeConsistency =
            Check("build.mode_consistency", "Mode Consistency", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck ScopedRegistries =
            Check("build.scoped_registries", "Scoped Registries", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck FirebaseCoherence =
            Check("build.firebase_coherence", "Firebase Coherence", ReadinessGroup.Firebase, FirebaseSuite, SdkModule.Firebase);
        internal static readonly ReadinessCheck ConfigSync =
            Check("build.config_sync", "Config Sync", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck AndroidManifest =
            Check("build.android_manifest", "Android Manifest", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck MaxSettings =
            Check("build.max_settings", "MAX Settings", ReadinessGroup.AppLovinMax, Dependent(SdkModule.AppLovinMax), SdkModule.AppLovinMax);
        internal static readonly ReadinessCheck AdjustSettings =
            Check("build.adjust_settings", "Adjust Settings", ReadinessGroup.Adjust, Dependent(SdkModule.Adjust), SdkModule.Adjust);
        internal static readonly ReadinessCheck Edm4uSettings =
            Check("build.edm4u_settings", "EDM4U Settings", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck GradleConfig =
            Check("build.gradle_config", "Gradle Configuration", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck FirebaseConfigAndroid =
            Check("build.firebase_config_android", "Firebase Android Config", ReadinessGroup.Firebase,
                OnPlatform(ReadinessPlatform.Android, FirebaseSuite), SdkModule.Firebase);
        internal static readonly ReadinessCheck FirebaseConfigIos =
            Check("build.firebase_config_ios", "Firebase iOS Config", ReadinessGroup.Firebase,
                OnPlatform(ReadinessPlatform.iOS, FirebaseSuite), SdkModule.Firebase);
        internal static readonly ReadinessCheck GameAnalyticsSettings =
            Check("build.gameanalytics_keys", "GameAnalytics Platform Keys", ReadinessGroup.GameAnalytics,
                Dependent(SdkModule.GameAnalytics), SdkModule.GameAnalytics);
        internal static readonly ReadinessCheck FacebookPlatformConfig =
            Check("build.facebook_platform", "Facebook Platform", ReadinessGroup.Facebook,
                Dependent(SdkModule.Facebook), SdkModule.Facebook);
        internal static readonly ReadinessCheck VerboseLogging =
            Check("build.verbose_logging", "Verbose Logging", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck DevelopmentBuild =
            Check("build.development_build", "Development Build", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck AdjustSandboxMode =
            Check("build.adjust_sandbox_mode", "Adjust Sandbox Mode", ReadinessGroup.Adjust,
                Dependent(SdkModule.Adjust), SdkModule.Adjust);
        internal static readonly ReadinessCheck AndroidKeystore =
            Check("build.android_keystore", "Android Keystore", ReadinessGroup.BuildAndProject,
                context => context.Platform == ReadinessPlatform.Unknown
                    ? Unknown("build platform is unknown")
                    : context.Platform == ReadinessPlatform.Android
                        ? Required("Android build fact")
                        : Optional("evaluated only if reported off-Android"),
                releaseOnly: true);
        internal static readonly ReadinessCheck GradleJavaHome =
            Check("build.gradle_java_home", "Gradle Java Home", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck GameAnalyticsResourceWhitelist =
            Check("build.gameanalytics_resource_whitelist", "GameAnalytics Resource Whitelist",
                ReadinessGroup.GameAnalytics, AlwaysOptional);
        internal static readonly ReadinessCheck AddressablesContent =
            Check("build.addressables_content", "Addressables Content", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck SdkPin =
            Check("build.sdk_pin", "SDK Pin", ReadinessGroup.BuildAndProject, AlwaysOptional);
        internal static readonly ReadinessCheck GameAnalyticsCredentialProbe =
            Check("build.gameanalytics_credentials", "GameAnalytics Credentials", ReadinessGroup.GameAnalytics,
                Dependent(SdkModule.GameAnalytics), SdkModule.GameAnalytics);

        internal static readonly IReadOnlyList<ReadinessCheck> All = new[]
        {
            RequiredSdks, GameAnalyticsSettings, GameAnalyticsCredentialProbe, FacebookPlatformConfig,
            FirebaseCoherence, MaxSettings, AdjustSettings, FirebaseConfigAndroid, FirebaseConfigIos,
            AndroidKeystore, AdjustSandboxMode, SdkPin, VersionMismatches, ModeConsistency, ScopedRegistries,
            ConfigSync, AndroidManifest, Edm4uSettings, GradleConfig, VerboseLogging, DevelopmentBuild,
            GradleJavaHome, GameAnalyticsResourceWhitelist, AddressablesContent,
        };

        static ReadinessCheck Check(
            string id,
            string label,
            ReadinessGroup group,
            Func<ReadinessContext, ReadinessRequirementDecision> requirement,
            SdkModule capability = SdkModule.None,
            bool releaseOnly = false) =>
            new ReadinessCheck(id, label, group, requirement, capability, releaseOnly);
    }
}
