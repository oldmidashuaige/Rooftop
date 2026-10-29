using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FishNet.Editing;
using FishNet.Object;
using FishNet.Component.Transforming;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器工具：烘焙网络预制体（NetworkPlayer / Zombie）+ 材质。
/// 完成后自动调用 FishNet 的 Refresh Default Prefabs 注册到 DefaultPrefabObjects。
/// </summary>
public static class BuildPrefabs
{
    private const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Tools/Build Network Prefabs")]
    public static void Build()
    {
        MaterialAssets.EnsureFolder(PrefabsFolder);

        var mats = MaterialAssets.Ensure();
        BuildPlayerPrefab(mats);
        BuildZombiePrefab(mats);

        // 注册到 FishNet DefaultPrefabObjects（否则 Spawn 时报 AssetPathHash is not set）
        RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("网络预制体已烘焙并刷新 Default Prefabs。");
    }

    private static void BuildPlayerPrefab(DemoMaterials mats)
    {
        var root = new GameObject("NetworkPlayer");

        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        SetClientAuthoritative(nt);

        root.AddComponent<FirstPersonController>();
        root.AddComponent<PlayerHealth>();
        root.AddComponent<RoleComponent>();
        root.AddComponent<InfectionSystem>();
        root.AddComponent<HumanAbilities>();
        root.AddComponent<PlayerZombie>();
        root.AddComponent<SpectatorLink>();
        root.AddComponent<NameTag>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = mats.player;

        // 丧尸形态的头部（供爆头判定；人类隐藏）
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        head.transform.localScale = Vector3.one * 0.45f;
        head.GetComponent<MeshRenderer>().sharedMaterial = mats.zombieHead;
        head.GetComponent<Collider>().isTrigger = true;

        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        camGo.transform.localRotation = Quaternion.identity;
        camGo.tag = "MainCamera"; // 玩家相机设为主相机，Camera.main 才能指向它
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        var gun = camGo.AddComponent<Gun>();
        camGo.AddComponent<GameHUD>();

        BuildGunModel(camGo.transform, mats.gun);
        gun.tracerMaterial = mats.tracer;
        gun.sparkMaterial = mats.spark;

        var fpc = root.GetComponent<FirstPersonController>();
        fpc.cameraTransform = camGo.transform;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabsFolder + "/NetworkPlayer.prefab");
        Object.DestroyImmediate(root);
    }

    private static void BuildGunModel(Transform camera, Material mat)
    {
        var gunRoot = new GameObject("GunModel");
        gunRoot.transform.SetParent(camera, false);
        gunRoot.transform.localPosition = new Vector3(0.26f, -0.22f, 0.42f);
        gunRoot.transform.localRotation = Quaternion.identity;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(gunRoot.transform, false);
        body.transform.localScale = new Vector3(0.08f, 0.09f, 0.30f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(gunRoot.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0.015f, 0.22f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(0.035f, 0.10f, 0.035f);
        barrel.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(barrel.GetComponent<Collider>());

        var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(gunRoot.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.09f, -0.09f);
        grip.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        grip.transform.localScale = new Vector3(0.07f, 0.14f, 0.08f);
        grip.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(grip.GetComponent<Collider>());
    }

    private static void BuildZombiePrefab(DemoMaterials mats)
    {
        var root = new GameObject("Zombie");
        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        SetServerAuthoritative(nt);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mats.zombie;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        head.transform.localScale = Vector3.one * 0.55f;
        head.GetComponent<MeshRenderer>().sharedMaterial = mats.zombieHead;

        root.AddComponent<SimpleZombie>();
        root.AddComponent<HatredBrain>();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabsFolder + "/Zombie.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SetServerAuthoritative(NetworkTransform nt)
    {
        var so = new SerializedObject(nt);
        var prop = so.FindProperty("_clientAuthoritative");
        if (prop != null) prop.boolValue = false;
        so.ApplyModifiedProperties();
    }

    private static void SetClientAuthoritative(NetworkTransform nt)
    {
        var so = new SerializedObject(nt);
        var prop = so.FindProperty("_clientAuthoritative");
        if (prop != null) prop.boolValue = true;
        so.ApplyModifiedProperties();
    }
}
