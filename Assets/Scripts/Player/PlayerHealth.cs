using UnityEngine;
using FishNet.Object;

/// <summary>
/// 玩家血量：服务器权威。受击→RpcSetHealth 同步 HUD；死亡→RpcDeath。
/// 阶段一占位：死亡后 2.5s 原地满血复活（阶段二将替换为 尸体→幽灵观战→被啃转丧尸）。
/// </summary>
public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 100;
    public float invincibleTime = 0.6f;

    private int health;
    private float nextInvincible;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool dead;

    public bool IsDead => dead;
    public int Health => health;
    public int MaxHealth => maxHealth;

    public override void OnStartServer()
    {
        base.OnStartServer();
        health = maxHealth;
        startPos = transform.position;
        startRot = transform.rotation;
    }

    [Server]
    public void TakeDamage(int amount)
    {
        if (dead) return;
        if (Time.time < nextInvincible) return;
        nextInvincible = Time.time + invincibleTime;

        health = Mathf.Max(0, health - amount);
        RpcSetHealth(health);

        if (health <= 0)
        {
            dead = true;
            RpcDeath();
            Invoke(nameof(RespawnServer), 2.5f);
        }
    }

    private void RespawnServer()
    {
        dead = false;
        health = maxHealth;
        transform.position = startPos;
        transform.rotation = startRot;
        RpcSetHealth(health);
    }

    [ObserversRpc]
    private void RpcSetHealth(int hp)
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnHealthChanged(hp, maxHealth);
    }

    [ObserversRpc]
    private void RpcDeath()
    {
        if (!IsOwner) return;
        if (GameHUD.Instance != null) GameHUD.Instance.OnDeath();
    }
}
