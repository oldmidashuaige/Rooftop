using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 设置面板（客户端）：鼠标灵敏度、准星（中心点/横线/竖线/长度/粗细/颜色手动输入+实时色块）、
/// 窗口模式、键位重绑。内容可滚动（滚轮/侧边滑块）。改动即应用并写入设置文件。
/// 返回：顶部"返回"按钮或 Esc（重绑时 Esc 取消重绑）。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance;

    private GameObject root;
    private RectTransform content;
    private float contentH;

    private readonly List<KeyBindRow> keyRows = new List<KeyBindRow>();
    private KeyBindRow rebindRow;
    private readonly List<System.Action> refreshers = new List<System.Action>();

    private InputField colorInput;
    private Image colorSwatch;
    private Text windowValue;

    private static readonly string[] ColorPresets = { "#EDEDED", "#66CCFF", "#00FFA2", "#FFFA00", "#FF8A7A", "#FFFFFF" };
    private static readonly string[] WindowNames = { "窗口", "全屏", "无边框" };

    private class KeyBindRow
    {
        public string actionName;
        public Text keyText;
        public System.Func<int> get;
        public System.Action<int> set;
    }

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("SettingsPanel");
            Instance = go.AddComponent<SettingsPanel>();
            Instance.Build();
        }
    }

    public static void Close()
    {
        if (Instance != null)
        {
            if (Instance.root != null) Destroy(Instance.root);
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        var canvas = UIKit.CreateCanvas("SettingsCanvas", 300, scaleWithScreen: true);
        root = canvas.gameObject;

        var scrim = UIKit.Panel("scrim", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.82f));
        scrim.GetComponent<Image>().raycastTarget = true;

        // 左侧面板（占左 1200 宽、全高）
        var panel = UIKit.Panel("panel", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1200f, 0f),
            new Color(EndfieldTheme.Panel.r, EndfieldTheme.Panel.g, EndfieldTheme.Panel.b, 0.97f));
        panel.GetComponent<Image>().raycastTarget = true;

        // 标题栏
        var title = UIKit.Text("title", panel, "设 置", 30, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.MiddleLeft);
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(0f, 1f);
        tr.offsetMin = new Vector2(60f, -70f);
        tr.offsetMax = new Vector2(400f, -20f);

        // 返回按钮（右上角）
        UIKit.CutButton("btnBack", panel, "◀ 返回", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-180f, -56f), new Vector2(-40f, -16f), Close, 16);

        // 滚动视图（标题栏下方）
        var (viewport, contentRt, scrollbar) = UIKit.ScrollView("scroll", panel,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(60f, 24f), new Vector2(-30f, -88f), 14f);
        content = contentRt;
        contentH = 0f;

        float x = 0f, y = 0f, w = 1000f, gap = 10f;

        // —— 鼠标灵敏度 ——
        y = Section(x, y, "鼠标灵敏度"); y += 6f;
        y = Stepper(x, y, "灵敏度", () => SettingsStore.S.mouseSensitivity, v => SettingsStore.S.mouseSensitivity = v,
            0.02f, 0.01f, 0.5f, "{0:0.00}");
        y += gap;

        // —— 准星 ——
        y = Section(x, y, "准星"); y += 6f;
        y = Toggle(x, y, "中心点", () => SettingsStore.S.crosshairCenter, v => SettingsStore.S.crosshairCenter = v);
        y += gap;
        y = Toggle(x, y, "水平线", () => SettingsStore.S.crosshairHorizontal, v => SettingsStore.S.crosshairHorizontal = v);
        y += gap;
        y = Toggle(x, y, "竖直线", () => SettingsStore.S.crosshairVertical, v => SettingsStore.S.crosshairVertical = v);
        y += gap;
        y = Stepper(x, y, "长度", () => SettingsStore.S.crosshairLength, v => SettingsStore.S.crosshairLength = v,
            1f, 2f, 40f, "{0:0}");
        y += gap;
        y = Stepper(x, y, "粗细", () => SettingsStore.S.crosshairThickness, v => SettingsStore.S.crosshairThickness = v,
            1f, 1f, 12f, "{0:0}");
        y += gap;
        y = ColorRow(x, y);
        y += gap;

        // —— 窗口模式 ——
        y = Section(x, y, "窗口模式"); y += 6f;
        y = CycleRow(x, y, "模式", WindowNames, () => SettingsStore.S.windowMode,
            v => { SettingsStore.S.windowMode = v; ApplyWindowMode(); });
        y += gap;

        // —— 键位 ——
        y = Section(x, y, "键位（点击行后按下新键）"); y += 6f;
        y = AddKeyRow(x, y, "前进", () => SettingsStore.S.keyForward, v => SettingsStore.S.keyForward = v); y += 2f;
        y = AddKeyRow(x, y, "后退", () => SettingsStore.S.keyBack, v => SettingsStore.S.keyBack = v); y += 2f;
        y = AddKeyRow(x, y, "左移", () => SettingsStore.S.keyLeft, v => SettingsStore.S.keyLeft = v); y += 2f;
        y = AddKeyRow(x, y, "右移", () => SettingsStore.S.keyRight, v => SettingsStore.S.keyRight = v); y += 2f;
        y = AddKeyRow(x, y, "跑步", () => SettingsStore.S.keyRun, v => SettingsStore.S.keyRun = v); y += 2f;
        y = AddKeyRow(x, y, "跳跃", () => SettingsStore.S.keyJump, v => SettingsStore.S.keyJump = v); y += 2f;
        y = AddKeyRow(x, y, "交互/啃尸", () => SettingsStore.S.keyInteract, v => SettingsStore.S.keyInteract = v); y += 2f;
        y = AddKeyRow(x, y, "攻击", () => SettingsStore.S.keyAttack, v => SettingsStore.S.keyAttack = v); y += 2f;
        y = AddKeyRow(x, y, "标记(丧尸)", () => SettingsStore.S.keyMark, v => SettingsStore.S.keyMark = v); y += 2f;
        y = AddKeyRow(x, y, "血清(博士)", () => SettingsStore.S.keySerum, v => SettingsStore.S.keySerum = v); y += 2f;
        y = AddKeyRow(x, y, "紫外枪(博士)", () => SettingsStore.S.keyUv, v => SettingsStore.S.keyUv = v); y += 2f;
        y = AddKeyRow(x, y, "暂停", () => SettingsStore.S.keyPause, v => SettingsStore.S.keyPause = v); y += 10f;

        contentH = y + 20f;
        content.sizeDelta = new Vector2(0f, contentH);
        RefreshAll();
    }

    // —— 定位助手：x 左起，y 从上往下 ——
    private RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
    {
        var rt = NewRectAt(parent, name);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - h);
        rt.offsetMax = new Vector2(x + w, -y);
        return rt;
    }

    private RectTransform NewRectAt(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private float Section(float x, float y, string s)
    {
        var t = UIKit.Text("sec", content, "// " + s, 14, EndfieldTheme.UIFont, FontStyle.Bold,
            EndfieldTheme.Signal, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - 26f);
        rt.offsetMax = new Vector2(x + 600f, -y);
        return y + 30f;
    }

    private float Stepper(float x, float y, string label,
        System.Func<float> get, System.Action<float> set, float step, float min, float max, string fmt)
    {
        Label(x, y, label);
        var minus = UIKit.CutButton("minus", content, "−", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 240f, -y - 36f), new Vector2(x + 280f, -y),
            () => { set(Mathf.Max(min, get() - step)); RefreshAll(); }, 18);
        var plus = UIKit.CutButton("plus", content, "+", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 300f, -y - 36f), new Vector2(x + 340f, -y),
            () => { set(Mathf.Min(max, get() + step)); RefreshAll(); }, 18);
        var value = UIKit.Text("val", content, "", 16, EndfieldTheme.UIFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.MiddleLeft);
        var vr = value.rectTransform;
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(0f, 1f);
        vr.offsetMin = new Vector2(x + 360f, -y - 34f);
        vr.offsetMax = new Vector2(x + 460f, -y - 2f);
        refreshers.Add(() => value.text = string.Format(fmt, get()));
        return y + 44f;
    }

    private float Toggle(float x, float y, string label,
        System.Func<bool> get, System.Action<bool> set)
    {
        Label(x, y, label);
        var btn = UIKit.CutButton("toggle", content, label, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x, -y - 36f), new Vector2(x + 200f, -y),
            () => { set(!get()); RefreshAll(); }, 16);
        var state = UIKit.Text("st", content, "", 14, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink2, TextAnchor.MiddleCenter);
        var sr = state.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(0f, 1f);
        sr.offsetMin = new Vector2(x + 210f, -y - 34f);
        sr.offsetMax = new Vector2(x + 260f, -y - 2f);
        refreshers.Add(() =>
        {
            bool on = get();
            btn.GetComponent<Image>().color = on ? EndfieldTheme.Signal : EndfieldTheme.Chip;
            state.text = on ? "开" : "关";
            state.color = on ? EndfieldTheme.OnSignal : EndfieldTheme.Ink2;
        });
        return y + 44f;
    }

    private float ColorRow(float x, float y)
    {
        Label(x, y, "颜色（#RRGGBB）");
        // 手动输入 hex
        colorInput = UIKit.Input("colorInput", content, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 200f, -y - 34f), new Vector2(x + 320f, -y - 2f), SettingsStore.S.crosshairColor, 14);
        colorInput.onEndEdit.AddListener(_ => ApplyColorFromInput());
        // 实时色块
        var swatchRT = UIKit.Panel("swatch", content, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 330f, -y - 36f), new Vector2(x + 370f, -y), Color.white);
        colorSwatch = swatchRT.GetComponent<Image>();
        // 预设快捷切换
        UIKit.CutButton("colprev", content, "预设", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 380f, -y - 36f), new Vector2(x + 460f, -y), () =>
            {
                int idx = System.Array.IndexOf(ColorPresets, SettingsStore.S.crosshairColor);
                idx = (idx + 1) % ColorPresets.Length;
                SettingsStore.S.crosshairColor = ColorPresets[idx];
                RefreshAll();
            }, 14);
        return y + 44f;
    }

    private void ApplyColorFromInput()
    {
        string txt = colorInput != null ? colorInput.text.Trim() : "";
        if (string.IsNullOrEmpty(txt) || !txt.StartsWith("#")) txt = "#" + txt;
        if (ColorUtility.TryParseHtmlString(txt, out _))
        {
            SettingsStore.S.crosshairColor = txt;
            RefreshAll();
        }
    }

    private float CycleRow(float x, float y, string label, string[] names,
        System.Func<int> get, System.Action<int> set)
    {
        Label(x, y, label);
        var t = UIKit.Text("val", content, "", 16, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.MiddleLeft);
        var tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(0f, 1f);
        tr.offsetMin = new Vector2(x + 360f, -y - 34f);
        tr.offsetMax = new Vector2(x + 460f, -y - 2f);
        windowValue = t;
        refreshers.Add(() => t.text = names[get()]);
        UIKit.CutButton("cyc", content, "切换", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 480f, -y - 36f), new Vector2(x + 560f, -y),
            () => { set((get() + 1) % names.Length); RefreshAll(); }, 14);
        return y + 44f;
    }

    private float AddKeyRow(float x, float y, string label,
        System.Func<int> get, System.Action<int> set)
    {
        Label(x, y, label);
        var row = new KeyBindRow { actionName = label, get = get, set = set };
        var btn = UIKit.CutButton("key_" + label, content, "", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x + 360f, -y - 32f), new Vector2(x + 560f, -y),
            () =>
            {
                rebindRow = rebindRow == row ? null : row;
                RefreshKeyRows();
            }, 14);
        row.keyText = btn.GetComponentInChildren<Text>();
        keyRows.Add(row);
        return y + 40f;
    }

    private void Label(float x, float y, string s)
    {
        var t = UIKit.Text("lbl", content, s, 15, EndfieldTheme.CJKFont, FontStyle.Normal,
            EndfieldTheme.Ink, TextAnchor.MiddleLeft);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - 34f);
        rt.offsetMax = new Vector2(x + 240f, -y - 2f);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Esc：重绑时取消重绑，否则关闭设置（返回上一级）
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (rebindRow != null) { rebindRow = null; RefreshKeyRows(); return; }
            Close();
            return;
        }

        if (rebindRow != null)
        {
            foreach (var key in Keyboard.current.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                var k = key.keyCode;
                if (k == Key.Escape) { rebindRow = null; RefreshKeyRows(); return; }
                rebindRow.set((int)k);
                rebindRow = null;
                RefreshKeyRows();
                RefreshAll();
                return;
            }
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) { rebindRow.set(GameSettings.MouseLeft); rebindRow = null; RefreshKeyRows(); RefreshAll(); return; }
                if (Mouse.current.rightButton.wasPressedThisFrame) { rebindRow.set(GameSettings.MouseRight); rebindRow = null; RefreshKeyRows(); RefreshAll(); return; }
            }
        }
    }

    private void RefreshKeyRows()
    {
        foreach (var r in keyRows)
        {
            if (r.keyText == null) continue;
            r.keyText.text = r == rebindRow ? "按下新键…" : GameSettings.KeyDisplayName(r.get());
        }
    }

    private void RefreshAll()
    {
        SettingsStore.Save();
        if (Crosshair.Instance != null) Crosshair.Instance.Build();
        foreach (var f in refreshers) f?.Invoke();
        RefreshKeyRows();
        if (colorInput != null && colorInput.isFocused == false)
            colorInput.text = SettingsStore.S.crosshairColor;
        if (colorSwatch != null)
        {
            if (ColorUtility.TryParseHtmlString(SettingsStore.S.crosshairColor, out var c)) colorSwatch.color = c;
        }
    }

    private void ApplyWindowMode()
    {
        switch (SettingsStore.S.windowMode)
        {
            case 0: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            case 1: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
            case 2: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
        }
        SettingsStore.Save();
    }
}
