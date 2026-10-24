using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 构建入口：Windows 64 位玩家（Builds/Rooftop.exe）。
/// 可经菜单 Tools/Build Windows，也可 -executeMethod 或 pipeline eval 调用。
/// 构建产物为单人份（一份构建三角色由启动参数决定）。
/// </summary>
public static class Builder
{
    [MenuItem("Tools/Build Windows")]
    public static void PerformBuild()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/Rooftop.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[Build] result={summary.result} size={summary.totalSize} time={summary.totalTime}");
        if (summary.result != BuildResult.Succeeded)
            throw new System.Exception("[Build] 构建失败: " + summary.result);
    }
}
