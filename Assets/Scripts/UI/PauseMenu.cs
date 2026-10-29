using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing;

/// <summary>
/// 暂停菜单（客户端）：游戏中按暂停键（默认 Esc）弹出屏幕中央菜单，
/// 含 继续 / 设置 / 退出。暂停时冻结输入、解锁鼠标。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance;

    private GameObject root;
    private bool paused;
    private bool wasCursorLocked;

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("PauseMenu");
            Instance = go.AddComponent<PauseMenu>();
            Instance.Build();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        var canvas = UIKit.CreateCanvas("PauseCanvas", 250, scaleWithScreen: true);
        root = canvas.gameObject;
        var scrim = UIKit.Panel("scrim", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.7f));
        scrim.GetComponent<Image>().raycastTarget = true;

        // 居中面板（严格屏幕中央）
        var panel = UIKit.Panel("panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-280f, -180f), new Vector2(280f, 180f),
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));
        panel.GetComponent<Image>().raycastTarget = true;

        var title = UIKit.Text("title", panel, "已暂停", 34, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.MiddleCenter);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -50f);
        title.rectTransform.sizeDelta = new Vector2(400f, 50f);

        float bw = 360f, bx = -bw / 2f;
        // 顶部锚点 (0.5,1)，从面板顶往下排：继续 90..136 / 设置 146..192 / 退出 202..248
        UIKit.CutButton("btnResume", panel, "继 续", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(bx, -136f), new Vector2(bx + bw, -90f), Resume, 18);
        UIKit.CutButton("btnSettings", panel, "设 置", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(bx, -192f), new Vector2(bx + bw, -146f), () =>
            {
                SettingsPanel.Ensure();
            }, 18);
        UIKit.CutButton("btnExit", panel, "退 出", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(bx, -248f), new Vector2(bx + bw, -202f), ExitToMenu, 18);

        root.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (FirstPersonController.Local == null) return; // 不在游戏中（大厅/菜单）不响应
        if (SettingsPanel.Instance != null) return;      // 设置面板打开时暂停键不响应

        if (Keyboard.current[(UnityEngine.InputSystem.Key)SettingsStore.S.keyPause].wasPressedThisFrame)
        {
            if (paused) Resume();
            else Pause();
        }
    }

    private void Pause()
    {
        if (paused) return;
        paused = true;
        wasCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (FirstPersonController.Local != null) FirstPersonController.Local.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (root != null) root.SetActive(true);
    }

    private void Resume()
    {
        if (!paused) return;
        paused = false;
        if (root != null) root.SetActive(false);
        if (FirstPersonController.Local != null)
        {
            var rc = FirstPersonController.Local.GetComponent<RoleComponent>();
            FirstPersonController.Local.SetInputEnabled(rc == null || !rc.IsCorpse);
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ExitToMenu()
    {
        // 直接隐藏暂停界面（不调用 Resume，避免把鼠标锁回 Locked）
        paused = false;
        if (root != null) root.SetActive(false);

        // 停服务器+客户端、重置 GameFlow、解锁鼠标（训练场退出即结束训练场服务器）
        if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.DisconnectToMenu();
    }
}
