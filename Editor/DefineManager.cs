using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Centralized define symbol management for Sorolla Palette
    /// Eliminates DRY violations by providing unified define management
    /// </summary>
    public static class DefineManager
    {
        public const string FACEBOOK_DEFINE = "SOROLLA_FACEBOOK_ENABLED";
        public const string MAX_DEFINE = "SOROLLA_MAX_ENABLED";
        public const string ADJUST_DEFINE = "SOROLLA_ADJUST_ENABLED";

        /// <summary>
        /// Set a define symbol enabled/disabled for all build targets
        /// </summary>
        public static void SetDefineEnabled(string define, bool enabled)
        {
            SetDefineEnabledForTarget(NamedBuildTarget.Android, define, enabled);
            SetDefineEnabledForTarget(NamedBuildTarget.iOS, define, enabled);
        }

        /// <summary>
        /// Set a define symbol enabled/disabled for a specific build target
        /// </summary>
        public static void SetDefineEnabledForTarget(NamedBuildTarget buildTarget, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            var changed = false;

            if (enabled && !defines.Contains(define))
            {
                defines.Add(define);
                changed = true;
            }
            else if (!enabled && defines.Contains(define))
            {
                defines.Remove(define);
                changed = true;
            }

            if (changed)
            {
                var newSymbols = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, newSymbols);
                Debug.Log($"[DefineManager] Updated defines for {buildTarget}: {newSymbols}");
            }
        }

        /// <summary>
        /// Check if a define symbol is enabled for a build target
        /// </summary>
        public static bool IsDefineEnabled(NamedBuildTarget buildTarget, string define)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d));

            return defines.Contains(define);
        }

        /// <summary>
        /// Get all define symbols for a build target
        /// </summary>
        public static string[] GetDefines(NamedBuildTarget buildTarget)
        {
            return PlayerSettings.GetScriptingDefineSymbols(buildTarget)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToArray();
        }

        /// <summary>
        /// Apply mode-specific defines
        /// </summary>
        public static void ApplyModeDefines(string mode)
        {
            if (mode == "Prototype")
            {
                SetDefineEnabled(FACEBOOK_DEFINE, true);
                SetDefineEnabled(ADJUST_DEFINE, false);
                // MAX is optional in Prototype mode - don't force it
            }
            else if (mode == "Full")
            {
                SetDefineEnabled(ADJUST_DEFINE, true);
                SetDefineEnabled(MAX_DEFINE, true);
                SetDefineEnabled(FACEBOOK_DEFINE, false);
            }
        }
    }
}