using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

/// <summary>
/// 头顶标签（屏幕空间 Overlay）：玩家名在上，[角色名] 在下。
/// 每帧把玩家头部投影到屏幕坐标定位 → 始终跟随玩家；隐藏自己/尸体/身后。
/// 用 Overlay uGUI（TextMesh 与世界空间 uGUI 在 URP 构建中不渲染）。
/// </summary>
public class NameTag : MonoBehaviour
{
    private RectTransform canvasRt;
    private RectTransform boxRt;
    private Text nameText;
    private Text roleText;
    private Camera cam;
    private string lastName = "";
    private string lastRole = "";

    private Camera ViewCam()
    {
        if (Camera.main != null) return Camera.main;
        var fpc = FirstPersonController.Local;
        if (fpc != null && fpc.cameraTransform != null) return fpc.cameraTransform.GetComponent<Camera>();
        return null;
    }

    private void Awake()
    {
        var go = new GameObject("NameTagCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasRt = (RectTransform)go.transform;
        canvasRt.anchorMin = Vector2.zero;
        canvasRt.anchorMax = Vector2.one;
        canvasRt.offsetMin = Vector2.zero;
        canvasRt.offsetMax = Vector2.zero;

        // 居中容器：每帧移动到玩家头部投影位置（底边锚定头部上方，向上偏移）
        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(canvasRt, false);
        boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0f);
        boxRt.sizeDelta = new Vector2(320f, 52f);

        nameText = MakeText("NameTag", new Vector2(0f, 1f), new Vector2(320f, 28f), 14, Color.white, TextAnchor.MiddleCenter);
        roleText = MakeText("RoleTag", new Vector2(0f, 0f), new Vector2(320f, 22f), 11, EndfieldTheme.Signal, TextAnchor.MiddleCenter);

        go.SetActive(false);
    }

    private Text MakeText(string goName, Vector2 anchor, Vector2 size, int fontSize, Color color, TextAnchor align)
    {
        var tgo = new GameObject(goName, typeof(RectTransform));
        tgo.transform.SetParent(boxRt, false);
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var t = tgo.AddComponent<Text>();
        t.font = EndfieldTheme.CJKFont != null ? EndfieldTheme.CJKFont : EndfieldTheme.UIFont;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void Update()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null) return;

        bool show = !rc.IsCorpse;
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsOwner) show = false; // 不显示自己的标签

        if (show && cam == null) cam = ViewCam();

        if (show && cam != null)
        {
            Vector3 head = transform.position + Vector3.up * 2.2f;
            Vector3 sp = cam.WorldToScreenPoint(head);
            // 限制在屏幕内（避免贴边/越界时标签跑到奇怪位置）
            if (sp.z > 0f)
            {
                sp.x = Mathf.Clamp(sp.x, 60f, Screen.width - 60f);
                sp.y = Mathf.Clamp(sp.y, 30f, Screen.height - 30f);
                if (!canvasRt.gameObject.activeSelf) canvasRt.gameObject.SetActive(true);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 local))
                    boxRt.anchoredPosition = local;

                string name = rc.DisplayName;
                if (name != lastName) { lastName = name; nameText.text = name; }
                string role = "[" + (rc.IsZombie ? "丧尸" : RoleDef.DisplayName(rc.Role)) + "]";
                if (role != lastRole) { lastRole = role; roleText.text = role; }
                return;
            }
        }
        if (canvasRt.gameObject.activeSelf) canvasRt.gameObject.SetActive(false);
    }
}
