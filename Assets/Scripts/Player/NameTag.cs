using UnityEngine;

/// <summary>
/// 头顶标签（世界空间）：用户名在上，角色在下方 [角色]。
/// 始终看向本地相机；尸体/观战时隐藏。服务器权威下发用户名，所有客户端可见。
/// </summary>
public class NameTag : MonoBehaviour
{
    private TextMesh nameTm;
    private TextMesh roleTm;
    private Transform nameTr;
    private Transform roleTr;
    private Transform cam;
    private string lastName = "";
    private string lastRole = "";

    private void Awake()
    {
        nameTr = MakeLine("NameTag", new Vector3(0f, 2.55f, 0f), 24, Color.white, out nameTm);
        roleTr = MakeLine("RoleTag", new Vector3(0f, 2.30f, 0f), 20, EndfieldTheme.Signal, out roleTm);
    }

    private Transform MakeLine(string goName, Vector3 localPos, int fontSize, Color color, out TextMesh tm)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        tm = go.AddComponent<TextMesh>();
        tm.font = EndfieldTheme.CJKFont != null ? EndfieldTheme.CJKFont : EndfieldTheme.UIFont;
        tm.fontSize = fontSize;
        tm.characterSize = 0.03f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = "";
        return go.transform;
    }

    private void Update()
    {
        var rc = GetComponent<RoleComponent>();
        if (rc == null) return;

        bool show = !rc.IsCorpse;

        if (!show)
        {
            if (nameTr.gameObject.activeSelf) nameTr.gameObject.SetActive(false);
            if (roleTr.gameObject.activeSelf) roleTr.gameObject.SetActive(false);
            return;
        }
        if (!nameTr.gameObject.activeSelf) nameTr.gameObject.SetActive(true);
        if (!roleTr.gameObject.activeSelf) roleTr.gameObject.SetActive(true);

        string name = rc.DisplayName;
        if (name != lastName)
        {
            lastName = name;
            nameTm.text = name;
        }

        string role = "[" + (rc.IsZombie ? "丧尸" : RoleDef.DisplayName(rc.Role)) + "]";
        if (role != lastRole)
        {
            lastRole = role;
            roleTm.text = role;
        }

        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            Vector3 dir = transform.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                var rot = Quaternion.LookRotation(dir);
                nameTr.rotation = rot;
                roleTr.rotation = rot;
            }
        }
    }
}
