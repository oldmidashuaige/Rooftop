using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Transporting;

/// <summary>
/// 主菜单（终末地风 · 三栏布局，占满全屏）：
/// 左栏=主导航（对局/角色/故事/设置/离开）；中栏=历史房间表+快速加入+三大模式；右栏=世界聊天（UI 占位）。
/// 开局前不渲染 3D 场景，背景用高分辨率图片（MenuBackground）。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private Canvas canvas;
    private GameObject root;
    private InputField ipField;
    private InputField portField;
    private Text statusText;
    private readonly List<RectTransform> navBlocks = new List<RectTransform>();
    private int activeNav;
    private RectTransform battleRoot;
    private RectTransform charRoot;
    private RectTransform storyRoot;
    private RectTransform historyBody;

    private void Awake()
    {
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
            SetStatus("已连接，等待出生…");
            if (root != null) root.SetActive(false); // 交给大厅
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            SetStatus("连接失败：可能是版本不对或地址错误");
            if (root != null) root.SetActive(true);
        }
    }

    private void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
    }

    // ============ 构建 ============

    private void BuildUI()
    {
        UIKit.EnsureEventSystem();
        canvas = UIKit.CreateCanvas("MainMenuCanvas", 100, scaleWithScreen: true);
        root = canvas.gameObject;
        MenuBackground.AddTo(canvas);

        BuildLeftColumn();
        BuildCenterColumn();
        BuildRightColumn();
        SetNav(0);
    }

    // —— 左栏：主导航 ——

    private void BuildLeftColumn()
    {
        var col = UIKit.Panel("nav", root.transform,
            new Vector2(0f, 0f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero,
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));

        // 顶部标题
        Label(col, "// PROJECT.ROOFTOP", 18, 30, 200, 18, EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Bold);
        Label(col, "天 台", 18, 52, 200, 40, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 28, FontStyle.Bold);
        UIKit.HLine("rule", col, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(18f, -104f), new Vector2(-18f, -103f), EndfieldTheme.Rule);

        // 导航项（均匀分布：对局/角色/故事/设置/离开）
        string[] labels = { "对局", "角色", "故事", "设置", "离开" };
        float itemH = 52f;
        float gap = 10f;
        float startY = 140f;
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var btn = UIKit.CutButton("nav" + i, col, labels[i],
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -(startY + i * (itemH + gap) + itemH)), new Vector2(-18f, -(startY + i * (itemH + gap))),
                () => SetNav(idx), 20);

            // 左侧高亮块（激活时显示）
            var block = UIKit.Panel("hl" + i, btn, new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(4f, 0f), EndfieldTheme.Signal);
            block.gameObject.SetActive(false);
            navBlocks.Add(block);
        }
    }

    private void Label(Transform parent, string content, float x, float y, float w, float h, Color color, Font font, int size, FontStyle style)
    {
        var t = UIKit.Text("lbl", parent, content, size, font, style, color, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - h);
        rt.offsetMax = new Vector2(x + w, -y);
    }

    private void SetNav(int idx)
    {
        activeNav = idx;
        for (int i = 0; i < navBlocks.Count; i++)
            if (navBlocks[i] != null) navBlocks[i].gameObject.SetActive(i == idx);
        if (battleRoot != null) battleRoot.gameObject.SetActive(idx == 0);
        if (charRoot != null) charRoot.gameObject.SetActive(idx == 1);
        if (storyRoot != null) storyRoot.gameObject.SetActive(idx == 2);
        if (idx == 3) { SettingsPanel.Ensure(); } // 设置
        if (idx == 4) { Application.Quit(); }     // 离开
    }

    // —— 中栏：对局内容 ——

    private void BuildCenterColumn()
    {
        var col = UIKit.Panel("center", root.transform,
            new Vector2(0.18f, 0f), new Vector2(0.75f, 1f), Vector2.zero, Vector2.zero, Color.clear);

        battleRoot = BuildBattlePanel(col);
        charRoot = BuildPlaceholder(col, "角 色", "角色图鉴 / 卡牌管理 / 属性查看（敬请期待）");
        storyRoot = BuildPlaceholder(col, "故 事", "单机战役模式入口（敬请期待）");
    }

    private RectTransform BuildBattlePanel(RectTransform parent)
    {
        var rootRt = UIKit.Panel("battle", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
        float lx = 40f, rx = 40f;

        // 标题
        Label(rootRt, "// ROOM & MATCH", lx, 40, 500, 18, EndfieldTheme.Ink2, EndfieldTheme.UIFont, 13, FontStyle.Bold);
        Label(rootRt, "对 局", lx, 62, 400, 40, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 30, FontStyle.Bold);
        statusText = UIKit.Text("status", rootRt, "", 15, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Signal, TextAnchor.MiddleLeft);
        statusText.rectTransform.anchorMin = new Vector2(1f, 1f);
        statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(-420f, -60f);
        statusText.rectTransform.offsetMax = new Vector2(-lx, -26f);

        // —— 历史房间表 ——
        Label(rootRt, "历史房间", lx, 120, 300, 26, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 20, FontStyle.Bold);
        float tableTop = 158f;
        float tableH = 220f;

        // 表头
        MakeTableHeader(rootRt, lx, tableTop, tableH);
        // 表体（可滚动）
        var (vp, content, _) = UIKit.ScrollView("historyScroll", rootRt,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(lx, -(tableTop + 32f + tableH)), new Vector2(-rx, -(tableTop + 32f)),
            scrollbarWidth: 12f);
        historyBody = content;
        RefreshHistory();

        // —— 快速加入 ——
        float qy = tableTop + 32f + tableH + 30f;
        Label(rootRt, "快速加入 / 自定义 IP", lx, qy, 300, 24, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 18, FontStyle.Bold);
        float inputY = qy + 34f;
        ipField = UIKit.Input("ipInput", rootRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx, -(inputY + 44f)), new Vector2(lx + 300f, -(inputY)), "127.0.0.1");
        portField = UIKit.Input("portInput", rootRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx + 310f, -(inputY + 44f)), new Vector2(lx + 410f, -(inputY)), "7770");
        UIKit.CutButton("btnJoin", rootRt, "加 入", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx + 424f, -(inputY + 44f)), new Vector2(lx + 534f, -(inputY)),
            () =>
            {
                if (NetworkGameManager.Instance == null) return;
                ushort port = 7770;
                ushort.TryParse(portField.text, out port);
                NetworkGameManager.Instance.JoinGame(ipField.text, port);
            }, 16);

        // —— 三大模式 ——
        float modeY = inputY + 80f;
        float modeH = 120f;
        float cardW = 250f, cardGap = 30f;
        // 训练厂
        UIKit.CutButton("modeTrain", rootRt, "训练厂\n单机练习 / 试炼场", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx, -(modeY + modeH)), new Vector2(lx + cardW, -(modeY)),
            () => { if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.StartTraining(); }, 22);
        // 房主开房
        UIKit.CutButton("modeHost", rootRt, "房主开房\n创建自定义房间", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx + cardW + cardGap, -(modeY + modeH)), new Vector2(lx + cardW * 2 + cardGap, -(modeY)),
            () => { if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.HostGame(); }, 22);
        // 官方匹配
        UIKit.CutButton("modeMatch", rootRt, "官方匹配\n系统自动匹配对手", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(lx + (cardW + cardGap) * 2, -(modeY + modeH)), new Vector2(lx + cardW * 3 + cardGap * 2, -(modeY)),
            () => SetStatus("官方匹配：敬请期待"), 22);

        // 版尾
        Label(rootRt, "AGPL-3.0  │  FishNet 4.7  │  URP 17.6", lx, 980, 600, 20, EndfieldTheme.Ink2, EndfieldTheme.UIFont, 12, FontStyle.Normal);
        return rootRt;
    }

    private void MakeTableHeader(RectTransform parent, float lx, float topY, float h)
    {
        float[] widths = { 0.30f, 0.20f, 0.12f, 0.18f, 0.20f };
        string[] heads = { "房主 / 房间", "IP", "端口", "时间", "加入" };
        float x = lx;
        for (int i = 0; i < widths.Length; i++)
        {
            float w = 720f * widths[i]; // 表格总宽约 720
            var cell = UIKit.Panel("h" + i, parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -(topY + 26f)), new Vector2(x + w, -(topY)),
                new Color(EndfieldTheme.Chip.r, EndfieldTheme.Chip.g, EndfieldTheme.Chip.b, 0.8f));
            var t = UIKit.Text("ht", cell, heads[i], 14, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Ink2, TextAnchor.MiddleCenter);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            x += w;
        }
    }

    private void RefreshHistory()
    {
        if (historyBody == null) return;
        // 清空旧行
        for (int i = historyBody.childCount - 1; i >= 0; i--)
            Object.Destroy(historyBody.GetChild(i).gameObject);

        var recs = RoomsHistory.Records;
        float[] widths = { 0.30f, 0.20f, 0.12f, 0.18f, 0.20f };
        float rowH = 34f;
        for (int i = 0; i < recs.Count; i++)
        {
            var r = recs[i];
            var row = UIKit.Panel("row" + i, historyBody, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -(i * rowH + rowH)), new Vector2(720f, -(i * rowH)),
                (i % 2 == 0) ? new Color(0f, 0f, 0f, 0.25f) : new Color(0f, 0f, 0f, 0.12f));
            row.gameObject.AddComponent<RowHover>();

            string[] cells = { r.name, r.ip, r.port, r.time };
            float x = 0f;
            for (int c = 0; c < 4; c++)
            {
                float w = 720f * widths[c];
                var cell = UIKit.Panel("c" + c, row, Vector2.zero, Vector2.one,
                    new Vector2(x, 0f), new Vector2(x + w, 0f), Color.clear);
                var t = UIKit.Text("t", cell, cells[c], 14, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Ink, TextAnchor.MiddleCenter);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = Vector2.zero;
                t.rectTransform.offsetMax = Vector2.zero;
                x += w;
            }
            // 加入按钮
            string ip = r.ip, port = r.port;
            UIKit.CutButton("join", row, "加入", new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(720f * 0.80f, 3f), new Vector2(720f - 3f, -3f),
                () =>
                {
                    if (NetworkGameManager.Instance == null) return;
                    ushort p = 7770;
                    ushort.TryParse(port, out p);
                    NetworkGameManager.Instance.JoinGame(ip, p);
                }, 13);
        }
        // 更新 content 高度
        historyBody.sizeDelta = new Vector2(0f, recs.Count * rowH + 10f);
    }

    private RectTransform BuildPlaceholder(RectTransform parent, string title, string desc)
    {
        var rt = UIKit.Panel("ph", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
        Label(rt, title, 40, 120, 400, 40, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 30, FontStyle.Bold);
        Label(rt, desc, 40, 180, 700, 26, EndfieldTheme.Ink2, EndfieldTheme.CJKFont, 18, FontStyle.Normal);
        // 空按钮预留
        UIKit.CutButton("phBtn", rt, "预留按钮", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -260f), new Vector2(260f, -210f), () => { }, 16);
        rt.gameObject.SetActive(false);
        return rt;
    }

    // —— 右栏：世界聊天 ——

    private void BuildRightColumn()
    {
        var col = UIKit.Panel("chat", root.transform,
            new Vector2(0.75f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.96f));

        // 顶部标签页
        UIKit.Panel("tabBar", col, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -56f), new Vector2(0f, -10f), Color.clear);
        RectTransform tabWorld = MakeTab(col, "世界", 20f, 0);
        RectTransform tabFriend = MakeTab(col, "好友", 120f, 1);
        _ = tabWorld; _ = tabFriend;

        // 聊天内容区（可滚动）
        var (vp, content, _) = UIKit.ScrollView("chatScroll", col,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(16f, -76f), new Vector2(-16f, -96f), scrollbarWidth: 10f);
        _ = vp;
        var hint = UIKit.Text("hint", content, "世界频道（UI 占位，暂不发送）", 14, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Ink2, TextAnchor.UpperLeft);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(0f, 1f);
        hrt.offsetMin = new Vector2(4f, -20f);
        hrt.offsetMax = new Vector2(300f, 0f);
        content.sizeDelta = new Vector2(0f, 30f);

        // 底部输入区
        var chatInput = UIKit.Input("chatInput", col, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(16f, 16f), new Vector2(-92f, 56f), "");
        _ = chatInput;
        UIKit.CutButton("chatSend", col, "发送", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-80f, 16f), new Vector2(-16f, 56f), () => { }, 15);
    }

    private RectTransform MakeTab(RectTransform parent, string label, float x, int idx)
    {
        var rt = UIKit.Panel("tab" + idx, parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x, -48f), new Vector2(x + 90f, -12f),
            idx == 0 ? new Color(EndfieldTheme.Signal.r, EndfieldTheme.Signal.g, EndfieldTheme.Signal.b, 0.25f) : Color.clear);
        var t = UIKit.Text("t", rt, label, 18, EndfieldTheme.CJKFont, FontStyle.Bold,
            idx == 0 ? EndfieldTheme.Signal : EndfieldTheme.Ink2, TextAnchor.MiddleCenter);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();
        btn.onClick.AddListener(() =>
        {
            // 简单切换高亮（世界=0/好友=1，此处仅视觉）
            Color on = new Color(EndfieldTheme.Signal.r, EndfieldTheme.Signal.g, EndfieldTheme.Signal.b, 0.25f);
            foreach (Transform c in parent)
            {
                var img = c.GetComponent<Image>();
                if (img != null && c.name.StartsWith("tab"))
                    img.color = Color.clear;
            }
            rt.GetComponent<Image>().color = on;
        });
        return rt;
    }
}

/// <summary>历史表格行 hover 高亮。</summary>
public class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image img;
    private Color baseColor;

    private void Awake()
    {
        img = GetComponent<Image>();
        if (img != null) baseColor = img.color;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (img != null) img.color = new Color(EndfieldTheme.Signal.r, EndfieldTheme.Signal.g, EndfieldTheme.Signal.b, 0.22f);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (img != null) img.color = baseColor;
    }
}
