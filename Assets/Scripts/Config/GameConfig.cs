using UnityEngine;

/// <summary>
/// 全局数值配置（ScriptableObject）。服务端运行时读取为准，集中可调。
/// 默认值：人类5 + 玩家丧尸1；感染30s；撤离3座/30s；博士紫外枪3次/血清2次；玩家丧尸标记CD 20s/范围25m。
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "ProjectRoof/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("人数")]
    public int humanSlots = 5;
    public int zombieSlots = 1;
    [Tooltip("大厅满员后若无人选角，多少秒后自动分配开局（防卡死；-autostart 则满员立即开局）")]
    public float lobbyAutoStartDelay = 15f;

    [Header("感染")]
    public float infectionDuration = 30f;

    [Header("撤离")]
    public int extractionSeats = 3;
    public float extractionCountdown = 30f;

    [Header("观战")]
    public SpectatorMode spectatorMode = SpectatorMode.Free;

    [Header("博士")]
    public int doctorUvUses = 3;
    public int doctorSerumUses = 2;
    public float uvStunDuration = 2f;
    public float uvRange = 25f;

    [Header("玩家丧尸")]
    public float markRange = 25f;
    public float markCooldown = 20f;
    public float markHatredBoost = 150f;
    public float eatCorpseDuration = 3f;

    [Header("人机丧尸·仇恨")]
    public float forgetRate = 0.12f;         // 指数遗忘率 λ
    public float acquireThreshold = 60f;     // 锁定阈值
    public float dropThreshold = 20f;        // 放弃阈值
    public float minChaseTime = 3f;          // 最短追击时间（防抽搐）
    public float sightBonusPerSec = 8f;      // 视线内每秒仇恨加成
    public float regionRadius = 60f;         // 人机游荡/追击区域半径（demo 地图较大；正式地图阶段四调小）
    public float roamSpeed = 1.0f;           // 游荡移速
    public float chaseSpeed = 4.5f;          // 追击移速（比人类慢，可追上走路者）

    [Header("声音事件（服务器计算→喂仇恨）")]
    public float walkStepLoudness = 1.5f;
    public float walkStepRange = 3f;
    public float runStepLoudness = 6f;
    public float runStepRange = 10f;
    public float landLoudness = 12f;
    public float landRange = 8f;
    public float gunshotLoudness = 60f;
    public float gunshotRange = 45f;
    public float pryLoudness = 18f;
    public float pryRange = 20f;
    public float uvLoudness = 8f;
    public float uvRangeSound = 12f;

    public static GameConfig Load()
    {
        var cfg = Resources.Load<GameConfig>("Config/GameConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<GameConfig>();
    }
}

public enum SpectatorMode
{
    Free = 0,        // 自由飞行 + 可附身人类/丧尸
    ZombiesOnly = 1, // 只能附身/观察丧尸（杜绝看人类爆点）
    Disabled = 2     // 禁用观战（死亡后固定看自己尸体直到被啃/回合结束）
}
