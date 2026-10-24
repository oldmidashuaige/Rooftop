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
        // 用绝对路径，避免 eval/批处理的 cwd 不同导致构建写到别处
        string outExe = System.IO.Path.Combine(Application.dataPath, "..", "Builds", "Rooftop.exe");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = outExe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[Build] result={summary.result} size={summary.totalSize} time={summary.totalTime} -> {outExe}");
        if (summary.result != BuildResult.Succeeded)
            throw new System.Exception("[Build] 构建失败: " + summary.result);
    }
}
