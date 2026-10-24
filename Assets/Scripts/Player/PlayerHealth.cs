using UnityEngine;
using FishNet.Object;

/// <summary>
/// 玩家血量：服务器权威。子弹（含友伤）扣血；丧尸攻击走感染，不走血量。
/// 感染中不可被攻击（无敌）。HP=0：人类→尸体+观战；玩家丧尸→弱化重生（附身人机）。
/// 受击造成短暂减速（猛烈攻击后恢复）。
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 100;
    public float invincibleTime = 0.6f;
    public float hitSlowDuration = 0.5f;

    private int health;
    private float nextInvincible;
    private bool dead;

    public bool IsDead => dead;
    public int Health => health;
    public int MaxHealth => maxHealth;

    public override void OnStartServer()
    {
        base.OnStartServer();
        health = maxHealth;
    }

    [Server]
    public void TakeDamage(int amount)
    {
        if (dead) return;
        if (Time.time < nextInvincible) return;

        // 感染中全程无法被攻击
        var inf = GetComponent<InfectionSystem>();
        if (inf != null && inf.IsInfected) return;

        nextInvincible = Time.time + invincibleTime;
        health = Mathf.Max(0, health - amount);
        RpcSetHealth(health);
        RpcHitSlow();

        if (health <= 0)
            ResolveDeath();
    }

    [Server]
    private void ResolveDeath()
    {
        dead = true;
        RpcDeath();
        Debug.Log($"[Flow] 客户端 {Owner.ClientId} HP=0 死亡");

        var rc = GetComponent<RoleComponent>();
        if (rc == null) return;

        if (rc.IsZombie)
        {
            // 玩家丧尸死亡 → 弱化重生（附身人机），原对象变自己的尸体
            PlayerZombie.OnPlayerZombieKilled(rc, Owner);
        }
        else
        {
            // 人类死亡 → 尸体 + 幽灵观战（等待被啃变丧尸 / 回合结束）
            rc.SetCorpse();
        }
    }

    [ObserversRpc]
    private void RpcSetHealth(int hp)
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnHealthChanged(hp, maxHealth);
    }

    [ObserversRpc]
    private void RpcHitSlow()
    {
        if (!IsOwner) return;
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.ApplyHitSlow(hitSlowDuration);
    }

    [ObserversRpc]
    private void RpcDeath()
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnDeath();
    }
}
