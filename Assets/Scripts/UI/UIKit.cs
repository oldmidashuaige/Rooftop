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

    /// <param name="scaleWithScreen">true=CanvasScaler 按屏幕缩放（参考分辨率 1920x1080）；false=恒定像素（HUD 用）。</param>
    public static Canvas CreateCanvas(string name, int sortingOrder = 0, bool scaleWithScreen = false)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        if (scaleWithScreen)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }
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
    public static RectTransform CutButton(string name, Transform parent, string label,        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
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
    public static InputField Input(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
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

    /// <summary>
    /// 可滚动视图：viewport(带 RectMask2D + ScrollRect) + content + 右侧纵向滚动条。
    /// 返回 (viewport, content, scrollbar)。向 content 添加子元素（锚点 (0,1) 从顶向下），
    /// 最后设置 content.sizeDelta.y = 内容总高。
    /// </summary>
    public static (RectTransform viewport, RectTransform content, Scrollbar scrollbar) ScrollView(
        string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        float scrollbarWidth = 14f)
    {
        var viewport = NewRect(name, parent);
        viewport.anchorMin = anchorMin;
        viewport.anchorMax = anchorMax;
        viewport.offsetMin = offsetMin;
        viewport.offsetMax = offsetMax;
        var vImg = viewport.gameObject.AddComponent<Image>();
        vImg.color = Color.clear;
        vImg.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var content = NewRect("content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 100f);
        scroll.content = content;

        // 右侧纵向滚动条
        var sbRT = NewRect("scrollbar", parent);
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.offsetMin = new Vector2(-scrollbarWidth, 0f);
        sbRT.offsetMax = new Vector2(0f, 0f);
        var sbBg = sbRT.gameObject.AddComponent<Image>();
        sbBg.color = new Color(EndfieldTheme.Rule.r, EndfieldTheme.Rule.g, EndfieldTheme.Rule.b, 0.6f);
        var scrollbar = sbRT.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var sliding = NewRect("sliding", sbRT);
        sliding.anchorMin = Vector2.zero;
        sliding.anchorMax = Vector2.one;
        sliding.offsetMin = Vector2.zero;
        sliding.offsetMax = Vector2.zero;

        var handle = NewRect("handle", sliding);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(1f, 1f);
        handle.offsetMin = new Vector2(2f, 2f);
        handle.offsetMax = new Vector2(-2f, -2f);
        var hImg = handle.gameObject.AddComponent<Image>();
        hImg.color = EndfieldTheme.Signal;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = hImg;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return (viewport, content, scrollbar);
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
