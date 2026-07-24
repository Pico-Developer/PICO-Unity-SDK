using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;
using System.IO;

public static class BuildAutomator
{
    [MenuItem("Build/Build and Profile")]
    public static void BuildAndProfile()
    {
        EditorApplication.delayCall += () =>
        {
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow"));
        };

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes are enabled in Build Settings!");
            return;
        }

        string projectName = Path.GetFileName(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        string apkPath = $"{projectName}.apk";

        var buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AutoRunPlayer | BuildOptions.EnableDeepProfilingSupport
        };

        Debug.Log("Building Player...");

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {summary.totalSize / (1024f * 1024f)} MB");
            Debug.Log($"Built APK to: {apkPath}");
        }
        else
        {
            Debug.LogError("Build failed: " + summary.result);
        }
    }
}
