using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;

/// <summary>
/// 人机丧尸仇恨 AI（服务器权威）。
/// 模型：每个活人对该丧尸一份仇恨值 H；声音事件加 H（ΔH = loudness·(1−d/R)²，穿墙）；视线内缓慢加 H（需无遮挡）。
/// 衰减用指数 H·exp(−λ·dt)（与帧率无关）；双阈值迟滞防抽搐；最短追击时间；
/// 区域束缚：只在锚点半径内追击，目标出区即回锚游荡。
/// 玩家丧尸标记 = 把目标 H 推到标记值（>锁定阈值）→ 短暂集火，随后自然衰减。
/// </summary>
public class HatredBrain : MonoBehaviour
{
    public float detectRange = 45f;
    public float attackRange = 1.9f;
    public float attackCooldown = 1.2f;

    private GameConfig cfg;
    private Transform trans;
    private Vector3 anchor;
    private readonly Dictionary<int, float> hatred = new Dictionary<int, float>();
    private int currentTarget = -1;
    private float chaseTimer;
    private float attackTimer;
    private Vector3 roamTarget;
    private float roamTimer;
    private bool serverReady;

    private void Awake()
    {
        trans = transform;
        cfg = GameConfig.Load();
    }

    public override string ToString() => "HatredBrain";

    private void Start()
    {
        if (InstanceFinder.IsServerStarted)
        {
            serverReady = true;
            anchor = transform.position; // 锚点 = 出生位置（区域束缚中心）
        }
    }

    private void Update()
    {
        if (!serverReady || !IsServerAuthority()) return;

        var zombie = GetComponent<SimpleZombie>();
        if (zombie != null && zombie.IsDead) return;
        if (zombie != null && zombie.IsStunned) return; // 紫外灯僵直：AI 冻结

        float dt = Time.deltaTime;
        attackTimer -= dt;
        chaseTimer -= dt;

        // 1) 指数衰减
        var keys = new List<int>(hatred.Keys);
        foreach (var k in keys)
        {
            float h = hatred[k] * Mathf.Exp(-cfg.forgetRate * dt);
            if (h < 0.5f) hatred.Remove(k);
            else hatred[k] = h;
        }

        // 2) 有效活人列表（过滤丧尸/尸体/死亡/登机/感染中）+ 视线加成
        var humans = new List<RoleComponent>();
        foreach (var h in FindObjectsByType<RoleComponent>())
        {
            if (h.IsZombie || h.IsCorpse) continue;
            if (h.Owner == null || !h.Owner.IsActive) continue;
            var health = h.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) continue;
            if (GameFlow.Instance != null && GameFlow.Instance.IsBoarded(h.Owner.ClientId)) continue;
            if (h.GetComponent<InfectionSystem>() != null && h.GetComponent<InfectionSystem>().IsInfected) continue;
            humans.Add(h);

            int id = h.Owner.ClientId;
            float dist = Vector3.Distance(trans.position, h.transform.position);
            if (dist <= detectRange && HasLineOfSight(h.transform.position))
            {
                hatred[id] = hatred.TryGetValue(id, out var cur) ? cur + cfg.sightBonusPerSec * dt : cfg.sightBonusPerSec * dt;
            }
        }

        // 清理不再有效玩家（已转丧尸/死亡/登机）的仇恨，防止残留导致追错对象
        var validIds = new HashSet<int>();
        foreach (var h in humans) validIds.Add(h.Owner.ClientId);
        keys = new List<int>(hatred.Keys);
        foreach (var k in keys)
            if (!validIds.Contains(k)) hatred.Remove(k);

        // 3) 选目标（只从有效活人中选；迟滞：追击中只要仍 >= drop 且未超最短时间，就不丢目标）
        int best = -1;
        float bestH = cfg.acquireThreshold;
        foreach (var kv in hatred)
        {
            if (!validIds.Contains(kv.Key)) continue;
            if (kv.Value < bestH) continue;
            if (currentTarget == kv.Key && kv.Value >= cfg.dropThreshold)
            {
                best = kv.Key; bestH = kv.Value; break;
            }
            if (kv.Value > bestH) { best = kv.Key; bestH = kv.Value; }
        }

        if (best != -1 && best != currentTarget)
        {
            currentTarget = best;
            chaseTimer = cfg.minChaseTime;
        }
        if (best == -1)
        {
            currentTarget = -1;
        }

        // 4) 状态机：游荡 / 回锚 / 追击
        float distToAnchor = Vector3.Distance(trans.position, anchor);

        RoleComponent target = null;
        if (currentTarget >= 0)
        {
            foreach (var h in humans)
            {
                if (h.Owner != null && h.Owner.ClientId == currentTarget) { target = h; break; }
            }
            if (target == null) currentTarget = -1;
        }

        if (target != null && Vector3.Distance(target.transform.position, anchor) > cfg.regionRadius)
        {
            currentTarget = -1; // 目标出区：脱战（区域束缚）
            target = null;
        }

        if (target == null)
        {
            if (distToAnchor > 0.5f)
                MoveToward(anchor, cfg.roamSpeed);
            else
                Roam(dt);
            return;
        }

        // 追击（目标在区域内）
        Vector3 toTarget = target.transform.position - trans.position;
        toTarget.y = 0f;
        float distToTarget = toTarget.magnitude;

        if (distToTarget <= attackRange)
        {
            trans.rotation = Quaternion.Slerp(trans.rotation, Quaternion.LookRotation(toTarget.normalized), 8f * dt);
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                var inf = target.GetComponent<InfectionSystem>();
                if (inf != null && inf.TryInfect())
                    Debug.Log($"[AI] 人机丧尸感染 客户端 {currentTarget}");
            }
        }
        else
        {
            MoveToward(target.transform.position, cfg.chaseSpeed);
        }
    }

    // ============ 外部输入 ============

    /// <summary>声音事件：对 sourceClient 的仇恨增加，量随距离衰减（穿墙）。</summary>
    public void OnSound(int sourceClient, Vector3 pos, float loudness, float range)
    {
        if (sourceClient < 0) return;
        float dist = Vector3.Distance(trans.position, pos);
        if (dist > range) return;
        float falloff = 1f - dist / range;
        float add = loudness * falloff * falloff;
        if (add <= 0f) return;
        hatred[sourceClient] = hatred.TryGetValue(sourceClient, out var cur) ? cur + add : add;
    }

    /// <summary>玩家丧尸标记：目标仇恨推到标记值（进入锁定）。</summary>
    public void MarkPlayer(int clientId, float boost)
    {
        hatred[clientId] = Mathf.Max(hatred.TryGetValue(clientId, out var cur) ? cur : 0f, boost);
    }

    private bool IsServerAuthority()
    {
        var no = GetComponent<NetworkObject>();
        return no == null || no.IsServerStarted;
    }

    private bool HasLineOfSight(Vector3 humanPos)
    {
        Vector3 eye = trans.position + Vector3.up * 1.6f;
        Vector3 target = humanPos + Vector3.up * 1.2f;
        if (Physics.Linecast(eye, target, out RaycastHit hit))
        {
            // 只有命中人类自己才算看见
            return hit.collider.GetComponentInParent<RoleComponent>() != null;
        }
        return true;
    }

    private void MoveToward(Vector3 p, float speed)
    {
        Vector3 to = p - trans.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        Vector3 dir = to.normalized;
        trans.position += dir * speed * Time.deltaTime;
        trans.rotation = Quaternion.Slerp(trans.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
    }

    private void Roam(float dt)
    {
        roamTimer -= dt;
        if (roamTimer <= 0f)
        {
            roamTimer = Random.Range(2f, 5f);
            Vector2 off = Random.insideUnitCircle * cfg.regionRadius * 0.7f;
            roamTarget = anchor + new Vector3(off.x, 0f, off.y);
        }
        MoveToward(roamTarget, cfg.roamSpeed);
    }
}
