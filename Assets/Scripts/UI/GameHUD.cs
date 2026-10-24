using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 HUD（uGUI·终末地风）：准星 / 生命大数字 / 人机丧尸计数 / 命中标记 / 死亡提示。
/// 单例供 PlayerHealth、Gun 等通知。
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance;

    private GameObject root;
    private Text healthNum;
    private Text zombieNum;
    private RectTransform hitMarker;
    private Text deathText;
    private float hitTimer;
    private float deathTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildUI()
    {
        var canvas = UIKit.CreateCanvas("GameHUDCanvas", 50);
        root = canvas.gameObject;

        // 准星（中央四段细线）
        float c = Screen.width * 0.5f;
        Crosshair("chN", c - 1f, Screen.height * 0.5f - 10f, 2f, 6f);
        Crosshair("chS", c - 1f, Screen.height * 0.5f + 4f, 2f, 6f);
        Crosshair("chW", c - 10f, Screen.height * 0.5f - 1f, 6f, 2f);
        Crosshair("chE", c + 4f, Screen.height * 0.5f - 1f, 6f, 2f);

        // 命中标记（短暂信号色 X）
        var hm = UIKit.Panel("hitMarker", root.transform, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero, EndfieldTheme.Signal);
        hitMarker = hm;
        hm.gameObject.SetActive(false);
        // 四段
        hm.anchoredPosition = new Vector2(c, Screen.height * 0.5f);
        AddSegment(hm, -10f, -10f, 6f, 2f);
        AddSegment(hm, 4f, -10f, 6f, 2f);
        AddSegment(hm, -10f, 8f, 6f, 2f);
        AddSegment(hm, 4f, 8f, 6f, 2f);

        // 生命（左下大数字）
        var hp = UIKit.Panel("health", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40f, 40f), new Vector2(260f, 140f), Color.clear);
        UIKit.Num("hpEyebrow", hp.transform, "// HP", 12, EndfieldTheme.Ink2).rectTransform.anchoredPosition = new Vector2(0f, 78f);
        healthNum = UIKit.Num("hpNum", hp.transform, "100", 52, EndfieldTheme.Ink);
        healthNum.rectTransform.anchoredPosition = new Vector2(0f, 30f);
        healthNum.alignment = TextAnchor.MiddleLeft;

        // 丧尸计数（右下）
        var zc = UIKit.Panel("zombies", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-260f, 40f), new Vector2(-40f, 140f), Color.clear);
        UIKit.Num("zcEyebrow", zc.transform, "// ZOMBIES", 12, EndfieldTheme.Ink2).rectTransform.anchoredPosition = new Vector2(-80f, 78f);
        zombieNum = UIKit.Num("zcNum", zc.transform, "00", 52, EndfieldTheme.Ink, TextAnchor.MiddleRight);
        zombieNum.rectTransform.anchoredPosition = new Vector2(-80f, 30f);

        // 死亡提示
        deathText = UIKit.Text("death", root.transform, "你死了…", 40, EndfieldTheme.CJKFont, FontStyle.Bold,
            EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        deathText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        deathText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        deathText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        deathText.rectTransform.sizeDelta = new Vector2(500f, 60f);
        deathText.gameObject.SetActive(false);
    }

    private void Crosshair(string name, float x, float y, float w, float h)
    {
        var rt = UIKit.Panel(name, root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, EndfieldTheme.Ink);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        rt.GetComponent<Image>().raycastTarget = false;
    }

    private void AddSegment(RectTransform parent, float x, float y, float w, float h)
    {
        var seg = UIKit.Panel("seg", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, EndfieldTheme.Signal);
        seg.anchoredPosition = new Vector2(x, y);
        seg.sizeDelta = new Vector2(w, h);
        seg.GetComponent<Image>().raycastTarget = false;
    }

    private void Update()
    {
        if (hitTimer > 0f)
        {
            hitTimer -= Time.unscaledDeltaTime;
            if (hitTimer <= 0f && hitMarker != null) hitMarker.gameObject.SetActive(false);
        }
        if (deathTimer > 0f)
        {
            deathTimer -= Time.unscaledDeltaTime;
            if (deathTimer <= 0f && deathText != null) deathText.gameObject.SetActive(false);
        }
    }

    private float nextCountTime;
    private void LateUpdate()
    {
        if (Time.time >= nextCountTime)
        {
            nextCountTime = Time.time + 0.3f;
            int alive = 0;
            foreach (var z in FindObjectsByType<SimpleZombie>(FindObjectsSortMode.None))
                if (!z.IsDead) alive++;
            if (zombieNum != null) zombieNum.text = alive.ToString("00");
        }
    }

    public void OnHealthChanged(int hp, int max)
    {
        if (healthNum != null) healthNum.text = hp.ToString();
    }

    public void OnHitMarker()
    {
        if (hitMarker != null)
        {
            hitMarker.gameObject.SetActive(true);
            hitTimer = 0.15f;
        }
    }

    public void OnDeath()
    {
        if (deathText != null)
        {
            deathText.gameObject.SetActive(true);
            deathTimer = 3f;
        }
    }
}
