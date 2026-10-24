using System.Collections;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// 感染状态机（服务器权威）：任意丧尸近战命中人类 → 感染。
/// 感染期：全程无法行动/无法被攻击，播放 CG 占位（黑屏+文字+故障特效）。
/// 倒计时结束未解除 → 同对象转为玩家丧尸；博士血清 → 解除。
/// </summary>
public class InfectionSystem : NetworkBehaviour
{
    public float duration = 30f;
    private float timer;
    private bool infected;

    public bool IsInfected => infected;

    private void Awake()
    {
        var cfg = GameConfig.Load();
        duration = cfg.infectionDuration;
    }

    [Server]
    public bool TryInfect()
    {
        if (infected) return false;
        var rc = GetComponent<RoleComponent>();
        if (rc == null || rc.IsZombie || rc.IsCorpse) return false;
        var health = GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        infected = true;
        timer = duration;
        Debug.Log($"[Inf] 客户端 {Owner.ClientId} 被感染，{timer}s 后转化");
        RpcInfectionStart(duration);
        return true;
    }

    [Server]
    public void Cure()
    {
        if (!infected) return;
        infected = false;
        Debug.Log($"[Inf] 客户端 {Owner.ClientId} 感染被血清解除");
        RpcInfectionCured();
    }

    private void Update()
    {
        if (!IsServerStarted) return;
        if (!infected) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            infected = false;
            var rc = GetComponent<RoleComponent>();
            if (rc != null) rc.ConvertToZombie(false); // 感染转化 = 完整玩家丧尸（可标记）
            RpcInfectionCured(); // 结束 CG 覆盖
        }
    }

    // ============ 客户端表现 ============

    [ObserversRpc]
    private void RpcInfectionStart(float t)
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnInfectionStart(t);
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.SetInputEnabled(false);
    }

    [ObserversRpc]
    private void RpcInfectionCured()
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnInfectionEnd();
        var rc = GetComponent<RoleComponent>();
        if (rc != null && !rc.IsZombie)
        {
            var fpc = GetComponent<FirstPersonController>();
            if (fpc != null) fpc.SetInputEnabled(true);
        }
    }
}
