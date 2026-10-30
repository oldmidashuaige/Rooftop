using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;

/// <summary>
/// 网络总控：命令行解析、开服/建房/加入、连接事件、玩家生成/销毁、64 tick、自动测试。
/// 一份构建三角色：-server（专用服务器）/ -host（房主）/ -join IP（客户端）。
/// 可选：-autotest（客户端自动移动/开枪，压测用）、-role human|zombie（阶段二角色预选）。
/// </summary>
public class NetworkGameManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public Vector3[] spawnPoints = { new Vector3(0f, 0.2f, -8f) };

    public const ushort DefaultPort = 7770;
    private ushort portOverride;
    private bool hasPortOverride;
    public static NetworkGameManager Instance { get; private set; }

    private readonly Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();
    private int nextSpawnIndex;
    private bool autoTest;
    private bool autoStart;
    private string requestedRole = "auto";
    private bool trainingMode;
    private int requestedPlayers = -1;

    public IReadOnlyCollection<GameObject> Players => players.Values;
    public bool AutoStart => autoStart;
    public bool TrainingMode => trainingMode;
    /// <summary>-players N：专用服务器无房主 UI，用它等价设置总玩家数（2~9）。</summary>
    public int RequestedPlayers => requestedPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        Debug.Log("[Net] NetworkGameManager.Start; NM=" + (InstanceFinder.NetworkManager != null ? "yes" : "NO"));

        try
        {
            // 应用启动设置：窗口模式
            ApplyWindowModeSetting();

            InstanceFinder.TimeManager.SetTickRate(64);

            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteState;
            InstanceFinder.ClientManager.OnClientConnectionState += HandleClientState;

            ParseCommandLine();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Net] Start exception: " + e);
        }
    }

    public static void ApplyWindowModeSetting()
    {
        switch (SettingsStore.S.windowMode)
        {
            case 0: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            case 1: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
            case 2: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
        }
    }

    /// <summary>玩家出生后禁用菜单相机，让 Camera.main 指向玩家相机（交互射线从玩家视角发出）。</summary>
    public void OnLocalPlayerSpawned()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c.GetComponentInParent<NetworkObject>() != null) continue; // 玩家相机保留
            if (c.CompareTag("MainCamera")) // 菜单相机
            {
                c.enabled = false;
                var al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
        InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientState;
    }

    /// <summary>
    /// 应用退出前优雅停机：此时所有对象还活着，先停服务器/客户端连接，
    /// 让 FishNet 的停止回调干净执行（遍历 Spawned/NetworkBehaviours），
    /// 避免随后 Tugboat.OnDestroy→Shutdown 时遍历已被销毁的对象抛 NullReferenceException。
    /// </summary>
    private void OnApplicationQuit()
    {
        if (InstanceFinder.NetworkManager == null) return;
        try
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.StopConnection(false);
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.StopConnection();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Net] 退出清理异常（可忽略）: " + e.Message);
        }
    }

    private void ParseCommandLine()
    {
        var args = System.Environment.GetCommandLineArgs();
        Debug.Log("[Net] Args: " + string.Join(" ", args));

        // 第一遍：先扫描所有标记（-autotest / -role / -autostart / -port / -train），再决定动作
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "-autotest") autoTest = true;
            else if (a == "-autostart") autoStart = true;
            else if (a == "-train") trainingMode = true;
            else if (a == "-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out var p))
            {
                portOverride = p;
                hasPortOverride = true;
                i++;
            }
            else if (a == "-role" && i + 1 < args.Length) { requestedRole = args[i + 1].ToLowerInvariant(); i++; }
            else if (a == "-players" && i + 1 < args.Length && int.TryParse(args[i + 1], out var n))
            {
                requestedPlayers = Mathf.Clamp(n, 2, 9);
                i++;
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            switch (a)
            {
                case "-server":
                case "--server":
                    StartDedicatedServer();
                    return;
                case "-train":
                case "--train":
                    StartTraining();
                    return;
                case "-host":
                case "--host":
                    HostGame();
                    return;
                case "-join":
                case "--join":
                    string ip = "127.0.0.1";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) { ip = args[i + 1]; i++; }
                    JoinGame(ip, EffectivePort());
                    return;
            }
        }

        if (autoTest) StartCoroutine(EnableAutoTest());
    }

    /// <summary>训练场：复用房主开服代码（服务器+客户端），跳过选角由 GameFlow 直接开局。</summary>
    public void StartTraining()
    {
        trainingMode = true;
        Debug.Log("[Net] 训练场模式启动（房主=服务器）");
        HostGame();
    }

    /// <summary>
    /// 退出到主菜单（含关闭训练场）：停服务器+客户端、重置 GameFlow 状态、解锁鼠标。
    /// 必须重置 GameFlow，否则再进训练场 state 仍为 Playing，玩家不会生成、鼠标无法锁定。
    /// </summary>
    public void DisconnectToMenu()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm != null)
        {
            if (nm.IsServerStarted) nm.ServerManager.StopConnection(false);
            if (nm.IsClientStarted) nm.ClientManager.StopConnection();
        }
        var flow = FindObjectOfType<GameFlow>();
        if (flow != null) flow.ResetToLobby();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[Net] 已退出到主菜单（训练场服务器已结束）");
    }

    private ushort EffectivePort() => hasPortOverride ? portOverride : DefaultPort;

    public void HostGame()
    {
        var t = InstanceFinder.TransportManager.Transport;
        t.SetClientAddress("127.0.0.1");
        t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
        ushort port = EffectivePort();
        t.SetPort(port);
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
        if (autoTest) StartCoroutine(EnableAutoTest());
        Debug.Log($"[Net] 房主模式启动中（端口 {port}）…");
    }

    public void JoinGame(string ip, ushort port)
    {
        var t = InstanceFinder.TransportManager.Transport;
        t.SetClientAddress(ip.Trim());
        t.SetPort(port);
        InstanceFinder.ClientManager.StartConnection();
        if (autoTest) StartCoroutine(EnableAutoTest());
        Debug.Log($"[Net] 连接 {ip}:{port} …");
    }

    public void StartDedicatedServer()
    {
        Debug.Log("[Net] StartDedicatedServer");
        var t = InstanceFinder.TransportManager.Transport;
        t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
        ushort port = EffectivePort();
        t.SetPort(port);
        bool ok = InstanceFinder.ServerManager.StartConnection();
        Debug.Log($"[Net] 专用服务器运行中（端口 {port}）(ok={ok})；若端口被占用，FishNet 会打印 Bind exception，请关闭占用进程或用 -port 换端口");
        Application.targetFrameRate = 60;
    }

    private IEnumerator EnableAutoTest()
    {
        yield return new WaitForSeconds(4f);
        FirstPersonController.AutoMove = true;
        Gun.AutoFire = true;
        Debug.Log("[Net] 自动测试输入已启用 (role=" + requestedRole + ")");
    }

    private void HandleRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            conn.OnLoadedStartScenes += HandleLoadedStartScenes;
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            conn.OnLoadedStartScenes -= HandleLoadedStartScenes;
            DespawnPlayer(conn);
            var flow = FindObjectOfType<GameFlow>();
            if (flow != null) flow.HandleClientLeft(conn);
        }
    }

    private void HandleLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer) return;
        conn.OnLoadedStartScenes -= HandleLoadedStartScenes;
        var flow = FindObjectOfType<GameFlow>();
        if (flow != null) flow.HandleClientReady(conn);
        else Debug.LogWarning("[Net] 未找到 GameFlow，客户端不会出生");
    }

    private void HandleClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            Debug.Log("[Net] 客户端已连接，等待认证/出生…");
        else if (args.ConnectionState == LocalConnectionState.Stopped)
            Debug.Log("[Net] 客户端已断开（若为版本不匹配请更新构建）");
    }

    /// <summary>由 GameFlow 在开局时按角色生成玩家。</summary>
    public void SpawnPlayerWithRole(NetworkConnection conn, HumanRole role, bool isZombie, Vector3 pos)
    {
        if (playerPrefab == null) { Debug.LogError("[Net] playerPrefab 未赋值"); return; }

        var go = Instantiate(playerPrefab, pos, Quaternion.identity);
        var rc = go.GetComponent<RoleComponent>();
        if (rc != null) rc.Initialize(role, isZombie);

        var no = go.GetComponent<NetworkObject>();
        if (no != null) InstanceFinder.ServerManager.Spawn(no, conn);
        players[conn.ClientId] = go;
        Debug.Log($"[Net] 开局生成 客户端{conn.ClientId} role={(isZombie ? "ZOMBIE" : role.ToString())} at {pos} (总数 {players.Count})");
    }

    public void DespawnPlayer(NetworkConnection conn)
    {
        if (players.TryGetValue(conn.ClientId, out var go) && go != null)
        {
            var no = go.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) InstanceFinder.ServerManager.Despawn(no);
        }
        players.Remove(conn.ClientId);
    }
}
