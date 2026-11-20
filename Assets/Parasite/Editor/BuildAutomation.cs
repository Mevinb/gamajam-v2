using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Parasite.EditorTools
{
    /// <summary>Command-line / menu Linux build. Logs failures and exits non-zero in batch mode.</summary>
    public static class BuildAutomation
    {
        public static readonly string[] Scenes =
        {
            "Assets/Parasite/Scenes/Bootstrap.unity",
            "Assets/Parasite/Scenes/City.unity",
        };

        [MenuItem("Parasite/Build/Linux x86-64")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", "Parasite.x86_64");

        [MenuItem("Parasite/Build/Windows x64")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", "Parasite.exe");

        static void Build(BuildTarget target, string folder, string exe)
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", folder));
            Directory.CreateDirectory(outDir);
            var scenes = Scenes.Where(File.Exists).ToArray();
            if (scenes.Length == 0) { Fail("No scenes found — run Parasite/Setup/Build Scenes first."); return; }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, exe),
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Parasite] {folder} build result: {s.result}, size {s.totalSize / (1024 * 1024)} MB, time {s.totalTime}, errors {s.totalErrors}, warnings {s.totalWarnings}");
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type is LogType.Error or LogType.Exception) Debug.LogError($"[Parasite][BuildError] {msg.content}");
            if (s.result != BuildResult.Succeeded) Fail("Build failed: " + s.result);
            else if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Fail(string msg)
        {
            Debug.LogError("[Parasite] " + msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>Batch helper: just compile + import, then exit (used for CI-like checks).</summary>
        public static void CompileCheck()
        {
            Debug.Log("[Parasite] Compile check OK at " + DateTime.Now);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
