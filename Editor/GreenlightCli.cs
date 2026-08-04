using System;
using System.Collections.Generic;
using System.IO;
using Sorolla.Palette.Editor.Greenlight;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Headless entry point for the canonical greenlight report, so CI and command-line workflows get
    ///     the exact text the window's Copy Report button copies - same checks, same evaluator, same export:
    ///     <code>
    ///     Unity -batchmode -projectPath &lt;project&gt; -buildTarget &lt;target&gt;
    ///           -executeMethod Sorolla.Palette.Editor.GreenlightCli.Report
    ///           [-sorollaReportPath /abs/path.txt] -logFile &lt;log&gt;
    ///     </code>
    ///     Runs the window's refresh sequence, waits (bounded) for the credential probes to settle so the
    ///     report carries real probe verdicts instead of Pending, writes the report, then
    ///     exits 0. Exits 1 when the report cannot be produced. Do not pass -quit: the entry point owns the
    ///     editor lifetime because the probe needs update pumping after -executeMethod returns.
    /// </summary>
    public static class GreenlightCli
    {
        const double ProbeTimeoutSeconds = 30;

        static string s_path;
        static double s_deadline;
        static List<BuildValidator.ValidationResult> s_results;

        public static void Report()
        {
            s_path = ArgAfter("-sorollaReportPath") ??
                     Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                         "sorolla-greenlight-report.txt");
            try
            {
                RunChecks();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette] Greenlight CLI: check run failed: {e}");
                EditorApplication.Exit(1);
                return;
            }

            s_deadline = EditorApplication.timeSinceStartup + ProbeTimeoutSeconds;
            EditorApplication.update += WaitForProbeThenWrite;
        }

        static void RunChecks()
        {
            foreach (string repair in BuildValidator.ResolveRequiredPackages())
                Debug.Log($"[Palette] Greenlight CLI: auto-fixed {repair}");
            foreach (string repair in BuildValidator.RunSafeAutoFixes())
                Debug.Log($"[Palette] Greenlight CLI: auto-fixed {repair}");
            s_results = BuildValidator.RunAllChecks();
        }

        static void WaitForProbeThenWrite()
        {
            bool pending =
                GameAnalyticsCredentialValidator.Current.State ==
                GameAnalyticsCredentialValidator.ProbeState.Pending ||
                FacebookPlatformValidator.Current.State == FacebookPlatformValidator.ProbeState.Pending;
            if (pending && EditorApplication.timeSinceStartup < s_deadline)
                return;

            EditorApplication.update -= WaitForProbeThenWrite;
            try
            {
                // Re-run so a settled (or timed-out) probe state reaches the report as evidence.
                RunChecks();
                ReadinessReport report = GreenlightEvaluator.Evaluate(s_results);
                File.WriteAllText(s_path, GreenlightReportExport.ToText(report));
                Debug.Log($"[Palette] Greenlight CLI: report written to {s_path}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette] Greenlight CLI: report write failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        static string ArgAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
