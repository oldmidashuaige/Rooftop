using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

/// <summary>
/// 头顶名字标签（世界空间 billboard）：一个渲染文本 GameObject 贴在玩家头上，
/// 每帧只绕 Y 轴旋转对齐本地玩家相机，始终面向用户（文字保持竖直）。
/// 位置 = 头部世界坐标，不依赖屏幕投影 → 彻底消除旧 Overlay 投影的相机缓存/位置漂移问题。
/// 隐藏自身（owner）与尸体。
/// </summary>
public class NameTag : MonoBehaviour
{
    private const float AnchorHeight = 2.2f; // 头部锚点：脚下往上
    private const float WorldScale = 0.006f; // canvas 世界缩放：设计像素 × 0.006 = 世界米（约 1.26m×0.29m）
    private const float TagWidth = 210f;     // 设计像素
    private const float TagHeight = 48f;

    private Transform tagRoot;   // world-space canvas 根（含位置/旋转）
    private Text nameText;
    private Text roleText;
    private string lastName = "";
    private string lastRole = "";

    private Camera ViewCam()
    {
        var fpc = FirstPersonController.Local;
        if (fpc != null && fpc.cameraTransform != null)
        {
            var c = fpc.cameraTransform.GetComponent<Camera>();
            if (c != null) return c;
        }
        if (Camera.main != null) return Camera.main;
        return null;
    }

    private void Awake()
    {
        // —— 世界空间 canvas：整个标签作为一个"渲染文本 GameObject" ——
        var go = new GameObject("NameTagBillboard", typeof(Canvas));
        tagRoot = go.transform;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;
        // world 层：常显示在玩家身上（不放进默认 layer 之外，避免被剔除）
        tagRoot.localScale = new Vector3(WorldScale, WorldScale, WorldScale);

        var canvasRt = (RectTransform)tagRoot;
        canvasRt.sizeDelta = new Vector2(TagWidth, TagHeight);

        // 半透明深色底板（chip），保证文字在任何背景上可读
        var bg = new GameObject("TagBG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(tagRoot, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(TagWidth - 8f, TagHeight - 2f);
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.10f, 0.12f, 0.14f, 0.82f);
        bgImg.raycastTarget = false;

        // 名字（上）：粗体白。偏移计算（TagHeight=48，底板 y∈[-23,23]）：
        // 两行块(20名+6间距+14角色=40)以锚点对称居中→块顶+20块底-20；
        // 名字字形中心 +10 → 相对顶部锚点(24)偏移 -14；角色字形中心 -13 → 相对底部锚点(-24)偏移 +11。
        nameText = MakeText("TagName", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(TagWidth - 10f, 24f), 20, FontStyle.Bold, Color.white);
        // 角色行（下）：强调色
        roleText = MakeText("TagRole", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 11f), new Vector2(TagWidth - 10f, 18f), 14, FontStyle.Normal, EndfieldTheme.Signal);

        tagRoot.gameObject.SetActive(false);
    }

    private Text MakeText(string goName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, int fontSize, FontStyle style, Color color)
    {
        var tgo = new GameObject(goName, typeof(RectTransform));
        tgo.transform.SetParent(tagRoot, false);
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var t = tgo.AddComponent<Text>();
        t.font = EndfieldTheme.CJKFont != null ? EndfieldTheme.CJKFont : EndfieldTheme.UIFont;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void LateUpdate()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null) { if (tagRoot.gameObject.activeSelf) tagRoot.gameObject.SetActive(false); return; }

        bool show = !rc.IsCorpse;
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsOwner) show = false; // 不显示自己的标签

        if (!show)
        {
            if (tagRoot.gameObject.activeSelf) tagRoot.gameObject.SetActive(false);
            return;
        }

        // 位置：脚下往上 AnchorHeight（纯世界坐标，与相机无关 → 不会漂）
        tagRoot.position = transform.position + Vector3.up * AnchorHeight;

        // 公告牌：只绕 Y 轴旋转，让标签正面 +Z 指向本地玩家相机（文字竖直、始终面向用户）
        var cam = ViewCam();
        if (cam != null)
        {
            Vector3 dir = cam.transform.position - tagRoot.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                tagRoot.rotation = Quaternion.LookRotation(dir.normalized);
        }

        if (!tagRoot.gameObject.activeSelf) tagRoot.gameObject.SetActive(true);

        string name = rc.DisplayName;
        if (name != lastName) { lastName = name; nameText.text = name; }
        string role = "[" + (rc.IsZombie ? "丧尸" : RoleDef.DisplayName(rc.Role)) + "]";
        if (role != lastRole) { lastRole = role; roleText.text = role; }
    }
}
