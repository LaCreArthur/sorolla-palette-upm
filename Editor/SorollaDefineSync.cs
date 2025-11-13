using UnityEditor;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Simplified define sync - now just delegates to ModeManager
    /// </summary>
    [InitializeOnLoad]
    internal static class SorollaDefineSync
    {
        static SorollaDefineSync()
        {
            // Sync defines on editor load
            EditorApplication.delayCall += () =>
            {
                if (ModeManager.IsModeSelected())
                {
                    var mode = ModeManager.GetCurrentMode();
                    DefineManager.ApplyModeDefines(mode);
                }
            };
        }
    }
}