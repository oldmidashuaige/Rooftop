using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing;

/// <summary>
/// 房间内备战大厅（终末地风）：扁平布局（直接挂 canvas，(0,1) 锚点 + Screen 坐标）。
/// 顶栏=房间信息+在线人数+房主设置（人数/立即开始）；左栏=队伍聊天（可用）；
/// 中栏=各玩家角色选择框（锁定标记）；右栏=角色卡牌(1-9)+详情+确定/锁定。
/// 开局前不渲染 3D，背景用高分辨率图片。
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance;

    private GameObject root;
    private Text roomInfoText;
    private Text onlineText;
    private Text hostCountText;
    private Text chatText;
    private InputField chatInput;
    private RectTransform chatContentRt;
    private RectTransform centerGrid;
    private RectTransform cardGrid;
    private Text detailText;
    private readonly List<RectTransform> cardHighlights = new List<RectTransform>();
    private int selectedCard = -1;
    private int currentTotal = 6;
    private readonly List<string> chatLines = new List<string>();
    private float centerL, centerR;
    private bool built;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isBatchMode) return;
        Build();
        root.SetActive(false);
    }

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("LobbyUI");
            go.AddComponent<LobbyUI>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        var canvas = UIKit.CreateCanvas("LobbyCanvas", 90);
        root = canvas.gameObject;
        MenuBackground.AddTo(canvas);
        ChatSystem.Ensure();

        float sw = Screen.width, sh = Screen.height;
        float topH = 64f;
        // 列边界
        float leftW = sw * 0.22f;
        centerL = leftW; centerR = sw * 0.72f;
        float rightL = sw * 0.72f;

        // ===== 顶栏 =====
        var bar = UIKit.Panel("topbar", root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -topH), new Vector2(0f, 0f),
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));
        roomInfoText = PlaceText(bar, "房间 --:--", 16, EndfieldTheme.Signal, 20, 16, 320, 34);
        onlineText = PlaceText(bar, "", 15, EndfieldTheme.Ink, centerL, 16, centerR - centerL, 34);
        bool isHost = InstanceFinder.IsServerStarted && InstanceFinder.IsClientStarted;
        if (isHost)
        {
            PlaceText(bar, "玩家数", 14, EndfieldTheme.Ink2, sw - 560f, 20, 70, 30);
            UIKit.CutButton("cntMinus", bar, "−", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(sw - 470f, -46f), new Vector2(sw - 430f, -18f),
                () => SendSetCount(Mathf.Max(2, currentTotal - 1)), 18);
            hostCountText = PlaceText(bar, "6", 16, EndfieldTheme.Signal, sw - 428f, 20, 48, 34);
            UIKit.CutButton("cntPlus", bar, "+", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(sw - 378f, -46f), new Vector2(sw - 338f, -18f),
                () => SendSetCount(Mathf.Min(9, currentTotal + 1)), 18);
            UIKit.CutButton("btnStart", bar, "立即开始", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(sw - 320f, -46f), new Vector2(sw - 190f, -18f),
                () => InstanceFinder.ClientManager.Broadcast(new GameFlow.ForceStartBroadcast { dummy = true }), 16);
        }

        // ===== 左栏：队伍聊天 =====
        var chatPanel = PlacePanel("chatpanel", root.transform, 0f, topH, leftW, sh - topH,
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));
        PlaceText(chatPanel, "队 伍 聊 天", 16, EndfieldTheme.Ink, 14, 14, 200, 28);
        var (_, content, _) = UIKit.ScrollView("chatScroll", chatPanel,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(10f, 56f), new Vector2(-10f, -44f), scrollbarWidth: 8f);
        chatContentRt = content;
        chatText = UIKit.Text("lines", content, "", 14, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Ink, TextAnchor.UpperLeft);
        var crt = chatText.rectTransform;
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(6f, 0f);
        crt.offsetMax = new Vector2(-6f, 0f);
        chatText.horizontalOverflow = HorizontalWrapMode.Wrap;
        chatText.verticalOverflow = VerticalWrapMode.Overflow;
        chatInput = UIKit.Input("chatInput", chatPanel, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(10f, 10f), new Vector2(-80f, 46f), "");
        chatInput.characterLimit = 100;
        UIKit.CutButton("chatSend", chatPanel, "发送", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-72f, 10f), new Vector2(-10f, 46f), () => SendChat(), 14);

        // ===== 中栏：玩家角色框 =====
        var centerPanel = PlacePanel("centerpanel", root.transform, centerL, topH, centerR - centerL, sh - topH, Color.clear);
        PlaceText(centerPanel, "选 择 角 色", 20, EndfieldTheme.Ink, 20, 14, 300, 34);
        centerGrid = UIKit.Panel("grid", centerPanel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(20f, -52f), new Vector2(-20f, -16f), Color.clear);

        // ===== 右栏：角色卡组 =====
        var cardPanel = PlacePanel("cardpanel", root.transform, rightL, topH, sw - rightL, sh - topH,
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));
        PlaceText(cardPanel, "角 色 卡 组", 18, EndfieldTheme.Ink, 18, 14, 220, 32);
        // 卡牌网格（1-9）
        float gridW = (sw - rightL) - 36f;
        float cardW = (gridW - 20f) / 3f;
        float cardH = 62f, cardGap = 10f;
        int total = RoleDef.HumanPool.Length + 1;
        for (int i = 0; i < total; i++)
        {
            int row = i / 3, col = i % 3;
            float x = 18f + col * (cardW + cardGap);
            float y = 52f + row * (cardH + cardGap);
            int idx = i;
            bool isZ = i >= RoleDef.HumanPool.Length;
            string label = isZ ? (i + 1) + " 丧尸" : (i + 1) + " " + RoleDef.DisplayName(RoleDef.HumanPool[i]);
            var card = UIKit.CutButton("card" + i, cardPanel, label,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -(y + cardH)), new Vector2(x + cardW, -y),
                () => SelectCard(idx), 12);
            cardHighlights.Add(card);
        }
        // 详情
        float detailTop = 52f + 3f * (cardH + cardGap) + 8f;
        var detail = UIKit.Panel("detail", cardPanel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(18f, -(detailTop + 140f)), new Vector2(-18f, -detailTop),
            new Color(EndfieldTheme.Chip.r, EndfieldTheme.Chip.g, EndfieldTheme.Chip.b, 0.85f));
        detailText = UIKit.Text("d", detail, "选择一张角色卡…", 14, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Ink, TextAnchor.UpperLeft);
        var drt = detailText.rectTransform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = new Vector2(14f, 14f);
        drt.offsetMax = new Vector2(-14f, -14f);
        // 锁定
        float lockTop = detailTop + 140f + 10f;
        UIKit.CutButton("btnLock", cardPanel, "确定 / 锁定", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -(lockTop + 48f)), new Vector2(-18f, -lockTop), () => LockSelection(), 18);
    }

    private Text PlaceText(Transform parent, string content, int size, Color color, float x, float y, float w, float h)
    {
        var t = UIKit.Text("lbl", parent, content, size, EndfieldTheme.CJKFont, FontStyle.Bold, color, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - h);
        rt.offsetMax = new Vector2(x + w, -y);
        return t;
    }

    /// <summary>点锚定 (0,1) 面板：左上角在 (x, yTop)，宽 w 高 h。</summary>
    private RectTransform PlacePanel(string name, Transform parent, float x, float yTop, float w, float h, Color color)
    {
        return UIKit.Panel(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x, -(yTop + h)), new Vector2(x + w, -yTop), color);
    }

    private void SendSetCount(int total)
    {
        currentTotal = total;
        if (hostCountText != null) hostCountText.text = total.ToString();
        InstanceFinder.ClientManager.Broadcast(new GameFlow.SetPlayerCountBroadcast { total = total });
    }

    private void SendChat()
    {
        if (chatInput == null) return;
        string t = chatInput.text.Trim();
        if (t.Length == 0) return;
        if (ChatSystem.Instance != null) ChatSystem.Instance.SendLocal(t);
        chatInput.text = "";
    }

    public void OnChatMessage(string name, string text)
    {
        if (chatText == null) return;
        chatLines.Add("<color=#66CCFF>" + name + "</color>: " + text);
        if (chatLines.Count > 60) chatLines.RemoveAt(0);
        chatText.text = string.Join("\n", chatLines);
        chatContentRt.sizeDelta = new Vector2(0f, 16f + chatLines.Count * 22f);
    }

    private void SelectCard(int idx)
    {
        selectedCard = idx;
        for (int i = 0; i < cardHighlights.Count; i++)
            if (cardHighlights[i] != null)
                cardHighlights[i].GetComponent<Image>().color = (i == idx) ? EndfieldTheme.Signal : EndfieldTheme.Chip;
        bool isZombie = idx >= RoleDef.HumanPool.Length;
        if (isZombie) detailText.text = "丧尸\n近战感染 · 标记 · 啃食尸体\n（玩家丧尸）";
        else
        {
            var r = RoleDef.HumanPool[idx];
            detailText.text = RoleDef.DisplayName(r) + "\n" + RoleDef.Eyebrow(r) + "\n" + RoleDef.Tool(r);
        }
    }

    private void LockSelection()
    {
        if (selectedCard < 0) return;
        bool isZombie = selectedCard >= RoleDef.HumanPool.Length;
        var cm = InstanceFinder.ClientManager;
        if (isZombie) cm.Broadcast(new GameFlow.RolePickBroadcast { isZombie = true, role = HumanRole.None });
        else cm.Broadcast(new GameFlow.RolePickBroadcast { isZombie = false, role = RoleDef.HumanPool[selectedCard] });
    }

    public void OnLobbyState(GameFlow.LobbyStateBroadcast msg)
    {
        if (root == null) return;
        if (msg.started) { root.SetActive(false); return; }
        if (!root.activeSelf) root.SetActive(true);

        var nm = NetworkGameManager.Instance;
        if (nm != null) roomInfoText.text = "房间  " + nm.CurrentIp + ":" + nm.CurrentPort;
        int humans = 0, zombies = 0, unassigned = 0;
        foreach (var e in msg.entries)
        {
            if (e.isZombie) zombies++;
            else if (e.role != HumanRole.None) humans++;
            else unassigned++;
        }
        onlineText.text = string.Format("在线  {0}/{1}  ·  人类 {2}/{3}  丧尸 {4}/{5}  未选 {6}",
            msg.entries.Length, msg.totalPlayers, humans, msg.humanSlots, zombies, msg.zombieSlots, unassigned);
        currentTotal = msg.totalPlayers;
        if (hostCountText != null) hostCountText.text = currentTotal.ToString();

        // 刷新中栏玩家框
        for (int i = centerGrid.childCount - 1; i >= 0; i--)
            Object.Destroy(centerGrid.GetChild(i).gameObject);
        float gw = (centerR - centerL) - 40f;
        float bw = (gw - 20f) / 3f, bh = 130f, gap = 10f;
        for (int i = 0; i < msg.entries.Length; i++)
        {
            var e = msg.entries[i];
            int row = i / 3, col = i % 3;
            float x = col * (bw + gap), y = row * (bh + gap);
            var box = UIKit.Panel("p" + i, centerGrid, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -(y + bh)), new Vector2(x + bw, -y),
                new Color(EndfieldTheme.Chip.r, EndfieldTheme.Chip.g, EndfieldTheme.Chip.b, 0.85f));
            string roleName = e.isZombie ? "丧尸" : RoleDef.DisplayName(e.role);
            bool chosen = e.isZombie || e.role != HumanRole.None;
            var t = UIKit.Text("t", box, "客户端" + e.clientId + "  " + roleName + (chosen ? "  ✓" : "  （未选）"), 14,
                EndfieldTheme.CJKFont, FontStyle.Bold, chosen ? EndfieldTheme.Signal : EndfieldTheme.Ink2, TextAnchor.MiddleCenter);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
