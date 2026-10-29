using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 准星（客户端）：严格居中于屏幕中心，由设置驱动。
/// 组成：中心点(可选)、水平线(可选，左右两段)、竖直线(可选，上下两段)。
/// 长度 = 单侧长度（从中心向外，px）；粗细 = 线宽（px）；颜色可调。
/// GameHUD 调用 SetVisible(false) 在丧尸/尸体时隐藏。
/// </summary>
public class Crosshair : MonoBehaviour
{
    public static Crosshair Instance;

    private GameObject root;
    private readonly List<RectTransform> parts = new List<RectTransform>();
    private Image centerDot;

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("Crosshair");
            Instance = go.AddComponent<Crosshair>();
            Instance.Build();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Build()
    {
        if (root != null) Destroy(root);
        root = null;
        parts.Clear();
        centerDot = null;

        var s = SettingsStore.S;
        Color color = ParseColor(s.crosshairColor);

        var canvas = UIKit.CreateCanvas("CrosshairCanvas", 55);
        root = canvas.gameObject;

        float cx = 0f; // 锚点 (0.5,0.5) 即屏幕中心，用偏移 0 居中

        if (s.crosshairCenter)
        {
            centerDot = MakePart("dot", s.crosshairThickness, s.crosshairThickness, color);
        }
        if (s.crosshairHorizontal)
        {
            // 左右两段，长度 = crosshairLength，粗细 = thickness
            MakePart("hlineL", s.crosshairLength, s.crosshairThickness, color, new Vector2(-(s.crosshairLength / 2f) - s.crosshairThickness / 2f, 0f));
            MakePart("hlineR", s.crosshairLength, s.crosshairThickness, color, new Vector2((s.crosshairLength / 2f) + s.crosshairThickness / 2f, 0f));
        }
        if (s.crosshairVertical)
        {
            MakePart("vlineT", s.crosshairThickness, s.crosshairLength, color, new Vector2(0f, (s.crosshairLength / 2f) + s.crosshairThickness / 2f));
            MakePart("vlineB", s.crosshairThickness, s.crosshairLength, color, new Vector2(0f, -(s.crosshairLength / 2f) - s.crosshairThickness / 2f));
        }
    }

    private Image MakePart(string name, float w, float h, Color color, Vector2 localPos = default)
    {
        var rt = UIKit.Panel(name, root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, color);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = localPos;
        var img = rt.GetComponent<Image>();
        img.raycastTarget = false;
        parts.Add(rt);
        return img;
    }

    public void SetVisible(bool on)
    {
        if (root != null) root.SetActive(on);
    }

    private static Color ParseColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.white;
    }
}
