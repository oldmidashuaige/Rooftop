using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器工具：构建游戏场景（Assets/Scenes/Main.unity）。
/// 生成程序化世界 + 网络管理器 + 总控 + 刷怪器 + 主菜单，接线预制体引用，加入 Build Settings。
/// </summary>
public static class BuildScene
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/Build Game Scene")]
    public static void Build()
    {
        var mats = MaterialAssets.Ensure();

        // 确保 Main 场景存在并打开
        var scene = EnsureScene();
        SceneManager.SetActiveScene(scene);

        SceneBuilder.Clear();
        SceneBuilder.BuildWorld(mats);

        WirePrefabReferences();

        // 主菜单组件（uGUI 自己构建 Canvas）
        var menuGo = new GameObject("MainMenu");
        menuGo.AddComponent<MainMenuUI>();
        SceneBuilder.MarkLocal(menuGo);

        // 启动登录界面（离线，输入用户名；服务器 batchmode 自动跳过）
        var loginGo = new GameObject("Login");
        loginGo.AddComponent<LoginUI>();
        SceneBuilder.MarkLocal(loginGo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AddToBuildSettings(scene);
        Debug.Log("游戏场景已构建并保存: " + ScenePath);
    }

    [MenuItem("Tools/Clear Demo Objects")]
    public static void ClearOnly()
    {
        SceneBuilder.Clear();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Demo 对象已清理。");
    }

    private static Scene EnsureScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.isLoaded && scene.path == ScenePath) return scene;

        if (System.IO.File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, ScenePath);
        return newScene;
    }

    private static void WirePrefabReferences()
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zombie.prefab");

        var mgr = Object.FindFirstObjectByType<NetworkGameManager>();
        if (mgr != null) mgr.playerPrefab = playerPrefab;

        var spawner = Object.FindFirstObjectByType<ZombieSpawner>();
        if (spawner != null) spawner.zombiePrefab = zombiePrefab;
    }

    private static void AddToBuildSettings(Scene scene)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string path = scene.path;
        if (scenes.Find(s => s.path == path) == null)
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
