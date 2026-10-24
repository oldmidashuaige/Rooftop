using UnityEngine;
using FishNet.Connection;
using FishNet.Object;

/// <summary>
/// 观战桥接（NetworkPlayer 上）：服务器驱动客户端进入/退出幽灵观战。
/// 幽灵相机本身是纯客户端本地对象（隐形/无碰撞/无声），服务器只管状态。
/// </summary>
public class SpectatorLink : NetworkBehaviour
{
    [Server]
    public void EnterSpectator(bool wasZombie)
    {
        TargetEnterSpectator(Owner, wasZombie);
    }

    [Server]
    public void ExitSpectator()
    {
        TargetExitSpectator(Owner);
    }

    /// <summary>被啃咬开始时：锁定固定视角看自己的尸体。</summary>
    [Server]
    public void ForceFixedView()
    {
        TargetForceFixedView(Owner);
    }

    [TargetRpc]
    private void TargetEnterSpectator(NetworkConnection conn, bool wasZombie)
    {
        if (!IsOwner) return;
        SpectatorController.Enter(this, wasZombie);
    }

    [TargetRpc]
    private void TargetExitSpectator(NetworkConnection conn)
    {
        if (!IsOwner) return;
        SpectatorController.Exit();
    }

    [TargetRpc]
    private void TargetForceFixedView(NetworkConnection conn)
    {
        if (!IsOwner) return;
        SpectatorController.ForceFixedView(gameObject);
    }
}
