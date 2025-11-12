using System;
using System.Linq;

namespace SorollaPalette.Editor
{
    /// <summary>
    ///     DRY SDK detection utilities - refactored to eliminate repetitive patterns
    /// </summary>
    internal static class SdkDetection
    {
        private static bool HasType(string assemblyQualifiedType)
        {
            return Type.GetType(assemblyQualifiedType) != null;
        }

        private static bool HasAssembly(string assemblyNameContains)
        {
            var term = assemblyNameContains.ToLowerInvariant();
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name.ToLowerInvariant().Contains(term));
        }

        /// <summary>
        ///     Generic SDK detection method - checks multiple type names
        /// </summary>
        private static bool IsSDKInstalled(params string[] typeNames)
        {
            return typeNames.Any(HasType);
        }

        public static bool IsGameAnalyticsInstalled()
        {
            return IsSDKInstalled("GameAnalytics, GameAnalyticsSDK",
                       "GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK") ||
                   HasAssembly("GameAnalyticsSDK");
        }

        public static bool IsFacebookInstalled()
        {
            return IsSDKInstalled("Facebook.Unity.FB, Facebook.Unity") ||
                   HasAssembly("Facebook.Unity");
        }

        public static bool IsMaxInstalled()
        {
            return IsSDKInstalled("MaxSdk, MaxSdk.Scripts",
                       "MaxSdkBase, MaxSdk.Scripts") ||
                   HasAssembly("MaxSdk.Scripts") ||
                   HasAssembly("applovin");
        }

        public static bool IsAdjustInstalled()
        {
            return IsSDKInstalled("com.adjust.sdk.Adjust, com.adjust.sdk",
                       "com.adjust.sdk.Adjust, Assembly-CSharp",
                       "AdjustSdk.Adjust, AdjustSdk.Scripts") ||
                   HasAssembly("com.adjust.sdk") ||
                   HasAssembly("adjustsdk.scripts") ||
                   HasAssembly("adjust");
        }
    }
}