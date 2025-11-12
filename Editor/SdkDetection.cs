// filepath: Packages/com.sorolla.palette/Editor/SdkDetection.cs

using System;
using System.Linq;

namespace SorollaPalette.Editor
{
    internal static class SdkDetection
    {
        private static bool HasType(string assemblyQualifiedType)
        {
            return Type.GetType(assemblyQualifiedType) != null;
        }

        private static bool HasAssembly(string assemblyNameContains)
        {
            var term = assemblyNameContains.ToLowerInvariant();
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.ToLowerInvariant().Contains(term));
        }

        public static bool IsGameAnalyticsInstalled()
        {
            return HasType("GameAnalytics, GameAnalyticsSDK") ||
                   HasType("GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK") ||
                   HasAssembly("GameAnalyticsSDK");
        }

        public static bool IsFacebookInstalled()
        {
            return HasType("Facebook.Unity.FB, Facebook.Unity") || HasAssembly("Facebook.Unity");
        }

        public static bool IsMaxInstalled()
        {
            return HasType("MaxSdk, MaxSdk.Scripts") ||
                   HasType("MaxSdkBase, MaxSdk.Scripts") ||
                   HasAssembly("MaxSdk.Scripts") || HasAssembly("applovin");
        }

        public static bool IsAdjustInstalled()
        {
            // Direct type checks
            if (HasType("com.adjust.sdk.Adjust, com.adjust.sdk") || HasType("com.adjust.sdk.Adjust, Assembly-CSharp"))
                return true;
            if (HasType("AdjustSdk.Adjust, AdjustSdk.Scripts"))
                return true;

            // Fallback: scan loaded assemblies for any that look like Adjust
            if (HasAssembly("com.adjust.sdk") || HasAssembly("adjustsdk.scripts") || HasAssembly("adjust"))
                return true;

            return false;
        }
    }
}