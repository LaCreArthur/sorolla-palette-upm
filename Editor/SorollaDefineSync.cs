// filepath: Packages/com.sorolla.palette/Editor/SorollaDefineSync.cs

#if UNITY_EDITOR
using System;
using System.Linq;
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
            var defines = PlayerSettings.GetScriptingDefineSymbols(nbt)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            var changed = false;

            void Ensure(string d)
            {
                if (!defines.Contains(d))
                {
                    defines.Add(d);
                    changed = true;
                }
            }

            void Remove(string d)
            {
                if (defines.Remove(d)) changed = true;
            }

            // Enforce according to mode
            if (mode == "Prototype")
            {
                // Prototype: Facebook ON, Adjust OFF; MAX optional (leave as-is)
                Ensure(FACEBOOK_DEFINE);
                Remove(ADJUST_DEFINE);
                // Do not force MAX_DEFINE; user toggles it in the window
            }
            else if (mode == "Full")
            {
                // Full: Adjust ON, Facebook OFF; MAX ON (required)
                Ensure(ADJUST_DEFINE);
                Ensure(MAX_DEFINE);
                Remove(FACEBOOK_DEFINE);
            }

            if (changed)
            {
                var newSymbols = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(nbt, newSymbols);
                Debug.Log($"[Sorolla DefineSync] Updated defines for {nbt}: {newSymbols}");
            }
        }
    }
}
#endif