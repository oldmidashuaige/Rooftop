using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>统一从设置读取键位并查询状态（客户端输入）。</summary>
public static class GameInput
{
    /// <summary>该键当前是否按住。</summary>
    public static bool IsDown(int field)
    {
        if (field == GameSettings.MouseLeft) return Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (field == GameSettings.MouseRight) return Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (Keyboard.current == null) return false;
        var k = (Key)SettingsStore.S.GetKey(field);
        if (k == Key.None) return false;
        return Keyboard.current[k].isPressed;
    }

    /// <summary>该键是否本帧刚按下。</summary>
    public static bool WasPressed(int field)
    {
        if (field == GameSettings.MouseLeft) return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (field == GameSettings.MouseRight) return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (Keyboard.current == null) return false;
        var k = (Key)SettingsStore.S.GetKey(field);
        if (k == Key.None) return false;
        return Keyboard.current[k].wasPressedThisFrame;
    }

    /// <summary>鼠标左键是否按住（连发用）。</summary>
    public static bool AttackHeld()
    {
        if (Mouse.current == null) return false;
        return Mouse.current.leftButton.isPressed;
    }
}
