using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// 玩家对象核心状态（NetworkPlayer 根）：职业 / 阵营 / 尸体 / 转化。
/// 服务器负责状态转换（死亡→尸体、被啃/感染→玩家丧尸、吃自己尸体→恢复能力）；
/// 客户端据 SyncVar&lt;T&gt; 切换视觉与输入。
/// 转化规则：同客户端转职、同一 NetworkObject 切模式（不重建，规避所有权风险）。
/// </summary>
public class RoleComponent : NetworkBehaviour
{
    private readonly SyncVar<HumanRole> role = new();
    private readonly SyncVar<bool> isZombie = new();
    private readonly SyncVar<bool> isCorpse = new();
    private readonly SyncVar<bool> canMark = new();
    private readonly SyncVar<int> corpseOwnerClientId = new();
    private readonly SyncVar<bool> corpseWasZombie = new();
    private readonly SyncVar<string> displayName = new();

    private float stunTimer;

    public HumanRole Role => role.Value;
    public bool IsZombie => isZombie.Value;
    public bool IsCorpse => isCorpse.Value;
    public bool CanMark => canMark.Value;
    public int CorpseOwnerClientId => corpseOwnerClientId.Value;
    public bool CorpseWasZombie => corpseWasZombie.Value;
    public bool IsStunned => stunTimer > 0f;
    public string DisplayName => displayName.Value;

    private Renderer bodyRenderer;
    private FirstPersonController fpc;
    private Gun gun;
    private Camera ownerCamera;

    // ============ 初始化 ============

    public void Initialize(HumanRole r, bool zombie)
    {
        role.Value = r;
        isZombie.Value = zombie;
        isCorpse.Value = false;
        canMark.Value = zombie; // 初始玩家丧尸可直接标记；被击杀附身后需吃尸体恢复
    }

    /// <summary>弱化重生：玩家丧尸被击杀后在随机人机处重生（无标记，吃回尸体恢复）。</summary>
    public void InitializeWeakZombie()
    {
        role.Value = HumanRole.None;
        isZombie.Value = true;
        isCorpse.Value = false;
        canMark.Value = false;
    }

    private void Awake()
    {
        fpc = GetComponent<FirstPersonController>();
        var bodyT = transform.Find("Body");
        if (bodyT != null) bodyRenderer = bodyT.GetComponent<Renderer>();
        var camT = transform.Find("Camera");
        if (camT != null) ownerCamera = camT.GetComponent<Camera>();

        role.OnChange += OnRoleChanged;
        isZombie.OnChange += OnZombieChanged;
        isCorpse.OnChange += OnCorpseChanged;
        canMark.OnChange += OnCanMarkChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner)
        {
            // 上报登录用户名 → 服务器广播 displayName → 所有客户端头顶显示
            CmdSetDisplayName(SettingsStore.S.username);
        }
        ApplyVisuals();
        ApplyControls();
    }

    [ServerRpc]
    private void CmdSetDisplayName(string name)
    {
        displayName.Value = string.IsNullOrEmpty(name) ? "玩家" : name;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // 玩家退出：清理本地丧尸 UI（迷雾/标记），避免残留到主菜单
        if (IsOwner && PlayerZombieUI.Instance != null)
        {
            PlayerZombieUI.Instance.Hide();
            Destroy(PlayerZombieUI.Instance.gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    private static Color RoleColor(HumanRole r)
    {
        switch (r)
        {
            case HumanRole.OfficeWorker: return new Color(0.35f, 0.55f, 0.70f); // 青
            case HumanRole.WorkerBrother: return new Color(0.85f, 0.55f, 0.20f); // 橙
            case HumanRole.WorkerElder: return new Color(0.80f, 0.30f, 0.25f); // 红
            case HumanRole.Doctor: return new Color(0.55f, 0.75f, 0.45f); // 绿
            default: return new Color(0.20f, 0.35f, 0.65f); // 蓝（学生）
        }
    }

    // ============ SyncVar 回调 ============

    private void OnRoleChanged(HumanRole _, HumanRole __, bool asServer) => ApplyVisuals();
    private void OnZombieChanged(bool _, bool __, bool asServer) { ApplyVisuals(); ApplyControls(); }
    private void OnCorpseChanged(bool _, bool __, bool asServer) { ApplyVisuals(); ApplyControls(); }
    private void OnCanMarkChanged(bool _, bool __, bool asServer)
    {
        if (IsOwner && PlayerZombieUI.Instance != null) PlayerZombieUI.Instance.Refresh();
    }

    private void ApplyVisuals()
    {
        bool corpse = isCorpse.Value;
        bool zombie = isZombie.Value;

        // 自己隐藏 Body（第一人称不显示自己身体）；僵尸/尸体显示
        if (IsOwner && !zombie && !corpse && bodyRenderer != null)
            bodyRenderer.enabled = false;
        else if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
            Color c = corpse ? new Color(0.45f, 0.42f, 0.40f)
                : zombie ? new Color(0.28f, 0.46f, 0.22f) : RoleColor(role.Value);
            bodyRenderer.material.color = c;
        }

        // 丧尸形态显示 Head（供爆头判定），人类隐藏
        var head = transform.Find("Head");
        if (head != null)
        {
            head.gameObject.SetActive(zombie && !corpse);
            if (zombie)
            {
                var hr = head.GetComponent<Renderer>();
                if (hr != null) hr.material.color = corpse ? new Color(0.45f, 0.42f, 0.40f) : new Color(0.38f, 0.55f, 0.28f);
            }
        }

        // 丧尸/尸体不显示枪械模型（玩家丧尸近战，无枪）
        var gunModel = transform.Find("Camera/GunModel");
        if (gunModel != null)
            gunModel.gameObject.SetActive(!zombie && !corpse);

        // 通知 HUD 隐藏/显示准星（丧尸无枪）
        if (IsOwner && GameHUD.Instance != null)
            GameHUD.Instance.SetHasGun(!zombie && !corpse);
    }

    private void ApplyControls()
    {
        bool corpse = isCorpse.Value;
        bool zombie = isZombie.Value;

        if (fpc != null) fpc.SetInputEnabled(!corpse);
        if (gun == null) gun = GetComponentInChildren<Gun>(true);
        if (gun != null) gun.SetEnabled(!corpse && !zombie && IsOwner);
        var human = GetComponent<HumanAbilities>();
        if (human != null) human.SetEnabled(!corpse && !zombie);
        var zombieC = GetComponent<PlayerZombie>();
        if (zombieC != null) zombieC.SetEnabled(!corpse && zombie);

        // 相机：尸体时关闭（交给幽灵观战相机，避免双 AudioListener）
        if (IsOwner && ownerCamera != null)
            ownerCamera.gameObject.SetActive(!corpse);
    }

    // ============ 服务器状态转换 ============

    /// <summary>HP=0 → 尸体（观战等待被啃或回合结束）。</summary>
    [Server]
    public void SetCorpse()
    {
        if (isCorpse.Value) return;
        isCorpse.Value = true;
        corpseOwnerClientId.Value = Owner.ClientId;
        corpseWasZombie.Value = isZombie.Value;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        EnsureCorpseCollider();
        transform.rotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);

        var spectator = GetComponent<SpectatorLink>();
        if (spectator != null) spectator.EnterSpectator(corpseWasZombie.Value);
    }

    /// <summary>尸体需有碰撞体，供玩家丧尸"啃咬尸体"的射线命中。</summary>
    private void EnsureCorpseCollider()
    {
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(0.7f, 1.8f, 0.7f);
        }
    }

    /// <summary>同对象转为玩家丧尸（感染完成 / 尸体被啃）。</summary>
    [Server]
    public void ConvertToZombie(bool resetAbilities)
    {
        isZombie.Value = true;
        isCorpse.Value = false;
        canMark.Value = !resetAbilities;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        var spectator = GetComponent<SpectatorLink>();
        if (spectator != null) spectator.ExitSpectator();

        Debug.Log($"[Flow] 客户端 {Owner.ClientId} 转化为玩家丧尸 (resetAbilities={resetAbilities})");
    }

    [Server]
    public void RestoreMark()
    {
        canMark.Value = true;
        Debug.Log($"[Flow] 客户端 {Owner.ClientId} 吃回自己尸体，恢复标记能力");
    }

    /// <summary>博士紫外灯：玩家丧尸僵直（客户端输入冻结一段时间 + 白色闪光）。</summary>
    [Server]
    public void StunZombie(float duration)
    {
        stunTimer = duration;
        RpcStun(duration);
    }

    private void Update()
    {
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;
    }

    [ObserversRpc]
    private void RpcStun(float duration)
    {
        stunTimer = duration;
        if (IsOwner && PlayerZombieUI.Instance != null) PlayerZombieUI.Instance.RefreshMark(duration, duration);
    }

    /// <summary>玩家丧尸死亡：原对象成为"自己的尸体"。</summary>
    [Server]
    public void BecomeOwnCorpse()
    {
        if (isCorpse.Value) return;
        isCorpse.Value = true;
        corpseOwnerClientId.Value = Owner.ClientId;
        corpseWasZombie.Value = true;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        EnsureCorpseCollider();
        transform.rotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
    }
}
