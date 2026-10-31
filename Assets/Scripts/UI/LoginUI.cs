using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动登录界面（离线）：只需输入用户名即可进入。
/// 用户名存入设置文件，作为游戏内头顶显示名。登录后销毁本界面，露出主菜单。
/// </summary>
public class LoginUI : MonoBehaviour
{
    private Canvas canvas;
    private GameObject root;
    private InputField nameField;

    private void Awake()
    {
        if (Application.isBatchMode) { Destroy(gameObject); return; } // 服务器不需要登录

        // 命令行模式（-join/-host/-server/-train/-autotest）：跳过登录，直接用默认用户名。
        // 否则登录面板会不透明地盖住整个游戏画面，导致"看不到游戏/HUD、动不了"。
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "-join" || a == "-host" || a == "-server" || a == "-train" || a == "-autotest" || a == "-menu")
            {
                if (string.IsNullOrEmpty(SettingsStore.S.username))
                    SettingsStore.S.username = "玩家";
                Debug.Log("[Login] 命令行模式，跳过登录界面 (username=" + SettingsStore.S.username + ")");
                Destroy(gameObject);
                return;
            }
        }

        Build();
    }

    private void Build()
    {
        UIKit.EnsureEventSystem();
        canvas = UIKit.CreateCanvas("LoginCanvas", 200, scaleWithScreen: true);
        root = canvas.gameObject;
        MenuBackground.AddTo(canvas);

        var paperColor = new Color(EndfieldTheme.Paper.r, EndfieldTheme.Paper.g, EndfieldTheme.Paper.b, 0.96f);
        var paper = UIKit.Panel("paper", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, paperColor);
        paper.GetComponent<Image>().raycastTarget = true;

        // 顶部三色条
        UIKit.Panel("triA", root.transform, new Vector2(0f, 1f), new Vector2(1f / 3f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.ParseHex("FF00F0"));
        UIKit.Panel("triB", root.transform, new Vector2(1f / 3f, 1f), new Vector2(2f / 3f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.Signal);
        UIKit.Panel("triC", root.transform, new Vector2(2f / 3f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.ParseHex("00FFA2"));

        // 居中面板：中心锚点，尺寸 760x360（顶部 +180，底部 -180）
        var panel = UIKit.Panel("panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-380f, -180f), new Vector2(380f, 180f),
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.95f));
        panel.GetComponent<Image>().raycastTarget = true;

        // 内容统一从面板顶部 (0.5,1) 往下排
        Label(panel, "// LOGIN", 0f, 26f, EndfieldTheme.Ink2, EndfieldTheme.UIFont, 13, FontStyle.Bold);
        Label(panel, "登 录", 0f, 64f, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 34, FontStyle.Bold);
        Label(panel, "离线模式 · 输入用户名即可进入", 0f, 110f, EndfieldTheme.Ink2, EndfieldTheme.CJKFont, 14, FontStyle.Normal);

        // 用户名输入框（面板顶部往下 148..182）
        nameField = UIKit.Input("nameInput", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-260f, -182f), new Vector2(260f, -148f), SettingsStore.S.username);

        // 登录按钮（面板顶部往下 208..254）
        UIKit.CutButton("btnLogin", panel, "进 入", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-260f, -254f), new Vector2(260f, -208f), OnLogin, 20);

        // 版尾
        var foot = UIKit.Text("foot", root.transform, "PROJECT.ROOFTOP  ·  离线登录", 12,
            EndfieldTheme.UIFont, FontStyle.Normal, EndfieldTheme.Ink2, TextAnchor.LowerCenter);
        foot.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        foot.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        foot.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        foot.rectTransform.sizeDelta = new Vector2(400f, 20f);
    }

    private void Label(Transform parent, string content, float x, float y, Color color, Font font, int size, FontStyle style)
    {
        var t = UIKit.Text("lbl", parent, content, size, font, style, color, TextAnchor.MiddleCenter);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -y); // top 锚点：y 向下为负
        rt.sizeDelta = new Vector2(700f, 30f);
    }

    private void OnLogin()
    {
        string name = nameField != null ? nameField.text.Trim() : "";
        if (string.IsNullOrEmpty(name)) name = "玩家";
        if (name.Length > 16) name = name.Substring(0, 16);

        SettingsStore.S.username = name;
        SettingsStore.Save();
        Debug.Log("[Login] 登录为: " + name);

        if (root != null) Destroy(root);
        Destroy(gameObject);
    }
}
