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
        // scaleWithScreen=true：CanvasScaler 按屏幕缩放（参考 1920x1080），布局随窗口大小自适应
        canvas = UIKit.CreateCanvas("MainMenuCanvas", 100, scaleWithScreen: true);
        root = canvas.gameObject;

        // 全屏半透明背景（露出后面的 3D 场景，略暗以便阅读）
        var paperColor = new Color(EndfieldTheme.Paper.r, EndfieldTheme.Paper.g, EndfieldTheme.Paper.b, 0.88f);
        var paper = UIKit.Panel("paper", root.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, paperColor);
        paper.GetComponent<Image>().raycastTarget = true;

        // 顶部三色条（锚点铺满全宽，随分辨率自适应）
        UIKit.Panel("triA", root.transform, new Vector2(0f, 1f), new Vector2(1f / 3f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.ParseHex("FF00F0"));
        UIKit.Panel("triB", root.transform, new Vector2(1f / 3f, 1f), new Vector2(2f / 3f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.Signal);
        UIKit.Panel("triC", root.transform, new Vector2(2f / 3f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -40f), new Vector2(0f, -37f), EndfieldTheme.ParseHex("00FFA2"));

        // 眉标 / 大标题 / 副标题
        UILabel("// PROJECT.ROOFTOP", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 13, FontStyle.Bold, 60, 150, 400, 18);
        UILabel("天 台", EndfieldTheme.Ink, EndfieldTheme.CJKFont, 44, FontStyle.Bold, 60, 168, 500, 56);
        UILabel("不对称射击恐怖 · 一份构建三种角色", EndfieldTheme.Ink2, EndfieldTheme.CJKFont, 15, FontStyle.Normal, 62, 232, 600, 22);

        // 细线（锚点铺满全宽）
        UIKit.HLine("rule1", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(60f, -291f), new Vector2(-60f, -290f), EndfieldTheme.Rule);

        // 输入区：标签在输入框正上方（标签 312..330，输入框顶 336，留 6px 间距，避免重叠）
        UILabel("SERVER IP", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Bold, 60, 312, 200, 18);
        ipField = UIKit.Input("ipInput", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -368f), new Vector2(380f, -336f), "127.0.0.1");

        UILabel("PORT", EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Bold, 400, 312, 120, 18);
        portField = UIKit.Input("portInput", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(400f, -368f), new Vector2(520f, -336f), "7770");

        // 按钮（在输入框下方留足间距：输入框底 368 → 按钮顶 400，32px 空隙）
        UIKit.CutButton("btnHost", root.transform, "创建房间 · 房主", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -446f), new Vector2(420f, -400f), () =>
            {
                if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.HostGame();
            });
        UIKit.CutButton("btnJoin", root.transform, "加入房间", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -512f), new Vector2(420f, -466f), () =>
            {
                if (NetworkGameManager.Instance == null) return;
                ushort port = 7770;
                ushort.TryParse(portField.text, out port);
                NetworkGameManager.Instance.JoinGame(ipField.text, port);
            });
        UIKit.CutButton("btnServer", root.transform, "启动专用服务器（本机测试）", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, -578f), new Vector2(420f, -532f), () =>
            {
                if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.StartDedicatedServer();
            });

        // 状态
        statusText = UIKit.Text("status", root.transform, "", 15, EndfieldTheme.CJKFont, FontStyle.Normal,
            EndfieldTheme.Signal, TextAnchor.MiddleLeft);
        statusText.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusText.rectTransform.anchorMax = new Vector2(0f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(60f, -650f);
        statusText.rectTransform.offsetMax = new Vector2(500f, -610f);

        // 版尾字段（参考高度 1080，锚点贴底）
        var foot = UIKit.Text("foot", root.transform, "AGPL-3.0  │  FishNet 4.7  │  URP 17.6",
            12, EndfieldTheme.UIFont, FontStyle.Normal, EndfieldTheme.Ink2, TextAnchor.LowerLeft);
        foot.rectTransform.anchorMin = new Vector2(0f, 0f);
        foot.rectTransform.anchorMax = new Vector2(0f, 0f);
        foot.rectTransform.offsetMin = new Vector2(60f, 24f);
        foot.rectTransform.offsetMax = new Vector2(560f, 42f);
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
