using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;

/// <summary>
/// 服务器声音事件总线：所有"会暴露位置/引发仇恨"的声音在此生成。
/// 每个事件：① 喂给全体人机丧尸的 HatredBrain；② 广播 3D 音效给客户端；
/// ③ 枪声额外广播"开枪者位置"给玩家丧尸阵营（中枪暴露）。
/// 声音穿墙，仅按距离衰减；视线加成在 HatredBrain 侧。
/// </summary>
public class SoundEvents : NetworkBehaviour
{
    [Serializable] public struct SoundAtBroadcast : IBroadcast { public Vector3 pos; public int clip; public float volume; }
    [Serializable] public struct GunshotIndicatorBroadcast : IBroadcast { public Vector3 pos; public float loudness; }

    public const int ClipShot = 0;
    public const int ClipStep = 1;
    public const int ClipLand = 2;
    public const int ClipPry = 3;
    public const int ClipInfect = 4;

    public static SoundEvents Instance { get; private set; }
    private GameConfig cfg;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cfg = GameConfig.Load();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============ 事件入口（服务器调用）============

    public void NotifyStep(Vector3 pos, int sourceClient, bool run)
    {
        if (!IsServerStarted) return;
        Emit(pos, sourceClient, run ? cfg.runStepLoudness : cfg.walkStepLoudness,
            run ? cfg.runStepRange : cfg.walkStepRange, ClipStep);
    }

    public void NotifyLand(Vector3 pos, int sourceClient)
    {
        if (!IsServerStarted) return;
        Emit(pos, sourceClient, cfg.landLoudness, cfg.landRange, ClipLand);
    }

    public void NotifyGunshot(Vector3 pos, Vector3 dir, int sourceClient)
    {
        if (!IsServerStarted) return;
        Emit(pos, sourceClient, cfg.gunshotLoudness, cfg.gunshotRange, ClipShot);
        BroadcastGunshotIndicator(pos, cfg.gunshotLoudness);
    }

    public void NotifyGunshotHit(Vector3 pos)
    {
        if (!IsServerStarted) return;
        Emit(pos, -1, cfg.gunshotLoudness * 0.6f, cfg.gunshotRange, ClipShot);
    }

    public void NotifyPry(Vector3 pos, int sourceClient)
    {
        if (!IsServerStarted) return;
        Emit(pos, sourceClient, cfg.pryLoudness, cfg.pryRange, ClipPry);
    }

    public void NotifyInfect(Vector3 pos)
    {
        if (!IsServerStarted) return;
        Emit(pos, -1, cfg.pryLoudness * 1.2f, cfg.pryRange * 1.4f, ClipInfect);
    }

    // ============ 核心分发 ============

    private void Emit(Vector3 pos, int sourceClient, float loudness, float range, int clip)
    {
        // 喂给所有在范围内的人机丧尸
        foreach (var brain in FindObjectsByType<HatredBrain>())
            brain.OnSound(sourceClient, pos, loudness, range);

        // 广播 3D 音效（服务器→所有客户端，含 host 本地）
        var msg = new SoundAtBroadcast { pos = pos, clip = clip, volume = Mathf.Clamp01(loudness / 60f) };
        NetworkManager.ServerManager.Broadcast(msg);
        var local = NetworkManager.ClientManager.Connection;
        if (local != null && local.IsValid && local.IsActive)
            NetworkManager.ServerManager.Broadcast(local, msg);
    }

    private void BroadcastGunshotIndicator(Vector3 pos, float loudness)
    {
        // 只发给玩家丧尸阵营
        foreach (var p in FindObjectsByType<RoleComponent>())
        {
            if (!p.IsZombie || p.IsCorpse) continue;
            if (p.Owner == null || !p.Owner.IsActive) continue;
            NetworkManager.ServerManager.Broadcast(p.Owner, new GunshotIndicatorBroadcast { pos = pos, loudness = loudness }, false);
        }
    }

    // ============ 客户端 ============

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkManager.ClientManager.RegisterBroadcast<SoundAtBroadcast>(OnSoundAt);
        NetworkManager.ClientManager.RegisterBroadcast<GunshotIndicatorBroadcast>(OnGunshotIndicator);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (NetworkManager == null) return;
        NetworkManager.ClientManager.UnregisterBroadcast<SoundAtBroadcast>(OnSoundAt);
        NetworkManager.ClientManager.UnregisterBroadcast<GunshotIndicatorBroadcast>(OnGunshotIndicator);
    }

    private void OnSoundAt(SoundAtBroadcast msg, Channel channel)
    {
        AudioClip clip = msg.clip switch
        {
            ClipShot => SfxLibrary.Shot,
            ClipLand => SfxLibrary.Land,
            ClipPry => SfxLibrary.Hurt,
            ClipInfect => SfxLibrary.Hurt,
            _ => SfxLibrary.Step
        };
        if (clip == null) return;
        SfxAt.Play3D(msg.pos, clip, msg.volume);
    }

    private void OnGunshotIndicator(GunshotIndicatorBroadcast msg, Channel channel)
    {
        // 本机是玩家丧尸 → 显示开枪者方向指示
        if (PlayerZombieUI.Instance != null)
            PlayerZombieUI.Instance.ShowGunshotIndicator(msg.pos, msg.loudness);
    }
}
