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
    public static NetworkGameManager Instance { get; private set; }

    private readonly Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();
    private int nextSpawnIndex;
    private bool autoTest;
    private bool autoStart;
    private string requestedRole = "auto";

    public IReadOnlyCollection<GameObject> Players => players.Values;
    public bool AutoStart => autoStart;

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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteState;
        InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientState;
    }

    private void ParseCommandLine()
    {
        var args = System.Environment.GetCommandLineArgs();
        Debug.Log("[Net] Args: " + string.Join(" ", args));

        // 第一遍：先扫描所有标记（-autotest / -role / -autostart），再决定动作，避免 -join 提前 return 漏掉后续参数
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "-autotest") autoTest = true;
            else if (a == "-autostart") autoStart = true;
            else if (a == "-role" && i + 1 < args.Length) { requestedRole = args[i + 1].ToLowerInvariant(); i++; }
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
                case "-host":
                case "--host":
                    HostGame();
                    return;
                case "-join":
                case "--join":
                    string ip = "127.0.0.1";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) { ip = args[i + 1]; i++; }
                    JoinGame(ip, DefaultPort);
                    return;
            }
        }

        if (autoTest) StartCoroutine(EnableAutoTest());
    }

    public void HostGame()
    {
        var t = InstanceFinder.TransportManager.Transport;
        t.SetClientAddress("127.0.0.1");
        t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
        t.SetPort(DefaultPort);
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
        if (autoTest) StartCoroutine(EnableAutoTest());
        Debug.Log("[Net] 房主模式启动中…");
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
        t.SetPort(DefaultPort);
        bool ok = InstanceFinder.ServerManager.StartConnection();
        Debug.Log("[Net] ServerManager.StartConnection returned " + ok);
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
