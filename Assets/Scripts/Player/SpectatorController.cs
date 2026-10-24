using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 幽灵观战相机（纯客户端本地对象）：无碰撞/无建模/无声/完全隐形。
/// - 自由飞行：WASD 平移、空格上升、Shift 下降（穿墙）
/// - 附身：左键点玩家（射线）或滚轮在存活玩家间切换；右键退出
/// - 被啃时：固定视角看自己的尸体
/// - SpectatorMode.Disabled：只有固定视角，无法自由飞行
/// </summary>
public class SpectatorController : MonoBehaviour
{
    private static SpectatorController inst;

    private Camera cam;
    private bool possessed;
    private Transform possessTarget;
    private bool fixedView;
    private GameObject fixedTarget;
    private float moveSpeed = 12f;
    private float yaw;
    private float pitch;
    private int wheelIndex;

    private readonly List<RoleComponent> aliveTargets = new List<RoleComponent>();

    public static bool Active => inst != null;

    public static void Enter(SpectatorLink link, bool wasZombie)
    {
        if (inst != null) { inst.SetTarget(link.transform); return; }
        var go = new GameObject("SpectatorCamera");
        go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        inst = go.AddComponent<SpectatorController>();
        inst.Init(link);
    }

    public static void Exit()
    {
        if (inst != null)
        {
            Destroy(inst.gameObject);
            inst = null;
        }
    }

    public static void ForceFixedView(GameObject corpse)
    {
        if (inst == null) Enter(corpse.GetComponent<SpectatorLink>(), false);
        if (inst != null) inst.SetFixedView(corpse.transform);
    }

    private void Init(SpectatorLink link)
    {
        var start = link.transform.position + Vector3.up * 3f;
        transform.position = start;
        cam = GetComponent<Camera>();
        yaw = 0f;
        pitch = -20f;

        var cfg = GameConfig.Load();
        if (cfg.spectatorMode == SpectatorMode.Disabled)
        {
            // 禁用观战：固定看自己尸体
            SetFixedView(link.transform);
        }
    }

    private void SetTarget(Transform t) { transform.position = t.position + Vector3.up * 3f; }

    private void SetFixedView(Transform corpse)
    {
        fixedView = true;
        possessed = false;
        fixedTarget = corpse.gameObject;
        RefreshAlive();
    }

    private void OnDestroy()
    {
        if (inst == this) inst = null;
    }

    private void Update()
    {
        if (fixedView)
        {
            UpdateFixedView();
            return;
        }
        UpdateFreeFly();
    }

    private void UpdateFixedView()
    {
        if (fixedTarget == null)
        {
            fixedView = false;
            return;
        }
        var p = fixedTarget.transform.position + Vector3.up * 0.4f;
        var offset = new Vector3(1.6f, 0.8f, 1.8f);
        transform.position = p + offset;
        transform.LookAt(fixedTarget.transform.position + Vector3.up * 0.6f);
    }

    private void UpdateFreeFly()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            possessed = false;
            possessTarget = null;
            return;
        }

        if (possessed && possessTarget != null)
        {
            // 附身：跟随目标相机
            var targetCam = possessTarget.GetComponentInChildren<Camera>(true);
            transform.position = targetCam != null ? targetCam.transform.position : possessTarget.position + Vector3.up * 1.6f;
            transform.rotation = targetCam != null ? targetCam.transform.rotation : possessTarget.rotation;

            // 右键退出附身
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                possessed = false;
                possessTarget = null;
            }

            // 滚轮切换附身目标
            var scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (scroll != 0f)
            {
                CycleTargets(scroll > 0f ? 1 : -1);
            }
            return;
        }

        // 自由飞行
        Vector2 move = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) move.y += 1f;
        if (Keyboard.current.sKey.isPressed) move.y -= 1f;
        if (Keyboard.current.dKey.isPressed) move.x += 1f;
        if (Keyboard.current.aKey.isPressed) move.x -= 1f;

        float up = 0f;
        if (Keyboard.current.spaceKey.isPressed) up += 1f;
        if (Keyboard.current.leftShiftKey.isPressed) up -= 1f;

        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 dir = (transform.forward * move.y + transform.right * move.x + Vector3.up * up).normalized;
        transform.position += dir * moveSpeed * Time.unscaledDeltaTime;

        // 左键点击附身（射线命中玩家）
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPossess();
        }

        // 滚轮选择
        var scroll2 = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (scroll2 != 0f) CycleTargets(scroll2 > 0f ? 1 : -1);
    }

    private void RefreshAlive()
    {
        aliveTargets.Clear();
        var cfg = GameConfig.Load();
        foreach (var rc in FindObjectsByType<RoleComponent>())
        {
            if (rc.IsCorpse) continue;
            if (rc.Owner == null || !rc.Owner.IsActive) continue;
            if (cfg.spectatorMode == SpectatorMode.ZombiesOnly && !rc.IsZombie) continue;
            aliveTargets.Add(rc);
        }
    }

    private void TryPossess()
    {
        RefreshAlive();
        if (Camera.main != null && Physics.Raycast(cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)), out RaycastHit hit, 200f))
        {
            var rc = hit.collider.GetComponentInParent<RoleComponent>();
            if (rc != null && aliveTargets.Contains(rc))
            {
                Possess(rc);
                return;
            }
        }
        // 射线未中玩家 → 附身当前滚轮选中的目标
        if (aliveTargets.Count > 0)
            Possess(aliveTargets[wheelIndex % aliveTargets.Count]);
    }

    private void CycleTargets(int dir)
    {
        RefreshAlive();
        if (aliveTargets.Count == 0) return;
        wheelIndex = ((wheelIndex + dir) % aliveTargets.Count + aliveTargets.Count) % aliveTargets.Count;
        Possess(aliveTargets[wheelIndex]);
    }

    private void Possess(RoleComponent rc)
    {
        possessed = true;
        possessTarget = rc.transform;
        // 直接吸附到目标相机
        var targetCam = possessTarget.GetComponentInChildren<Camera>(true);
        if (targetCam != null)
        {
            transform.position = targetCam.transform.position;
            transform.rotation = targetCam.transform.rotation;
        }
    }
}
