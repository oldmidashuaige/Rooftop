using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Connection;
using FishNet.Object;

/// <summary>
/// 玩家丧尸能力（服务器权威判定）：
/// - 标记（需吃回自己尸体后恢复；CD）：对技能范围内人机丧尸把目标仇恨推到标记值 → 集火
/// - 近战：攻击人类 → 触发感染
/// - 啃尸：吃人类尸体 → 该观战玩家转玩家丧尸；吃自己的尸体 → 恢复标记能力
/// - 被击杀：原对象变"自己的尸体"，随即以弱化玩家丧尸（无标记）在随机人机位置重生
/// </summary>
public class PlayerZombie : NetworkBehaviour
{
    public float meleeRange = 2f;

    private GameConfig cfg;
    private float markCooldownTimer;
    private bool enabledByRole;

    private void Awake()
    {
        cfg = GameConfig.Load();
    }

    public void SetEnabled(bool on)
    {
        enabledByRole = on;
        if (on && IsOwner)
        {
            PlayerZombieUI.Ensure();
            PlayerZombieUI.Instance.Show();
        }
        else if (!on && IsOwner && PlayerZombieUI.Instance != null)
        {
            PlayerZombieUI.Instance.Hide();
        }
    }

    private void Update()
    {
        if (!IsOwner || !enabledByRole) return;

        var rc = GetComponent<RoleComponent>();
        if (rc != null && rc.IsStunned) return; // 紫外灯僵直

        if (markCooldownTimer > 0f)
        {
            markCooldownTimer -= Time.deltaTime;
            if (PlayerZombieUI.Instance != null) PlayerZombieUI.Instance.RefreshMark(markCooldownTimer, cfg.markCooldown);
        }

        if (Keyboard.current == null) return;

        // F = 标记当前视野内目标
        if (Keyboard.current.fKey.wasPressedThisFrame)
            TryMark();

        // 左键 = 近战感染（视野内最近人类）
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryMelee();

        // E = 啃尸体（视野内最近尸体）
        if (Keyboard.current.eKey.wasPressedThisFrame)
            TryEat();
    }

    private void TryMark()
    {
        var target = FindLookTarget(false);
        if (target == null) return;
        if (markCooldownTimer > 0f) return;
        markCooldownTimer = cfg.markCooldown;
        CmdRequestMark(target.Owner.ClientId);
    }

    private void TryMelee()
    {
        var target = FindLookTarget(true);
        if (target == null) return;
        CmdRequestMelee(target.Owner.ClientId);
    }

    private void TryEat()
    {
        var corpse = FindLookTarget(false, true);
        if (corpse == null) return;
        CmdRequestEatCorpse(corpse.NetworkObject);
    }

    private RoleComponent FindLookTarget(bool humanOnly, bool corpseOnly = false)
    {
        if (Camera.main == null) return null;
        var cam = Camera.main.transform;
        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, meleeRange + 20f)) return null;
        var rc = hit.collider.GetComponentInParent<RoleComponent>();
        if (rc == null || rc.IsCorpse != corpseOnly) return null;
        if (corpseOnly) return rc;
        if (humanOnly && rc.IsZombie) return null;
        if (!humanOnly && !rc.IsZombie) return null;
        return rc;
    }

    // ============ 服务器 ============

    [ServerRpc]
    private void CmdRequestMark(int targetClientId)
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !rc.IsZombie || rc.IsCorpse || !rc.CanMark) return;

        RoleComponent target = null;
        foreach (var p in FindObjectsByType<RoleComponent>())
            if (p.Owner != null && p.Owner.ClientId == targetClientId) { target = p; break; }
        if (target == null) return;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > cfg.markRange) return;

        Debug.Log($"[Zombie] 玩家丧尸标记 客户端 {targetClientId}");
        foreach (var brain in FindObjectsByType<HatredBrain>())
        {
            if (Vector3.Distance(transform.position, brain.transform.position) <= cfg.markRange)
                brain.MarkPlayer(targetClientId, cfg.markHatredBoost);
        }
    }

    [ServerRpc]
    private void CmdRequestMelee(int targetClientId)
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !rc.IsZombie || rc.IsCorpse) return;

        RoleComponent target = null;
        foreach (var p in FindObjectsByType<RoleComponent>())
            if (p.Owner != null && p.Owner.ClientId == targetClientId) { target = p; break; }
        if (target == null || target.IsZombie || target.IsCorpse) return;
        if (Vector3.Distance(transform.position, target.transform.position) > meleeRange) return;

        var inf = target.GetComponent<InfectionSystem>();
        if (inf != null) inf.TryInfect();
    }

    [ServerRpc]
    private void CmdRequestEatCorpse(NetworkObject corpseNo)
    {
        var eater = GetComponent<RoleComponent>();
        if (eater == null || !eater.IsZombie || eater.IsCorpse) return;
        if (corpseNo == null) return;

        var corpse = corpseNo.GetComponent<RoleComponent>();
        if (corpse == null || !corpse.IsCorpse) return;
        if (Vector3.Distance(transform.position, corpse.transform.position) > meleeRange + 0.5f) return;

        if (corpse.CorpseWasZombie)
        {
            // 吃自己的尸体 → 恢复标记能力
            if (corpse.CorpseOwnerClientId == Owner.ClientId)
            {
                eater.RestoreMark();
                corpseNo.Despawn();
                Debug.Log("[Zombie] 吃回自己的尸体，恢复标记能力");
            }
        }
        else
        {
            // 吃人类尸体 → 该观战玩家转玩家丧尸（同对象切换）
            corpse.ConvertToZombie(false);
            Debug.Log("[Zombie] 啃食人类尸体，观战玩家转化为玩家丧尸");
        }
    }

    // ============ 玩家丧尸被击杀 → 弱化重生 ============

    /// <summary>服务器：玩家丧尸死亡。原对象变"自己的尸体"，随后在随机人机处弱化重生（无标记）。</summary>
    public static void OnPlayerZombieKilled(RoleComponent rc, NetworkConnection conn)
    {
        if (rc == null) return;
        rc.BecomeOwnCorpse();

        Vector3 pos = FindSpawnNearHumanoid();
        if (pos == Vector3.zero) pos = new Vector3(0f, 0.2f, 0f);

        var nm = UnityEngine.Object.FindObjectOfType<NetworkGameManager>();
        if (nm == null) return;
        var go = UnityEngine.Object.Instantiate(nm.playerPrefab, pos, Quaternion.identity);
        var newRc = go.GetComponent<RoleComponent>();
        if (newRc != null) newRc.InitializeWeakZombie();
        var no = go.GetComponent<NetworkObject>();
        if (no != null) FishNet.InstanceFinder.ServerManager.Spawn(no, conn);
        Debug.Log($"[Zombie] 玩家丧尸 {conn.ClientId} 被击杀，弱化重生 at {pos}（吃回尸体恢复标记）");
    }

    private static Vector3 FindSpawnNearHumanoid()
    {
        var humanoids = FindObjectsByType<SimpleZombie>(FindObjectsInactive.Exclude);
        foreach (var h in humanoids)
        {
            if (h.IsDead) continue;
            return h.transform.position + new Vector3(Random.Range(-1.5f, 1.5f), 0.2f, Random.Range(-1.5f, 1.5f));
        }
        return Vector3.zero;
    }
}
