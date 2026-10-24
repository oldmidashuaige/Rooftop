using UnityEngine;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;

/// <summary>程序化测试场景用材质集合。</summary>
public class DemoMaterials
{
    public Material ground;
    public Material wall;
    public Material crate;
    public Material player;
    public Material gun;
    public Material zombie;
    public Material zombieHead;
    public Material tracer;
    public Material spark;
}

/// <summary>
/// 程序化场景构建器：生成测试世界 + 网络管理器 + 总控 + 刷怪器。
/// 供编辑器工具（Build Game Scene）调用；所有生成物带 DemoObject 标记以便清理。
/// </summary>
public static class SceneBuilder
{
    public static void BuildWorld(DemoMaterials mats)
    {
        NewBox("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(80f, 0.5f, 80f), mats.ground);

        NewBox("Wall_North", new Vector3(0f, 1.5f, 40f), new Vector3(80f, 3f, 1f), mats.wall);
        NewBox("Wall_South", new Vector3(0f, 1.5f, -40f), new Vector3(80f, 3f, 1f), mats.wall);
        NewBox("Wall_East", new Vector3(40f, 1.5f, 0f), new Vector3(1f, 3f, 80f), mats.wall);
        NewBox("Wall_West", new Vector3(-40f, 1.5f, 0f), new Vector3(1f, 3f, 80f), mats.wall);

        NewBox("Crate_A", new Vector3(-5f, 0.75f, 10f), new Vector3(1.5f, 1.5f, 1.5f), mats.crate);
        NewBox("Crate_B", new Vector3(5f, 0.75f, 14f), new Vector3(1.5f, 1.5f, 1.5f), mats.crate);
        NewBox("Crate_C", new Vector3(-1f, 0.5f, 22f), new Vector3(1f, 1f, 1f), mats.crate);
        NewBox("Crate_D", new Vector3(9f, 1f, 26f), new Vector3(2f, 2f, 2f), mats.crate);
        NewBox("Crate_E", new Vector3(-14f, 1f, -6f), new Vector3(1.8f, 2f, 1.8f), mats.crate);

        NewBox("Cover_Low", new Vector3(-10f, 0.5f, 2f), new Vector3(4f, 1f, 0.6f), mats.wall);
        NewBox("Cover_Tall", new Vector3(10f, 1.5f, 4f), new Vector3(0.6f, 3f, 4f), mats.wall);

        EnsureMenuCamera();
        CreateManagers();
    }

    private static void CreateManagers()
    {
        var netGo = new GameObject("NetworkManager");
        Mark(netGo);
        var nm = netGo.AddComponent<NetworkManager>();
        var tug = netGo.AddComponent<Tugboat>();
        tug.SetPort(7770);
        tug.SetMaximumClients(10);
        netGo.AddComponent<ProtocolAuthenticator>();

        var mgrGo = new GameObject("NetworkGameManager");
        Mark(mgrGo);
        var mgr = mgrGo.AddComponent<NetworkGameManager>();
        mgr.spawnPoints = new[]
        {
            new Vector3(0f, 0.2f, -12f),
            new Vector3(-6f, 0.2f, -10f),
            new Vector3(6f, 0.2f, -10f),
            new Vector3(0f, 0.2f, -8f),
            new Vector3(-3f, 0.2f, -6f),
            new Vector3(3f, 0.2f, -6f)
        };

        var spGo = new GameObject("ZombieSpawner");
        Mark(spGo);
        var sp = spGo.AddComponent<ZombieSpawner>();
        sp.spawnPoints = new[]
        {
            new Vector3(0f, 0f, 14f),
            new Vector3(-10f, 0f, 20f),
            new Vector3(10f, 0f, 24f),
            new Vector3(-6f, 0f, 30f),
            new Vector3(7f, 0f, 34f)
        };
    }

    public static void Clear()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            bool underDemo = cam.transform.parent != null &&
                             cam.transform.parent.GetComponentInParent<DemoObject>() != null;

            var gun = cam.GetComponent<Gun>();
            if (gun != null) Object.DestroyImmediate(gun);
            var hud = cam.GetComponent<GameHUD>();
            if (hud != null) Object.DestroyImmediate(hud);

            var gunGo = cam.transform.Find("Gun");
            if (gunGo != null) Object.DestroyImmediate(gunGo.gameObject);

            if (underDemo)
            {
                cam.transform.SetParent(null, false);
                cam.transform.position = new Vector3(0f, 1f, -10f);
                cam.transform.rotation = Quaternion.identity;
            }
        }

        var markers = Object.FindObjectsByType<DemoObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in markers) Object.DestroyImmediate(m.gameObject);

        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
            if (c != null && (c.GetComponent<MainMenuUI>() != null || c.GetComponent<GameHUD>() != null))
                Object.DestroyImmediate(c.gameObject);
    }

    private static void EnsureMenuCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
        }

        // 菜单相机不挂 AudioListener（玩家预制体相机自带，避免双监听刷屏）
        var al = cam.GetComponent<AudioListener>();
        if (al != null) Object.DestroyImmediate(al);

        cam.enabled = true;
        cam.transform.SetParent(null, false);
        cam.transform.position = new Vector3(0f, 1f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
    }

    private static GameObject NewBox(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Mark(go);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static void Mark(GameObject go) => MarkLocal(go);

    public static void MarkLocal(GameObject go)
    {
        if (go.GetComponent<DemoObject>() == null) go.AddComponent<DemoObject>();
    }
}
