using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 程序化 uGUI 构建器：按终末地固定规则生成界面。
/// - 切角 = 可点击（切角精灵，Sliced 保持切角恒定）；直角 = 展示/容器
/// - 唯一强调色只做填充（按钮 hover/按下 擦入强调色）
/// - 细线分区（1px Rule）；宽体数字；中文粗体标题；// 眉标
/// </summary>
public static class UIKit
{
    // —— 切角精灵（右下角切掉一个三角）——
    private static Sprite cutCornerSprite;

    public static Sprite CutCornerSprite
    {
        get
        {
            if (cutCornerSprite == null) cutCornerSprite = MakeCutCornerSprite(32);
            return cutCornerSprite;
        }
    }

    private static Sprite MakeCutCornerSprite(int px)
    {
        var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = Color.white;
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                // 右下角切掉一个三角：x + y > px（左上为原点）
                bool cut = (px - x) + (px - y) > px * 1f; // 斜线：x+y <= px 区域保留
                tex.SetPixel(x, y, cut ? fill : clear);
            }
        // 真正切角：保留 x+y <= px 部分（左下到右上对角线下）
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
                tex.SetPixel(x, y, (x + y) <= px ? fill : clear);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        // 边框：让 Sliced 时切角区域保持固定大小
        Vector4 border = new Vector4(0f, 0f, px, px); // left, bottom, right, top
        return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, border);
    }

    // —— 基础对象 ——
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        // Input System 模式：必须用 InputSystemUIInputModule（StandaloneInputModule 会读 UnityEngine.Input 而抛异常）
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        go.AddComponent<CanvasScaler>();
        // 关键：没有 GraphicRaycaster，EventSystem 无法对 Canvas 做命中检测，按钮永远点不了
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>纯色直角面板（容器，不可点）。</summary>
    public static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>细线（1px），用于分区。</summary>
    public static RectTransform HLine(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        return Panel(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, color);
    }

    /// <summary>文本（uGUI legacy Text，动态字体）。</summary>
    public static Text Text(string name, Transform parent, string content, int fontSize, Font font,
        FontStyle style, Color color, TextAnchor align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>眉标 + 标题成对（// LABEL / 标题）。返回标题 Text。</summary>
    public static Text EyebrowTitle(string name, Transform parent, string eyebrow, string title,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = Panel(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, Color.clear);
        float h = Mathf.Max(1f, offsetMax.y - offsetMin.y);
        float eyebrowH = 18f;
        Text(name + "_eyebrow", rt, "// " + eyebrow, 13, EndfieldTheme.UIFont, FontStyle.Bold,
            EndfieldTheme.Ink2, TextAnchor.LowerLeft).rectTransform.anchorMin = new Vector2(0f, 0.6f);
        Text(name + "_eyebrow", rt, "", 0, EndfieldTheme.UIFont, FontStyle.Normal, Color.clear, TextAnchor.LowerLeft);
        // 眉标置于上半，标题置于下半
        return Text(name + "_title", rt, title, Mathf.RoundToInt(h * 0.42f), EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.UpperLeft);
    }

    /// <summary>可点击切角按钮（chip 底，hover/按下 擦入强调色，文字切墨色）。</summary>
    public static RectTransform CutButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        UnityAction onClick, int fontSize = 18)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = CutCornerSprite;
        img.type = Image.Type.Sliced;
        img.color = EndfieldTheme.Chip;

        var lbl = Text("label", rt, label, fontSize, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Ink, TextAnchor.MiddleCenter);
        lbl.rectTransform.anchorMin = Vector2.zero;
        lbl.rectTransform.anchorMax = Vector2.one;
        lbl.rectTransform.offsetMin = new Vector2(10f, 0f);
        lbl.rectTransform.offsetMax = new Vector2(-6f, 0f);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var fx = rt.gameObject.AddComponent<CutButtonFX>();
        fx.image = img;
        fx.label = lbl;
        return rt;
    }

    /// <summary>数值/眉标用宽体等宽感数字（Bahnschrift 宽体）。</summary>
    public static Text Num(string name, Transform parent, string content, int fontSize, Color color,
        TextAnchor align = TextAnchor.MiddleLeft)
    {
        return Text(name, parent, content, fontSize, EndfieldTheme.UIFont, FontStyle.Bold, color, align);
    }

    /// <summary>输入框：chip 底 + InputField + 子 Text（宽体）。</summary>
    public static InputField Input(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        string initial, int fontSize = 16)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = EndfieldTheme.Chip;
        img.raycastTarget = true;

        var field = rt.gameObject.AddComponent<InputField>();
        field.targetGraphic = img;
        field.text = initial;

        var textRT = NewRect("text", rt);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 0f);
        textRT.offsetMax = new Vector2(-8f, 0f);
        var text = textRT.gameObject.AddComponent<Text>();
        text.font = EndfieldTheme.UIFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = EndfieldTheme.Ink;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        field.textComponent = text;

        return field;
    }
}

/// <summary>切角按钮交互：hover/按下 擦入强调色，文字转墨色；机械快速过渡。</summary>
public class CutButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image image;
    public Text label;
    private bool hovered;
    private bool pressed;

    private void Update()
    {
        if (image == null) return;
        Color target = (hovered || pressed) ? EndfieldTheme.Signal : EndfieldTheme.Chip;
        image.color = Color.Lerp(image.color, target, Time.unscaledDeltaTime * 24f);
        if (label != null)
            label.color = Color.Lerp(label.color, (hovered || pressed) ? EndfieldTheme.OnSignal : EndfieldTheme.Ink,
                Time.unscaledDeltaTime * 24f);
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
}
