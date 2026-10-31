using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

/// <summary>
/// 主菜单/大厅背景图层：开始游戏前不渲染 3D 场景，用高分辨率图片代替。
/// - LoadTexture: 从 Resources/Background 加载背景图（可用自己的 4kTest.png 替换该文件）。
/// - AddTo: 把背景图作为指定 Canvas 的最底层全屏 RawImage。
/// - DisableMenuCamera: 禁用菜单 3D 相机（玩家相机不动），开局前看不到 3D 场景。
/// </summary>
public static class MenuBackground
{
    private static Texture2D bg;

    public static Texture2D LoadTexture()
    {
        if (bg == null)
            bg = Resources.Load<Texture2D>("Background");
        return bg;
    }

    /// <summary>把背景图作为指定 Canvas 的最底层（全屏 RawImage，拉伸铺满）。</summary>
    public static void AddTo(Canvas canvas)
    {
        var go = new GameObject("MenuBackground");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<RawImage>();
        var tex = LoadTexture();
        if (tex != null) img.texture = tex;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    /// <summary>禁用菜单 3D 相机：开局前不渲染 3D 场景，只用背景图。</summary>
    public static void DisableMenuCamera()
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c.GetComponentInParent<NetworkObject>() != null) continue; // 玩家相机保留
            if (c.CompareTag("MainCamera"))
            {
                c.enabled = false;
                var al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }
    }
}
