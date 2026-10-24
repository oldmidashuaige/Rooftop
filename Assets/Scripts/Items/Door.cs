using System.Collections;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// 门（网络对象）：普通门 / 上锁门。
/// - 普通门：任何人可开/关
/// - 上锁门：员工钥匙可打开（打开后不自动关闭，被手动关闭后重新锁上）；哥哥撬门→永久转普通门
/// - 开/关状态服务器权威，客户端按 SyncVar 播放开门动画
/// </summary>
public class Door : NetworkBehaviour
{
    public float pryDuration = 4f;
    public bool startLocked;

    private readonly SyncVar<bool> isOpen = new();
    private readonly SyncVar<bool> isLocked = new();
    private readonly SyncVar<bool> isNormal = new();

    private Transform panel;
    private bool prying;

    public bool IsOpen => isOpen.Value;
    public bool IsLocked => isLocked.Value && !isNormal.Value;
    public bool IsNormal => isNormal.Value;

    private void Awake()
    {
        var t = transform.Find("Panel");
        if (t != null) panel = t;
        isOpen.OnChange += OnOpenChanged;
        isLocked.OnChange += OnLockChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        isLocked.Value = startLocked;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyVisual();
    }

    // ============ 服务器操作 ============

    [Server]
    public void ToggleOpen()
    {
        if (isOpen.Value) { isOpen.Value = false; return; }
        if (IsLocked) return; // 上锁门不能普通打开
        isOpen.Value = true;
    }

    [Server]
    public void UseKey()
    {
        if (isNormal.Value || !isLocked.Value) { isOpen.Value = true; return; }
        // 员工钥匙打开上锁门（保持 locked，但开门中；关闭后重新锁上）
        isOpen.Value = true;
    }

    [Server]
    public void Pry(NetworkConnection worker)
    {
        if (isNormal.Value || prying) return;
        StartCoroutine(PryCoroutine(worker));
    }

    private IEnumerator PryCoroutine(NetworkConnection worker)
    {
        prying = true;
        float t = pryDuration;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            if (t <= 0f)
            {
                var se = FindObjectOfType<SoundEvents>();
                if (se != null) se.NotifyPry(transform.position, worker.ClientId);
                break;
            }
            yield return null;
        }
        isNormal.Value = true;
        isLocked.Value = false;
        isOpen.Value = true;
        prying = false;
        Debug.Log("[Door] 上锁门被撬成普通门（永久可开）");
    }

    // ============ 客户端视觉 ============

    private void OnOpenChanged(bool _, bool __, bool asServer)
    {
        if (asServer) return;
        ApplyVisual();
    }

    private void OnLockChanged(bool _, bool __, bool asServer)
    {
        if (asServer) return;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (panel == null) return;
        float targetY = isOpen.Value ? 90f : 0f;
        var e = panel.localEulerAngles;
        e.y = targetY;
        panel.localEulerAngles = e;

        var mr = panel.GetComponent<Renderer>();
        if (mr != null)
            mr.material.color = IsLocked ? new Color(0.55f, 0.30f, 0.28f) : new Color(0.40f, 0.36f, 0.30f);
    }
}
