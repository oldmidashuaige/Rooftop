using UnityEngine;
using FishNet;

/// <summary>
/// 撤离区（服务器判定）：直升机判定区。撤离倒计时内，区内人类按"先到先得"登机（3 座）。
/// 用 Physics.OverlapBox 检测，广播给 GameFlow。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ExtractionZone : MonoBehaviour
{
    public Vector3 size = new Vector3(6f, 4f, 6f);

    private BoxCollider box;
    private bool serverReady;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
    }

    private void Start()
    {
        if (InstanceFinder.IsServerStarted) serverReady = true;
    }

    private void Update()
    {
        if (!serverReady || !InstanceFinder.IsServerStarted) return;
        var flow = GameFlow.Instance;
        if (flow == null || flow.State != GameFlow.RoundState.Evacuation) return;

        var hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents, box.transform.rotation, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var rc = h.GetComponentInParent<RoleComponent>();
            if (rc == null || rc.IsZombie || rc.IsCorpse) continue;
            if (rc.Owner == null || !rc.Owner.IsActive) continue;
            flow.HandleBoarding(rc.Owner.ClientId);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.3f);
        var c = GetComponent<BoxCollider>();
        if (c != null) Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
