using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using FishNet.Managing;

/// <summary>
/// 选角大厅（客户端 uGUI·终末地风）：8 名人类角色池 + 1 个丧尸位，点击选择。
/// 显示当前选择进度；开局后隐藏。
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance;

    private GameObject root;
    private readonly List<RectTransform> buttons = new List<RectTransform>();
    private Text statusText;
    private Text slotText;
    private bool built;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isBatchMode) return;
        Build();
        root.SetActive(false); // 连接后收到 LobbyState 才显示
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

        UIKit.Panel("paper", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, EndfieldTheme.Paper)
            .GetComponent<Image>().raycastTarget = true;

        float w = Screen.width;

        // 三色条
        UIKit.Panel("triA", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -40f), new Vector2(w / 3f, -37f), EndfieldTheme.ParseHex("FF00F0"));
        UIKit.Panel("triB", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(w / 3f, -40f), new Vector2(w * 2f / 3f, -37f), EndfieldTheme.Signal);
        UIKit.Panel("triC", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(w * 2f / 3f, -40f), new Vector2(w, -37f), EndfieldTheme.ParseHex("00FFA2"));

        Label("// SELECT ROLE", 60, 150, EndfieldTheme.Ink2, EndfieldTheme.UIFont, 13, FontStyle.Bold);
        Label("选择你的角色", 60, 168, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 34, FontStyle.Bold);

        // 角色网格：8 人类 + 1 丧尸 = 9 格，每行 3 个
        float gx = 60f, gy = 260f, gw = 300f, gh = 74f, gap = 12f;
        int idx = 0;
        for (int i = 0; i < RoleDef.HumanPool.Length; i++)
        {
            HumanRole r = RoleDef.HumanPool[i];
            var btn = MakeRoleButton(r, gx + (idx % 3) * (gw + gap), gy + (idx / 3) * (gh + gap), gw, gh);
            buttons.Add(btn);
            idx++;
        }
        // 丧尸位
        var zombieBtn = MakeZombieButton(gx + (idx % 3) * (gw + gap), gy + (idx / 3) * (gh + gap), gw, gh);
        buttons.Add(zombieBtn);

        slotText = Label("", 60, gy + 4 * (gh + gap) + 10, EndfieldTheme.Ink, EndfieldTheme.CJKFont, 16, FontStyle.Normal);
        statusText = Label("", 60, gy + 4 * (gh + gap) + 40, EndfieldTheme.Signal, EndfieldTheme.CJKFont, 16, FontStyle.Bold);

        // 立即开始（测试/少人时用：未满员也能开局，未选角色自动分配）
        UIKit.CutButton("btnForceStart", root.transform, "立即开始（未满也能开）",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60f, gy + 4 * (gh + gap) + 80f), new Vector2(320f, gy + 4 * (gh + gap) + 126f),
            () =>
            {
                var cm = FishNet.InstanceFinder.ClientManager;
                cm.Broadcast(new GameFlow.ForceStartBroadcast { dummy = true });
            }, 16);
    }

    private Text Label(string content, float x, float y, Color color, Font font, int size, FontStyle style)
    {
        var t = UIKit.Text("lbl", root.transform, content, size, font, style, color, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - 26f);
        rt.offsetMax = new Vector2(x + 900f, -y);
        return t;
    }

    private RectTransform MakeRoleButton(HumanRole r, float x, float y, float w, float h)
    {
        var btn = UIKit.CutButton("role_" + r + "_" + RoleCountBefore(r), root.transform,
            RoleDef.DisplayName(r) + "  " + RoleDef.Tool(r),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y - h), new Vector2(x + w, -y),
            () =>
            {
                var cm = InstanceFinder.ClientManager;
                cm.Broadcast(new GameFlow.RolePickBroadcast { isZombie = false, role = r });
            }, 15);
        return btn;
    }

    private RectTransform MakeZombieButton(float x, float y, float w, float h)
    {
        return UIKit.CutButton("role_ZOMBIE", root.transform, "玩家丧尸",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y - h), new Vector2(x + w, -y),
            () =>
            {
                var cm = InstanceFinder.ClientManager;
                cm.Broadcast(new GameFlow.RolePickBroadcast { isZombie = true, role = HumanRole.None });
            }, 15);
    }

    private static int RoleCountBefore(HumanRole r)
    {
        int n = 0;
        for (int i = 0; i < RoleDef.HumanPool.Length; i++)
            if (RoleDef.HumanPool[i] == r) n++;
        return n;
    }

    public void OnLobbyState(GameFlow.LobbyStateBroadcast msg)
    {
        if (root == null) return;
        if (msg.started)
        {
            root.SetActive(false);
            return;
        }

        if (!root.activeSelf) root.SetActive(true);

        // 刷新已选状态：已满的角色格子标强调色
        int humans = 0, zombies = 0;
        foreach (var e in msg.entries)
        {
            if (e.isZombie) zombies++;
            else humans++;
        }
        slotText.text = $"已选：{humans}/{msg.humanSlots} 人类  ·  {zombies}/{msg.zombieSlots} 丧尸";

        var taken = new Dictionary<HumanRole, int>();
        foreach (var e in msg.entries)
            if (!e.isZombie) taken[e.role] = taken.TryGetValue(e.role, out var v) ? v + 1 : 1;

        for (int i = 0; i < RoleDef.HumanPool.Length; i++)
        {
            HumanRole r = RoleDef.HumanPool[i];
            int count = taken.TryGetValue(r, out var n) ? n : 0;
            bool full = count >= RoleLimitLocal(r);
            SetButtonHighlight(buttons[i], full);
        }
        SetButtonHighlight(buttons[RoleDef.HumanPool.Length], zombies >= 1);

        bool full2 = humans >= msg.humanSlots && zombies >= msg.zombieSlots;
        statusText.text = full2
            ? "人数已满，即将自动开局…（未选角色将自动分配）"
            : "选择角色（人类 " + msg.humanSlots + " 名 + 丧尸 " + msg.zombieSlots + " 名）";
    }

    private static int RoleLimitLocal(HumanRole r) => r == HumanRole.Student ? 4 : 1;

    private void SetButtonHighlight(RectTransform btn, bool taken)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = taken ? EndfieldTheme.Signal : EndfieldTheme.Chip;
        }
    }
}
