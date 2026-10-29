using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

/// <summary>
/// 枪械：客户端(owner) 计算瞄准+散布并发起 CmdFire；服务器 Raycast 判定命中与伤害，RpcFx 广播特效。
/// 命中玩家僵尸/人类时按阵营结算（阶段二）。爆头 = 命中名为 Head 的碰撞体。
/// </summary>
public class Gun : NetworkBehaviour
{
    public float range = 120f;
    public float fireRate = 8f;
    public float damage = 40f;
    public float headshotMultiplier = 3f;
    public float spreadStanding = 0.5f;
    public float spreadWalking = 4f;
    public float spreadRunning = 20f;
    public Material tracerMaterial;
    public Material sparkMaterial;

    public static bool AutoFire;

    private Camera cam;
    private float nextFireTime;
    private Light flashLight;
    private float flashTimer;
    private bool inputEnabled;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = GetComponentInParent<Camera>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        inputEnabled = IsOwner;
    }

    public void SetEnabled(bool on)
    {
        inputEnabled = on && IsOwner;
    }

    private void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && flashLight != null) flashLight.intensity = 0f;
        }

        if (!inputEnabled) return;
        if (Keyboard.current == null && !AutoFire) return;
        if (!AutoFire && Cursor.lockState != CursorLockMode.Locked) return;
        bool fire = AutoFire || GameInput.IsDown(SettingsStore.S.keyAttack);
        if (fire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Fire();
        }
    }

    private void Fire()
    {
        flashTimer = 0.05f;
        if (flashLight != null) flashLight.intensity = 8f;

        var clip = SfxLibrary.Shot;
        if (clip != null)
        {
            var src = SfxLibrary.EnsureSource(gameObject);
            src.PlayOneShot(clip, 0.9f);
        }

        Vector3 aim = AimDirection();
        CmdFire(aim);
    }

    private Vector3 AimDirection()
    {
        Vector3 direction = cam != null ? cam.transform.forward : transform.forward;
        float spread = ComputeSpread();
        if (spread > 0.001f)
        {
            float tan = Mathf.Tan(spread * Mathf.Deg2Rad);
            Vector2 off = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            if (off.sqrMagnitude > 1f) off.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 up = Vector3.Cross(direction, right).normalized;
            direction = (direction + right * off.x * tan + up * off.y * tan).normalized;
        }
        return direction;
    }

    private float ComputeSpread()
    {
        var ctrl = GetComponentInParent<FirstPersonController>();
        if (ctrl == null) return spreadStanding;
        float speed = ctrl.CurrentSpeed;
        if (speed < 0.5f) return spreadStanding;
        if (speed < ctrl.walkSpeed)
            return Mathf.Lerp(spreadStanding, spreadWalking, speed / Mathf.Max(0.001f, ctrl.walkSpeed));
        return Mathf.Lerp(spreadWalking, spreadRunning,
            (speed - ctrl.walkSpeed) / Mathf.Max(0.001f, ctrl.runSpeed - ctrl.walkSpeed));
    }

    [ServerRpc]
    private void CmdFire(Vector3 aim)
    {
        // Gun 挂在相机子物体上，transform.position 即眼睛位置
        Vector3 eye = transform.position;
        Vector3 end = eye + aim * range;
        var myNo = GetComponentInParent<NetworkObject>();

        Ray ray = new Ray(eye, aim);
        RaycastHit[] hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        RaycastHit chosen = default;
        for (int i = 0; i < hits.Length; i++)
        {
            // 跳过自己
            var hitNo = hits[i].collider.GetComponentInParent<NetworkObject>();
            if (hitNo != null && hitNo == myNo) continue;
            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                chosen = hits[i];
            }
        }

        bool didHit = chosen.collider != null;
        bool hitTarget = false; // 命中丧尸/玩家（人类或玩家丧尸）才有命中反馈
        if (didHit)
        {
            end = chosen.point;
            bool headshot = chosen.collider.transform.name == "Head";
            float dmg = headshot ? damage * headshotMultiplier : damage;

            // 子弹可伤害人机丧尸与玩家丧尸；友伤可伤害人类
            var zombie = chosen.collider.GetComponentInParent<SimpleZombie>();
            if (zombie != null)
            {
                zombie.TakeDamage(dmg, headshot);
                hitTarget = true;
            }
            else
            {
                var rc = chosen.collider.GetComponentInParent<RoleComponent>();
                if (rc != null)
                {
                    // 感染中不可被攻击
                    var inf = rc.GetComponent<InfectionSystem>();
                    if (inf == null || !inf.IsInfected)
                    {
                        var health = rc.GetComponent<PlayerHealth>();
                        if (health != null)
                        {
                            health.TakeDamage(Mathf.RoundToInt(dmg));
                            hitTarget = true;
                            var sound = FindObjectOfType<SoundEvents>();
                            if (sound != null) sound.NotifyGunshotHit(rc.transform.position);
                        }
                    }
                }
            }
        }

        // 枪声事件（喂仇恨 + 玩家丧尸暴露指示）
        var se = FindObjectOfType<SoundEvents>();
        if (se != null) se.NotifyGunshot(eye, aim, Owner.ClientId);

        RpcFx(eye, end, didHit, hitTarget);
    }

    [ObserversRpc]
    private void RpcFx(Vector3 origin, Vector3 end, bool didHit, bool hitTarget)
    {
        SpawnTracer(origin, end);
        SpawnSpark(end);
        // 命中提示只在命中丧尸/玩家时出现（不显示在墙壁等非目标上）
        if (hitTarget && IsOwner && GameHUD.Instance != null) GameHUD.Instance.OnHitMarker();
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        if (tracerMaterial != null) lr.material = tracerMaterial;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.startColor = new Color(1f, 0.9f, 0.4f);
        lr.endColor = new Color(1f, 0.5f, 0.1f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Destroy(go, 0.04f);
    }

    private void SpawnSpark(Vector3 point)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Spark";
        go.transform.position = point;
        go.transform.localScale = Vector3.one * 0.08f;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (sparkMaterial != null) mr.sharedMaterial = sparkMaterial;
        Destroy(go, 0.12f);
    }
}
