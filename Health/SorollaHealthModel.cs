using System;

namespace Sorolla.Palette.Health
{
    internal enum EvalMode
    {
        Unknown,
        Prototype,
        Full,
    }

    [Flags]
    internal enum SdkModule
    {
        None = 0,
        GameAnalytics = 1 << 0,
        Facebook = 1 << 1,
        FirebaseApp = 1 << 2,
        FirebaseAnalytics = 1 << 3,
        FirebaseCrashlytics = 1 << 4,
        FirebaseRemoteConfig = 1 << 5,
        AppLovinMax = 1 << 6,
        Adjust = 1 << 7,
        UnityIap = 1 << 8,
        Firebase = FirebaseApp | FirebaseAnalytics | FirebaseCrashlytics | FirebaseRemoteConfig,
    }
}
