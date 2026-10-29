using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;

/// <summary>
/// 回合总控（服务器权威）：大厅名单与角色分配 → 开局 → 进行 → 撤离 → 结算。
/// 规则：8 名人类角色池选 5 人类 + 1 玩家丧尸开局；撤离 3 座先到先得、30s 倒计时；
/// 结算——有人登机⇒登机者胜/未登机者败/丧尸败；无人登机⇒丧尸胜。
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum RoundState { Lobby, Playing, Evacuation, Ended }

    [Serializable] public struct RolePickBroadcast : IBroadcast { public bool isZombie; public HumanRole role; }
    [Serializable] public struct ForceStartBroadcast : IBroadcast { public bool dummy; }
    [Serializable] public struct LobbyEntryData : IBroadcast { public int clientId; public HumanRole role; public bool isZombie; }
    [Serializable] public struct LobbyStateBroadcast : IBroadcast { public LobbyEntryData[] entries; public int humanSlots; public int zombieSlots; public bool started; }
    [Serializable] public struct RoundStateBroadcast : IBroadcast { public int state; public float countdown; public int seatsTaken; public int seatsTotal; }

    private class LobbyEntry
    {
        public NetworkConnection conn;
        public HumanRole role = HumanRole.None;
        public bool isZombie;
        public bool ready; // 场景加载完成
    }

    public GameConfig config;
    public Vector3[] humanSpawnPoints;
    public Vector3 officeSpawnPoint;
    public Vector3 zombieSpawnPoint;

    private readonly Dictionary<int, LobbyEntry> lobby = new Dictionary<int, LobbyEntry>();
    private RoundState state = RoundState.Lobby;
    private float evacuateTimer;
    private int seatsTaken;
    private readonly HashSet<int> boarded = new HashSet<int>();
    private float endTimer;
    private string resultText = "";
    private float lobbyFullTime = -1f;

    public static GameFlow Instance { get; private set; }
    public RoundState State => state;

    public bool IsBoarded(int clientId) => boarded.Contains(clientId);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (config == null) config = GameConfig.Load();
    }

    private void Start()
    {
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.RegisterBroadcast<RolePickBroadcast>(HandleRolePick, false);
        InstanceFinder.ServerManager.RegisterBroadcast<ForceStartBroadcast>(HandleForceStart, false);
        InstanceFinder.ClientManager.RegisterBroadcast<LobbyStateBroadcast>(HandleLobbyState);
        InstanceFinder.ClientManager.RegisterBroadcast<RoundStateBroadcast>(HandleRoundState);
        InstanceFinder.ClientManager.RegisterBroadcast<ResultBroadcast>(HandleResult);
        if (InstanceFinder.IsServerStarted && NetworkGameManager.Instance != null && NetworkGameManager.Instance.AutoStart)
            StartCoroutine(AutoStartLoop());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.UnregisterBroadcast<RolePickBroadcast>(HandleRolePick);
        InstanceFinder.ServerManager.UnregisterBroadcast<ForceStartBroadcast>(HandleForceStart);
        InstanceFinder.ClientManager.UnregisterBroadcast<LobbyStateBroadcast>(HandleLobbyState);
        InstanceFinder.ClientManager.UnregisterBroadcast<RoundStateBroadcast>(HandleRoundState);
        InstanceFinder.ClientManager.UnregisterBroadcast<ResultBroadcast>(HandleResult);
    }

    // ============ 客户端连接 / 断开 ============

    public void HandleClientReady(NetworkConnection conn)
    {
        // 训练场：房主本地玩家就绪即直接开局（跳过选角大厅）
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.TrainingMode && state == RoundState.Lobby)
        {
            lobby[conn.ClientId] = new LobbyEntry { conn = conn, ready = true, isZombie = false, role = HumanRole.Student };
            Debug.Log("[Flow] 训练场：直接开局");
            StartTrainingRound();
            return;
        }

        if (!lobby.ContainsKey(conn.ClientId))
            lobby[conn.ClientId] = new LobbyEntry { conn = conn };
        lobby[conn.ClientId].ready = true;
        Debug.Log($"[Flow] 客户端 {conn.ClientId} 就绪 (lobby={lobby.Count})");
        if (InstanceFinder.IsServerStarted) BroadcastLobbyState();
        MaybeAutoStart();
    }

    /// <summary>训练场：为本机玩家生成角色（学生），并保证有人机丧尸可练手。</summary>
    private void StartTrainingRound()
    {
        if (state != RoundState.Lobby) return;
        state = RoundState.Playing;
        lobbyFullTime = -1f;
        boarded.Clear();
        seatsTaken = 0;
        resultText = "";
        Debug.Log("[Flow] 训练场开局！");

        try
        {
            foreach (var kv in lobby)
            {
                var nm = NetworkGameManager.Instance;
                if (nm == null) continue;
                nm.SpawnPlayerWithRole(kv.Value.conn, HumanRole.Student, false, PickSpawn(kv.Value));
            }
            // 训练场刷几只人机丧尸供练手
            var spawner = FindObjectOfType<ZombieSpawner>();
            if (spawner != null) spawner.maxZombies = 4;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Flow] 训练场开局异常: {e}");
        }
        BroadcastRoundState();
        BroadcastLobbyState();
    }

    public void HandleClientLeft(NetworkConnection conn)
    {
        lobby.Remove(conn.ClientId);
        boarded.Remove(conn.ClientId);
        // 没人了 → 回大厅（含训练场退出：保证再次进入能正常开局）
        if (lobby.Count == 0 && state != RoundState.Lobby)
            ResetToLobby();
        if (InstanceFinder.IsServerStarted) BroadcastLobbyState();
    }

    /// <summary>重置回大厅：清空名单/状态，保证退出训练场后再次进入能重新开局。</summary>
    public void ResetToLobby()
    {
        state = RoundState.Lobby;
        lobbyFullTime = -1f;
        boarded.Clear();
        seatsTaken = 0;
        resultText = "";
        foreach (var kv in lobby)
        {
            kv.Value.role = HumanRole.None;
            kv.Value.isZombie = false;
            kv.Value.ready = false;
        }
        lobby.Clear();
        Debug.Log("[Flow] 已重置回大厅");
        if (InstanceFinder.IsServerStarted) BroadcastLobbyState();
    }

    private IEnumerator AutoStartLoop()
    {
        while (state == RoundState.Lobby)
        {
            yield return new WaitForSeconds(0.5f);
            MaybeAutoStart();
        }
    }

    private void MaybeAutoStart()
    {
        if (state != RoundState.Lobby) return;
        if (!InstanceFinder.IsServerStarted) return;

        int need = config.humanSlots + config.zombieSlots;
        if (lobby.Count < need) return;

        var nm = NetworkGameManager.Instance;
        if (nm != null && nm.AutoStart)
        {
            // -autostart：满员立即自动分配开局
            AutoAssignRoles();
            StartRound();
            return;
        }

        // 无 -autostart：满员后给玩家一点选角时间，超时仍自动分配（防"人数已满等待开局"卡死）
        if (lobbyFullTime < 0f) lobbyFullTime = Time.time;
        if (Time.time - lobbyFullTime > config.lobbyAutoStartDelay)
        {
            AutoAssignRoles();
            StartRound();
        }
    }

    private void AutoAssignRoles()
    {
        int human = 0;
        foreach (var kv in lobby)
        {
            if (human < config.humanSlots)
            {
                kv.Value.isZombie = false;
                kv.Value.role = RoleDef.HumanPool[human % RoleDef.HumanPool.Length];
                human++;
            }
            else
            {
                kv.Value.isZombie = true;
                kv.Value.role = HumanRole.None;
            }
        }
    }

    // ============ 角色选择（大厅 UI → 服务器）============

    private void HandleRolePick(NetworkConnection conn, RolePickBroadcast msg, Channel channel)
    {        if (state != RoundState.Lobby) return;
        if (!lobby.TryGetValue(conn.ClientId, out var e)) return;

        if (msg.isZombie)
        {
            if (IsZombieTaken()) return;
            e.isZombie = true;
            e.role = HumanRole.None;
        }
        else
        {
            if (!IsRoleAvailable(msg.role)) return;
            e.isZombie = false;
            e.role = msg.role;
        }
        Debug.Log($"[Flow] 客户端 {conn.ClientId} 选角 {e.role} zombie={msg.isZombie}");
        BroadcastLobbyState();
        TryStartWhenFull();
    }

    /// <summary>任何客户端请求"立即开始"：未满员也开局（测试/调试用），未选角者自动分配。</summary>
    private void HandleForceStart(NetworkConnection conn, ForceStartBroadcast msg, Channel channel)
    {
        if (state != RoundState.Lobby) return;
        if (!InstanceFinder.IsServerStarted) return;
        if (lobby.Count == 0) return;
        Debug.Log($"[Flow] 客户端 {conn.ClientId} 请求立即开始");
        AutoAssignRoles();
        StartRound();
    }

    private int RoleCount(HumanRole role)
    {
        int n = 0;
        foreach (var kv in lobby)
            if (!kv.Value.isZombie && kv.Value.role == role) n++;
        return n;
    }

    private static int RoleLimit(HumanRole role) => role == HumanRole.Student ? 4 : 1;

    private bool IsRoleAvailable(HumanRole role) => RoleCount(role) < RoleLimit(role);

    private bool IsZombieTaken()
    {
        foreach (var kv in lobby)
            if (kv.Value.isZombie) return true;
        return false;
    }

    private void TryStartWhenFull()
    {
        if (state != RoundState.Lobby) return;
        if (!InstanceFinder.IsServerStarted) return;

        int humans = 0, zombies = 0;
        foreach (var kv in lobby)
        {
            if (kv.Value.role == HumanRole.None && !kv.Value.isZombie) return; // 有人未选
            if (kv.Value.isZombie) zombies++;
            else humans++;
        }
        if (humans == config.humanSlots && zombies == config.zombieSlots)
            StartRound();
    }

    // ============ 开局 ============

    private void StartRound()
    {
        if (state != RoundState.Lobby) return;
        state = RoundState.Playing;
        lobbyFullTime = -1f;
        boarded.Clear();
        seatsTaken = 0;
        resultText = "";
        Debug.Log("[Flow] 开局！");

        try
        {
            foreach (var kv in lobby)
            {
                var conn = kv.Value.conn;
                var nm = NetworkGameManager.Instance;
                Vector3 pos = PickSpawn(kv.Value);
                if (nm != null) nm.SpawnPlayerWithRole(conn, kv.Value.role, kv.Value.isZombie, pos);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Flow] 开局生成玩家异常: {e}");
        }
        BroadcastRoundState();
        BroadcastLobbyState();
    }

    private Vector3 PickSpawn(LobbyEntry e)
    {
        if (e.isZombie) return zombieSpawnPoint;
        if (e.role == HumanRole.OfficeWorker && officeSpawnPoint != Vector3.zero) return officeSpawnPoint;
        if (humanSpawnPoints != null && humanSpawnPoints.Length > 0)
            return humanSpawnPoints[UnityEngine.Random.Range(0, humanSpawnPoints.Length)];
        return new Vector3(0f, 0.2f, -8f);
    }

    // ============ 撤离 ============

    public bool RequestCallExtraction()
    {
        if (state != RoundState.Playing) return false;
        state = RoundState.Evacuation;
        evacuateTimer = config.extractionCountdown;
        Debug.Log($"[Flow] 呼叫救援！撤离倒计时 {evacuateTimer}s");
        BroadcastRoundState();
        return true;
    }

    public void HandleBoarding(int clientId)
    {
        if (state != RoundState.Evacuation) return;
        if (boarded.Contains(clientId)) return;
        if (seatsTaken >= config.extractionSeats) return;

        boarded.Add(clientId);
        seatsTaken++;
        Debug.Log($"[Flow] 客户端 {clientId} 登机 ({seatsTaken}/{config.extractionSeats})");
        BroadcastRoundState();
        if (seatsTaken >= config.extractionSeats) EndRound();
    }

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted) return;

        if (state == RoundState.Lobby)
        {
            // 周期检查：满员超时自动开局；未满员则重置计时
            int need = config.humanSlots + config.zombieSlots;
            if (lobby.Count < need) lobbyFullTime = -1f;
            MaybeAutoStart();
        }

        if (state == RoundState.Evacuation)
        {
            evacuateTimer -= Time.deltaTime;
            if (evacuateTimer <= 0f) EndRound();
        }

        if (state == RoundState.Ended)
        {
            endTimer -= Time.deltaTime;
            if (endTimer <= 0f) BackToLobby();
        }
    }

    private void EndRound()
    {
        if (state == RoundState.Ended) return;
        state = RoundState.Ended;
        endTimer = 15f;

        if (seatsTaken <= 0)
        {
            resultText = "僵尸胜利：无人登机撤离";
            Debug.Log("[Flow] 结果：僵尸胜（无人登机）");
        }
        else
        {
            resultText = $"人类胜：{seatsTaken} 名登机者撤离，未登机者失败";
            Debug.Log($"[Flow] 结果：{seatsTaken} 名登机者胜，其余败，僵尸败");
        }
        BroadcastRoundState();
        BroadcastResult(resultText);
    }

    private void BackToLobby()
    {
        state = RoundState.Lobby;
        foreach (var kv in lobby)
        {
            kv.Value.role = HumanRole.None;
            kv.Value.isZombie = false;
        }
        boarded.Clear();
        seatsTaken = 0;
        BroadcastRoundState();
        BroadcastLobbyState();
        Debug.Log("[Flow] 回到大厅");
        MaybeAutoStart();
    }

    // ============ 广播 ============

    /// <summary>服务器→所有客户端（含 host 本地客户端）。专用服务器时本地连接为 null，只发远端。</summary>
    private void BroadcastAll<T>(T msg) where T : struct, IBroadcast
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm == null) return;
        nm.ServerManager.Broadcast(msg);
        var local = nm.ClientManager.Connection;
        if (local != null && local.IsValid && local.IsActive)
            nm.ServerManager.Broadcast(local, msg);
    }

    private void BroadcastLobbyState()
    {
        var list = new List<LobbyEntryData>();
        foreach (var kv in lobby)
            list.Add(new LobbyEntryData { clientId = kv.Key, role = kv.Value.role, isZombie = kv.Value.isZombie });
        BroadcastAll(new LobbyStateBroadcast
        {
            entries = list.ToArray(),
            humanSlots = config.humanSlots,
            zombieSlots = config.zombieSlots,
            started = state != RoundState.Lobby
        });
    }

    private void BroadcastRoundState()
    {
        BroadcastAll(new RoundStateBroadcast
        {
            state = (int)state,
            countdown = state == RoundState.Evacuation ? evacuateTimer : 0f,
            seatsTaken = seatsTaken,
            seatsTotal = config.extractionSeats
        });
    }

    private void BroadcastResult(string text)
    {
        BroadcastAll(new ResultBroadcast { text = text });
    }

    [Serializable]
    public struct ResultBroadcast : IBroadcast { public string text; }

    // ============ 客户端回调 ============

    private void HandleLobbyState(LobbyStateBroadcast msg, Channel channel)
    {
        LobbyUI.Ensure();
        if (LobbyUI.Instance != null) LobbyUI.Instance.OnLobbyState(msg);
        if (GameHUD.Instance != null) GameHUD.Instance.OnLobbyState(msg);
    }

    private void HandleRoundState(RoundStateBroadcast msg, Channel channel)
    {
        if (GameHUD.Instance != null) GameHUD.Instance.OnRoundState(msg);
    }

    private void HandleResult(ResultBroadcast msg, Channel channel)
    {
        if (GameHUD.Instance != null) GameHUD.Instance.OnResult(msg.text);
    }
}
