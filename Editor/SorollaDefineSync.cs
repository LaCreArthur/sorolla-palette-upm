// filepath: Packages/com.sorolla.palette/Editor/SorollaDefineSync.cs

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SorollaPalette.Editor
{
    [InitializeOnLoad]
    internal static class SorollaDefineSync
    {
        private const string MODE_KEY = "SorollaPalette_Mode"; // "Prototype" | "Full" | "Not Selected"

        private const string FACEBOOK_DEFINE = "SOROLLA_FACEBOOK_ENABLED";
        private const string MAX_DEFINE = "SOROLLA_MAX_ENABLED";
        private const string ADJUST_DEFINE = "SOROLLA_ADJUST_ENABLED";

        static SorollaDefineSync()
        {
            // Sync on editor load and after script reloads
            EditorApplication.delayCall += SyncAllPlatforms;
        }

        public static void SyncAllPlatforms()
        {
            try
            {
                var mode = EditorPrefs.GetString(MODE_KEY, "Not Selected");
                if (mode == "Not Selected")
                    // Do not enforce anything until user picks a mode
                    return;

                SyncDefinesFor(NamedBuildTarget.Android, mode);
                SyncDefinesFor(NamedBuildTarget.iOS, mode);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Sorolla DefineSync] Failed to sync defines: " + e.Message);
            }
        }

        private static void SyncDefinesFor(NamedBuildTarget nbt, string mode)
        {
            DefineManager.ApplyModeDefines(mode);
        }
    }
}
#endif