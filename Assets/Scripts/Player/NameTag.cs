using UnityEngine;

/// <summary>
/// 头顶用户名（世界空间标签）：显示本对象 RoleComponent.DisplayName，始终看向本地相机。
/// 尸体/观战时隐藏。服务器权威下发用户名，所有客户端可见。
/// </summary>
public class NameTag : MonoBehaviour
{
    private TextMesh tm;
    private Transform cam;
    private string lastText = "";

    private void Awake()
    {
        var go = new GameObject("NameTag");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        tm = go.AddComponent<TextMesh>();
        tm.font = EndfieldTheme.CJKFont != null ? EndfieldTheme.CJKFont : EndfieldTheme.UIFont;
        tm.fontSize = 24;
        tm.characterSize = 0.03f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = "";
    }

    private void Update()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null) return;

        bool show = !rc.IsCorpse;
        if (show && rc.IsZombie) show = true;

        if (!show)
        {
            if (tm.gameObject.activeSelf) tm.gameObject.SetActive(false);
            return;
        }
        if (!tm.gameObject.activeSelf) tm.gameObject.SetActive(true);

        string name = rc.DisplayName;
        if (name != lastText)
        {
            lastText = name;
            tm.text = name;
            // 描边：简单加粗感（配合字体）
        }

        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            Vector3 dir = transform.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                tm.transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
