using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家丧尸客户端 UI（uGUI）：迷雾暗角（限视野）、标记冷却、枪声暴露方向箭头、自己尸体的指示。
/// 纯客户端；由 PlayerZombie 激活/隐藏。
/// </summary>
public class PlayerZombieUI : MonoBehaviour
{
    public static PlayerZombieUI Instance;

    private GameObject root;
    private GameObject fog;
    private Text markText;
    private Image markBar;
    private RectTransform gunshotArrow;
    private RectTransform corpseArrow;
    private float gunshotTimer;
    private Vector3 gunshotPos;

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("PlayerZombieUI");
            Instance = go.AddComponent<PlayerZombieUI>();
            Instance.Build();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Build()
    {
        var canvas = UIKit.CreateCanvas("ZombieCanvas", 60);
        root = canvas.gameObject;
        // 不 DontDestroyOnLoad：玩家退出时随会话清理，避免迷雾残留

        // 迷雾暗角：径向暗边（透明中心→深色边缘）
        var fogRT = UIKit.Panel("fog", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.black);
        var fogImg = fogRT.GetComponent<Image>();
        fogImg.sprite = Sprite.Create(MakeVignette(256), new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
        fogImg.color = Color.white;
        fog = fogRT.gameObject;

        // 标记状态（底部中央）
        var mark = UIKit.Panel("mark", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-160f, 40f), new Vector2(160f, 84f), Color.clear);
        markText = UIKit.Text("txt", mark, "标记 READY", 15, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        markText.rectTransform.anchorMin = Vector2.zero;
        markText.rectTransform.anchorMax = Vector2.one;
        markText.rectTransform.offsetMin = Vector2.zero;
        markText.rectTransform.offsetMax = Vector2.zero;
        var barBG = UIKit.Panel("barBG", mark, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 12f), EndfieldTheme.Chip);
        var barImg = UIKit.Panel("bar", mark, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 12f), EndfieldTheme.Signal);
        barImg.GetComponent<Image>().raycastTarget = false;
        markBar = barImg.GetComponent<Image>();
        markBar.type = Image.Type.Filled;
        markBar.fillMethod = Image.FillMethod.Horizontal;
        markBar.fillOrigin = 0;

        // 枪声方向箭头（右上）
        gunshotArrow = MakeArrow("gunshotArrow", root.transform, new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(-34f, -34f));
        gunshotArrow.gameObject.SetActive(false);

        // 自己尸体方向箭头（右上，不同位置）
        corpseArrow = MakeArrow("corpseArrow", root.transform, new Vector2(1f, 1f), new Vector2(-70f, -120f), new Vector2(-34f, -84f));
        corpseArrow.gameObject.SetActive(false);
    }

    private static RectTransform MakeArrow(string name, Transform parent, Vector2 anchorMin, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = UIKit.Panel(name, parent, anchorMin, anchorMin, offsetMin, offsetMax, EndfieldTheme.Signal);
        var img = rt.GetComponent<Image>();
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;
        return rt;
    }

    private static Texture2D MakeVignette(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                a = a * a * 0.92f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    public void Show()
    {
        if (fog != null) fog.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (fog != null) fog.SetActive(false);
        if (gunshotArrow != null) gunshotArrow.gameObject.SetActive(false);
        if (corpseArrow != null) corpseArrow.gameObject.SetActive(false);
    }

    public void Refresh()
    {
        RefreshMark(0f, 0f);
    }

    public void RefreshMark(float timer, float total)
    {
        if (markText == null) return;
        if (timer > 0f)
        {
            markText.text = "标记 " + timer.ToString("0.0") + "s";
            markText.color = EndfieldTheme.Ink2;
            if (markBar != null) { markBar.fillAmount = 1f - Mathf.Clamp01(timer / total); }
        }
        else
        {
            markText.text = "标记 READY";
            markText.color = EndfieldTheme.Signal;
            if (markBar != null) markBar.fillAmount = 1f;
        }
    }

    public void ShowGunshotIndicator(Vector3 pos, float loudness)
    {
        if (gunshotArrow == null) return;
        gunshotPos = pos;
        gunshotTimer = Mathf.Clamp01(loudness / 60f) * 4f + 2f;
        gunshotArrow.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!InstanceActive()) return;

        // 枪声箭头
        if (gunshotTimer > 0f)
        {
            gunshotTimer -= Time.unscaledDeltaTime;
            if (gunshotTimer <= 0f) gunshotArrow.gameObject.SetActive(false);
        }
        if (gunshotArrow.gameObject.activeSelf && Camera.main != null)
        {
            Vector3 to = gunshotPos - Camera.main.transform.position;
            to.y = 0f;
            gunshotArrow.rotation = Quaternion.Euler(0f, 0f, AngleFromUp(to));
        }

        // 自己尸体箭头
        var corpse = FindOwnCorpse();
        if (corpse != null)
        {
            corpseArrow.gameObject.SetActive(true);
            Vector3 to = corpse.transform.position - Camera.main.transform.position;
            to.y = 0f;
            corpseArrow.rotation = Quaternion.Euler(0f, 0f, AngleFromUp(to));
        }
        else if (corpseArrow != null)
            corpseArrow.gameObject.SetActive(false);
    }

    private bool InstanceActive()
    {
        if (Camera.main == null) return false;
        var rc = Camera.main.GetComponentInParent<RoleComponent>();
        return rc != null && rc.IsZombie && !rc.IsCorpse;
    }

    private RoleComponent FindOwnCorpse()
    {
        if (Camera.main == null) return null;
        var me = Camera.main.GetComponentInParent<RoleComponent>();
        if (me == null || me.Owner == null) return null;
        foreach (var rc in FindObjectsByType<RoleComponent>())
        {
            if (!rc.IsCorpse || !rc.CorpseWasZombie) continue;
            if (rc.CorpseOwnerClientId == me.Owner.ClientId) return rc;
        }
        return null;
    }

    private static float AngleFromUp(Vector3 to)
    {
        if (to.sqrMagnitude < 0.001f) return 0f;
        float angle = Vector3.SignedAngle(Vector3.up, to.normalized, Vector3.forward);
        return angle;
    }
}
