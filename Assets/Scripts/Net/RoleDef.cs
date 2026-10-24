using System.Collections.Generic;

/// <summary>人类职业。</summary>
public enum HumanRole
{
    None = 0,
    OfficeWorker,   // 员工：钥匙（不限次）+ 轻车熟路（+10% 移速）
    WorkerBrother,  // 工人·弟弟：接线钳（电工，修电闸免零件/更快，阶段三生效）
    WorkerElder,    // 工人·哥哥：扳手（撬上锁门→转普通门）
    Doctor,         // 博士·老学究：紫外线枪（丧尸僵直/闪光）+ 血清（解除感染）
    Student         // 学生×4：基础人类（能力留空）
}

/// <summary>阵营。</summary>
public enum Team
{
    None = 0,
    Human,
    Zombie
}

/// <summary>角色定义：元数据 + 能力查询。总 8 名可选人类（1+2+1+4），选 5 人类 + 1 玩家丧尸开局。</summary>
public static class RoleDef
{
    public static readonly HumanRole[] HumanPool =
    {
        HumanRole.OfficeWorker,
        HumanRole.WorkerBrother,
        HumanRole.WorkerElder,
        HumanRole.Doctor,
        HumanRole.Student,
        HumanRole.Student,
        HumanRole.Student,
        HumanRole.Student
    };

    public static int PoolIndex(HumanRole r)
    {
        for (int i = 0; i < HumanPool.Length; i++)
            if (HumanPool[i] == r) return i;
        return -1;
    }

    public static string Eyebrow(HumanRole r)
    {
        switch (r)
        {
            case HumanRole.OfficeWorker: return "OFFICE WORKER";
            case HumanRole.WorkerBrother: return "WORKER // ELDER BROTHER";
            case HumanRole.WorkerElder: return "WORKER // YOUNGER BROTHER";
            case HumanRole.Doctor: return "DOCTOR";
            case HumanRole.Student: return "STUDENT";
            default: return "???";
        }
    }

    public static string DisplayName(HumanRole r)
    {
        switch (r)
        {
            case HumanRole.OfficeWorker: return "员工";
            case HumanRole.WorkerBrother: return "工人·哥哥";
            case HumanRole.WorkerElder: return "工人·弟弟";
            case HumanRole.Doctor: return "博士";
            case HumanRole.Student: return "学生";
            default: return "未分配";
        }
    }

    public static string Tool(HumanRole r)
    {
        switch (r)
        {
            case HumanRole.OfficeWorker: return "钥匙（不限次）· 轻车熟路 +10%";
            case HumanRole.WorkerBrother: return "扳手：撬开上锁门";
            case HumanRole.WorkerElder: return "接线钳：修电闸（免零件/更快）";
            case HumanRole.Doctor: return "紫外线枪 · 血清";
            default: return "无专属道具";
        }
    }

    public static float SpeedBonus(HumanRole r) => r == HumanRole.OfficeWorker ? 0.10f : 0f;

    public static bool HasKey(HumanRole r) => r == HumanRole.OfficeWorker;
    public static bool CanPry(HumanRole r) => r == HumanRole.WorkerElder;
    public static bool IsElectrician(HumanRole r) => r == HumanRole.WorkerBrother;
    public static bool IsDoctor(HumanRole r) => r == HumanRole.Doctor;
}
