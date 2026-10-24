using System.Collections;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// 人机丧尸（阶段一简化版）：服务器权威 AI，追最近的活玩家并近战扣血。
/// 阶段二将扩展为：区域锚点游荡 + 仇恨系统（声音/标记/视线）+ 攻击改为触发感染。
/// </summary>
public class SimpleZombie : NetworkBehaviour
{
    public float health = 100f;
    public float moveSpeed = 1.3f;
    public float detectRange = 60f;
    public float attackDamage = 10f;
    public float attackRange = 1.8f;
    public float attackCooldown = 1.2f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private MaterialPropertyBlock mpb;
    private bool dead;
    private float flashTimer;
    private float stunTimer;

    public bool IsDead => dead;
    public bool IsStunned => stunTimer > 0f;

    /// <summary>博士紫外灯：僵直一段时间（AI 冻结 + 白闪）。</summary>
    [Server]
    public void Stun(float duration)
    {
        stunTimer = duration;
        RpcFlashWhite();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
        mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f) RestoreColors();
        }
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;
        // 移动/追击/攻击由 HatredBrain（服务器）驱动
    }

    [ObserversRpc]
    private void RpcFlashWhite()
    {
        if (IsServerStarted) return;
        flashTimer = 0.15f;
        SetColor(Color.white);
    }

    [Server]
    public void TakeDamage(float amount, bool headshot)
    {
        if (dead) return;

        flashTimer = 0.12f;
        SetColor(new Color(0.9f, 0.15f, 0.1f));
        RpcFlash();

        float dmg = headshot ? amount : amount; // 爆头倍率在 Gun 侧已处理
        health -= dmg;
        if (health <= 0f)
        {
            Debug.Log("[Zombie] 人机丧尸被击杀");
            Die();
        }
    }

    [ObserversRpc]
    private void RpcFlash()
    {
        if (IsServerStarted) return;
        flashTimer = 0.12f;
        SetColor(new Color(0.9f, 0.15f, 0.1f));
    }

    private void Die()
    {
        dead = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        RpcDie();
        Invoke(nameof(DoDespawn), 3f);
    }

    [ObserversRpc]
    private void RpcDie()
    {
        if (IsServerStarted) return;
        StartCoroutine(DieVisual());
    }

    private IEnumerator DieVisual()
    {
        transform.rotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
        transform.position += Vector3.down * 0.4f;
        yield return new WaitForSeconds(3f);
        if (gameObject != null) Destroy(gameObject);
    }

    private void DoDespawn()
    {
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn();
    }

    private void SetColor(Color c)
    {
        if (renderers == null || mpb == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    private void RestoreColors()
    {
        if (renderers == null || mpb == null || baseColors == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", baseColors[i]);
            renderers[i].SetPropertyBlock(mpb);
        }
    }
}
