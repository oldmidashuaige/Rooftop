using UnityEngine;

/// <summary>
/// 终末地风味主题 token（本项目变体：深色 / 强调色 #66CCFF / paper=cool / cut=standard / ground=plain / motion=calm）。
/// 固定规则：唯一强调色只做填充；红色只给删除；切角=可点击；直角=展示；细线分区；无圆角/投影/渐变。
/// 全部 UI 从这里取色，不要在部件上各自写色。
/// </summary>
public static class EndfieldTheme
{
    // —— 颜色（深色主题）——
    public static readonly Color Paper   = Hex("171A1C"); // cool paper
    public static readonly Color Panel   = Hex("1F2326"); // cool panel
    public static readonly Color Ink     = Hex("EDEDED");
    public static readonly Color Ink2    = Hex("A3A3A3");
    public static readonly Color Rule    = Hex("323A3F"); // cool rule
    public static readonly Color Chip    = Hex("2A3034"); // cool chip
    public static readonly Color Signal  = Hex("66CCFF"); // 强调色（唯一）
    public static readonly Color OnSignal= Hex("161616");
    public static readonly Color Danger  = Hex("FF8A7A"); // 只给删除
    public static readonly Color Scrim   = new Color(0f, 0f, 0f, 0.63f);

    public static Color Trace => Signal; // 深色主题下"当前位置"的线用强调色

    // —— 字体 ——
    private static Font uiFont;        // 宽体几何拉丁（编号/眉标/按钮英文）
    private static Font cjkFont;       // 中文黑体（粗体标题 / 正文）

    public static Font UIFont
    {
        get
        {
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Narrow", "Arial" }, 16);
                if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            return uiFont;
        }
    }

    public static Font CJKFont
    {
        get
        {
            if (cjkFont == null)
            {
                cjkFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei" }, 16);
                if (cjkFont == null) cjkFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            return cjkFont;
        }
    }

    /// <summary>眉标样式：宽体大写英文小字（UI 字号）。</summary>
    public static GUIStyle EyebrowStyle(int size = 13)
    {
        var s = new GUIStyle
        {
            font = UIFont,
            fontSize = size,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Ink2 },
            alignment = TextAnchor.MiddleLeft
        };
        s.richText = false;
        return s;
    }

    /// <summary>标题样式：中文粗体。</summary>
    public static GUIStyle TitleStyle(int size = 30)
    {
        return new GUIStyle
        {
            font = CJKFont,
            fontSize = size,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Ink },
            alignment = TextAnchor.MiddleLeft
        };
    }

    public static GUIStyle BodyStyle(int size = 15)
    {
        return new GUIStyle
        {
            font = CJKFont,
            fontSize = size,
            fontStyle = FontStyle.Normal,
            normal = { textColor = Ink },
            alignment = TextAnchor.MiddleLeft
        };
    }

    public static Color ParseHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) return c;
        return Color.magenta;
    }

    private static Color Hex(string hex) => ParseHex(hex);
}
