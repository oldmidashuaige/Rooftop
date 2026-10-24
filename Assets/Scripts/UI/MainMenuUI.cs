using UnityEngine;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Transporting;

/// <summary>
/// 主菜单（uGUI·终末地风）：标题 + IP/端口 + 创建/加入/专用服务器。
/// 布局统一用"锚点(0,1) + offset"从左上排布。驱动 NetworkGameManager。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private Canvas canvas;
    private GameObject root;
    private InputField ipField;
    private InputField portField;
    private Text statusText;

    private void Awake()
    {
        // 专用服务器（batchmode/headless）不建菜单
        if (Application.isBatchMode) return;
        BuildUI();
    }

    private void Start()
    {
        var cm = FishNet.InstanceFinder.ClientManager;
        if (cm != null)
            cm.OnClientConnectionState += HandleClientState;
    }

    private void OnDestroy()
    {
        var cm = FishNet.InstanceFinder.ClientManager;
        if (cm == null) return;
        cm.OnClientConnectionState -= HandleClientState;
    }

    private void HandleClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            SetStatus("已连接，等待认证/出生…");
            if (root != null) root.SetActive(false); // 交给大厅
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            SetStatus("已断开（版本不匹配？）");
            if (root != null) root.SetActive(true);
        }
    }

    private void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
    }

    private void BuildUI()
    {
        UIKit.EnsureEventSystem();
        canvas = UIKit.CreateCanvas("MainMenuCanvas", 100);
        root = canvas.gameObject;

        // 全屏纸色背景
        var paper = UIKit.Panel("paper", root.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, EndfieldTheme.Paper);
        paper.GetComponent<Image>().raycastTarget = true;

        float w = Screen.width;

        // 顶部三色条
        UIKit.Panel("triA", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -40f), new Vector2(w / 3f, -40f + 3f), EndfieldTheme.ParseHex("FF00F0"));
        UIKit.Panel("triB", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(w / 3f, -40f), new Vector2(w * 2f / 3f, -37f), EndfieldTheme.Signal);
        UIKit.Panel("triC", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(w * 2f / 3f, -40f), new Vector2(w, -37f), EndfieldTheme.ParseHex("00FFA2"));

        // 眉标
        UILabel("// PROJECT.ROOFTOP", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 13, FontStyle.Bold, 60, 150, 400, 18);
        // 大标题
        UILabel("天 台", EndfieldTheme.Ink, EndfieldTheme.CJKFont, 44, FontStyle.Bold, 60, 168, 500, 56);
        UILabel("不对称射击恐怖 · 一份构建三种角色", EndfieldTheme.Ink2, EndfieldTheme.CJKFont, 15, FontStyle.Normal, 62, 232, 600, 22);

        // 细线
        UIKit.HLine("rule1", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -280f), new Vector2(w - 60f, -279f), EndfieldTheme.Rule);

        // 输入区
        ipField = UIKit.Input("ipInput", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -340f), new Vector2(380f, -306f), "127.0.0.1");
        portField = UIKit.Input("portInput", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(400f, -340f), new Vector2(520f, -306f), "7770");

        UILabel("SERVER IP", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Bold, 60, 312, 200, 18);
        UILabel("PORT", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Bold, 400, 312, 120, 18);

        // 按钮
        UIKit.CutButton("btnHost", root.transform, "创建房间 · 房主", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -380f), new Vector2(420f, -334f), () =>
            {
                if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.HostGame();
            });
        UIKit.CutButton("btnJoin", root.transform, "加入房间", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -450f), new Vector2(420f, -404f), () =>
            {
                if (NetworkGameManager.Instance == null) return;
                ushort port = 7770;
                ushort.TryParse(portField.text, out port);
                NetworkGameManager.Instance.JoinGame(ipField.text, port);
            });
        UIKit.CutButton("btnServer", root.transform, "启动专用服务器（本机测试）", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -520f), new Vector2(420f, -474f), () =>
            {
                if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.StartDedicatedServer();
            });

        // 状态
        statusText = UIKit.Text("status", root.transform, "", 15, EndfieldTheme.CJKFont, FontStyle.Normal,
            EndfieldTheme.Signal, TextAnchor.MiddleLeft);
        statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusText.rectTransform.anchorMax = new Vector2(0f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(60f, -600f);
        statusText.rectTransform.offsetMax = new Vector2(500f, -560f);

        // 版尾字段
        UILabel("AGPL-3.0  │  FishNet 4.7  │  URP 17.6", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12,
            FontStyle.Normal, 60, Screen.height - 40, 500, 18);
    }

    /// <summary>在左上锚点坐标系放置一个文本（x 从左、y 向下）。</summary>
    private void UILabel(string content, Color color, Font font, int size, FontStyle style,
        float x, float y, float w, float h)
    {
        var t = UIKit.Text("lbl", root.transform, content, size, font, style, color, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - h);
        rt.offsetMax = new Vector2(x + w, -y);
        t.name = "lbl_" + content.Substring(0, System.Math.Min(content.Length, 8));
    }
}
