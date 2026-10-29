using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

/// <summary>
/// 人类职业能力（客户端按键 → ServerRpc 判定）：
/// 员工 E=钥匙开门；哥哥 E=撬门；博士 右键=紫外枪(限次)、G=血清(限次)；
/// E 可呼叫撤离（电台）与登机（撤离区）；其他角色 E 只能开门/关门。
/// </summary>
public class HumanAbilities : NetworkBehaviour
{
    private GameConfig cfg;
    private bool enabledByRole;

    private void Awake()
    {
        cfg = GameConfig.Load();
    }

    public void SetEnabled(bool on)
    {
        enabledByRole = on;
    }

    private void Update()
    {
        if (!IsOwner || !enabledByRole) return;
        if (Keyboard.current == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (GameInput.WasPressed(SettingsStore.S.keyInteract))
            TryInteract();

        var rc = GetComponent<RoleComponent>();
        if (rc == null) return;

        if (RoleDef.IsDoctor(rc.Role))
        {
            if (GameInput.WasPressed(SettingsStore.S.keyUv))
                TryUseUV();
            if (GameInput.WasPressed(SettingsStore.S.keySerum))
                TryUseSerum();
        }
    }

    // ============ 交互（E）============

    private void TryInteract()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !Camera.main) return;
        var cam = Camera.main.transform;

        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 3.5f)) return;

        var door = hit.collider.GetComponentInParent<Door>();
        if (door != null)
        {
            CmdInteractDoor(door.NetworkObject, RoleDef.HasKey(rc.Role) ? 1 : (RoleDef.CanPry(rc.Role) ? 2 : 0));
            return;
        }

        var radio = hit.collider.GetComponentInParent<RadioStation>();
        if (radio != null)
        {
            CmdCallExtraction();
            return;
        }
    }

    private void TryUseUV()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !Camera.main) return;
        if (!Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, cfg.uvRange)) return;

        // 命中人机丧尸或玩家丧尸
        var zombie = hit.collider.GetComponentInParent<SimpleZombie>();
        var zrc = hit.collider.GetComponentInParent<RoleComponent>();
        NetworkObject targetNo = null;
        if (zombie != null) targetNo = zombie.GetComponent<NetworkObject>();
        else if (zrc != null && zrc.IsZombie) targetNo = zrc.GetComponent<NetworkObject>();
        if (targetNo == null) return;
        CmdUseUV(targetNo);
    }

    private void TryUseSerum()
    {
        // 优先对自己使用；否则对视野内感染者
        var myInf = GetComponent<InfectionSystem>();
        if (myInf != null && myInf.IsInfected)
        {
            CmdUseSerumSelf();
            return;
        }
        if (!Camera.main) return;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 6f))
        {
            var targetInf = hit.collider.GetComponentInParent<InfectionSystem>();
            if (targetInf != null)
            {
                CmdUseSerumOther(targetInf.NetworkObject);
            }
        }
    }

    // ============ ServerRpc ============

    [ServerRpc]
    private void CmdInteractDoor(NetworkObject doorNo, int action)
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || rc.IsCorpse) return;
        if (doorNo == null) return;
        var door = doorNo.GetComponent<Door>();
        if (door == null) return;
        if (Vector3.Distance(transform.position, door.transform.position) > 4f) return;

        switch (action)
        {
            case 0: door.ToggleOpen(); break;                 // 普通开门/关门
            case 1: door.UseKey(); break;                     // 员工钥匙
            case 2: door.Pry(Owner); break;                   // 哥哥撬门
        }
    }

    [ServerRpc]
    private void CmdCallExtraction()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || rc.IsZombie || rc.IsCorpse) return;
        var flow = GameFlow.Instance;
        if (flow != null) flow.RequestCallExtraction();
    }

    [ServerRpc]
    private void CmdUseUV(NetworkObject targetNo)
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !RoleDef.IsDoctor(rc.Role) || rc.IsCorpse) return;
        if (targetNo == null) return;
        if (Vector3.Distance(transform.position, targetNo.transform.position) > cfg.uvRange) return;

        var zombie = targetNo.GetComponent<SimpleZombie>();
        if (zombie != null) { zombie.Stun(cfg.uvStunDuration); return; }
        var zrc = targetNo.GetComponent<RoleComponent>();
        if (zrc != null && zrc.IsZombie) zrc.StunZombie(cfg.uvStunDuration);
    }

    [ServerRpc]
    private void CmdUseSerumSelf()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !RoleDef.IsDoctor(rc.Role) || rc.IsCorpse) return;
        var inf = GetComponent<InfectionSystem>();
        if (inf != null) inf.Cure();
    }

    [ServerRpc]
    private void CmdUseSerumOther(NetworkObject targetNo)
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null || !RoleDef.IsDoctor(rc.Role) || rc.IsCorpse) return;
        if (targetNo == null) return;
        if (Vector3.Distance(transform.position, targetNo.transform.position) > 6f) return;
        var inf = targetNo.GetComponent<InfectionSystem>();
        if (inf != null) inf.Cure();
    }
}
