using System.IO;
using UnityEngine;

/// <summary>可序列化的游戏设置（存到 persistentDataPath/settings.json，每次启动加载）。</summary>
[System.Serializable]
public class GameSettings
{
    public string username = "玩家";

    // —— 鼠标 ——
    public float mouseSensitivity = 0.08f;

    // —— 准星 ——
    public bool crosshairCenter = true;
    public bool crosshairHorizontal = true;
    public bool crosshairVertical = true;
    public float crosshairLength = 10f;      // 单侧长度（从中心向外，px）
    public float crosshairThickness = 2f;    // 线宽（px）
    public string crosshairColor = "#EDEDED";

    // —— 窗口模式 0=窗口 1=全屏 2=无边框 ——
    public int windowMode = 1;

    // —— 键位（UnityEngine.InputSystem.Key 枚举值；鼠标键用负值哨兵）——
    public const int MouseLeft = -1;
    public const int MouseRight = -2;

    public int keyForward = (int)UnityEngine.InputSystem.Key.W;
    public int keyBack = (int)UnityEngine.InputSystem.Key.S;
    public int keyLeft = (int)UnityEngine.InputSystem.Key.A;
    public int keyRight = (int)UnityEngine.InputSystem.Key.D;
    public int keyRun = (int)UnityEngine.InputSystem.Key.LeftShift;
    public int keyJump = (int)UnityEngine.InputSystem.Key.Space;
    public int keyInteract = (int)UnityEngine.InputSystem.Key.E;      // 人类交互 / 玩家丧尸啃尸
    public int keyAttack = MouseLeft;                                   // 开枪 / 丧尸近战（鼠标左键）
    public int keyMark = (int)UnityEngine.InputSystem.Key.F;          // 玩家丧尸标记
    public int keySerum = (int)UnityEngine.InputSystem.Key.G;         // 博士血清
    public int keyUv = MouseRight;                                     // 博士紫外枪（鼠标右键）
    public int keyPause = (int)UnityEngine.InputSystem.Key.Escape;    // 暂停菜单

    private const string FileName = "settings.json";

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public UnityEngine.InputSystem.Key GetKey(int field) => (UnityEngine.InputSystem.Key)field;

    /// <summary>是否为鼠标键。</summary>
    public static bool IsMouse(int field) => field == MouseLeft || field == MouseRight;

    /// <summary>显示名（用于设置面板）。</summary>
    public static string KeyDisplayName(int field)
    {
        if (field == MouseLeft) return "鼠标左键";
        if (field == MouseRight) return "鼠标右键";
        return ((UnityEngine.InputSystem.Key)field).ToString();
    }

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                var s = JsonUtility.FromJson<GameSettings>(json);
                if (s != null) return s;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Settings] 读取设置失败，使用默认: " + e.Message);
        }
        return new GameSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(SavePath, json);
            Debug.Log("[Settings] 已保存到 " + SavePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Settings] 保存设置失败: " + e.Message);
        }
    }
}

/// <summary>全局设置单例：启动时加载一次，改动后 Save() 落盘。</summary>
public static class SettingsStore
{
    private static GameSettings _s;
    public static GameSettings S => _s ?? (_s = GameSettings.Load());
    public static void Save() { S.Save(); }
    public static void Reload() { _s = GameSettings.Load(); }
}
