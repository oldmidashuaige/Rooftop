using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏内 HUD（uGUI·终末地风）：准星 / 生命 / 人机丧尸数 / 命中标记 / 死亡 /
/// 角色信息 / 感染 CG 占位 / 撤离倒计时与座位 / 结算。
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance;

    private GameObject root;
    private Text healthNum;
    private Text zombieNum;
    private RectTransform hitMarker;
    private Text deathText;
    private Text roleText;
    private GameObject infRoot;
    private Text infText;
    private GameObject evacRoot;
    private Text evacText;
    private Text resultText;
    private float hitTimer;
    private float deathTimer;
    private float infStartTime;
    private float infDuration;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 销毁自己创建的 Canvas（独立对象，不随玩家自动销毁），避免退出后 HUD/感染图标残留
        if (root != null) Destroy(root);
    }

    private void BuildUI()
    {
        var canvas = UIKit.CreateCanvas("GameHUDCanvas", 50);
        root = canvas.gameObject;

        // 准星改用独立组件 Crosshair（由 FirstPersonController 创建，设置可调）
        Crosshair.Ensure();

        // 命中标记：严格居中，斜向 X（4 根对角臂，与准星同中心）
        var hm = UIKit.Panel("hitMarker", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, EndfieldTheme.Signal);
        hitMarker = hm;
        hm.gameObject.SetActive(false);
        hm.anchoredPosition = Vector2.zero;
        hm.sizeDelta = Vector2.zero;
        AddDiagonal(hm, 10f, 10f, 45f, 12f, 2f);   // 右上
        AddDiagonal(hm, -10f, -10f, 45f, 12f, 2f); // 左下
        AddDiagonal(hm, -10f, 10f, -45f, 12f, 2f); // 左上
        AddDiagonal(hm, 10f, -10f, -45f, 12f, 2f); // 右下

        // 角色信息（左上）
        var role = UIKit.Panel("role", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -40f), new Vector2(340f, -90f), Color.clear);
        UIKit.Num("roleEyebrow", role.transform, "// ROLE", 12, EndfieldTheme.Ink2).rectTransform.anchoredPosition = new Vector2(0f, 28f);
        roleText = UIKit.Text("roleText", role.transform, "", 20, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Ink, TextAnchor.MiddleLeft);
        roleText.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        // 生命（左下）
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

        // 死亡
        deathText = UIKit.Text("death", root.transform, "你死了…", 40, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        deathText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        deathText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        deathText.rectTransform.anchoredPosition = Vector2.zero;
        deathText.rectTransform.sizeDelta = new Vector2(500f, 60f);
        deathText.gameObject.SetActive(false);

        // 感染 CG 占位（全屏暗 + 文字）
        var inf = UIKit.Panel("infection", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.82f));
        infRoot = inf.gameObject;
        infText = UIKit.Text("infText", inf, "", 30, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        infText.rectTransform.anchorMin = new Vector2(0.5f, 0.6f);
        infText.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
        infText.rectTransform.anchoredPosition = Vector2.zero;
        infText.rectTransform.sizeDelta = new Vector2(700f, 60f);
        var infSub = UIKit.Text("infSub", inf, "（CG 占位 · 后续替换为剧情视频）", 15, EndfieldTheme.CJKFont, FontStyle.Normal, EndfieldTheme.Ink2, TextAnchor.MiddleCenter);
        infSub.rectTransform.anchorMin = new Vector2(0.5f, 0.55f);
        infSub.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
        infSub.rectTransform.anchoredPosition = Vector2.zero;
        infSub.rectTransform.sizeDelta = new Vector2(700f, 30f);
        infRoot.SetActive(false);

        // 撤离倒计时（顶部中央）
        var evac = UIKit.Panel("evac", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-220f, -50f), new Vector2(220f, -90f), Color.clear);
        evacRoot = evac.gameObject;
        evacText = UIKit.Text("evacText", evac, "撤离倒计时 30s  │  座位 0/3", 20, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        evacText.rectTransform.anchorMin = Vector2.zero;
        evacText.rectTransform.anchorMax = Vector2.one;
        evacText.rectTransform.offsetMin = Vector2.zero;
        evacText.rectTransform.offsetMax = Vector2.zero;
        evacRoot.SetActive(false);

        // 结算（全屏中央）
        resultText = UIKit.Text("result", root.transform, "", 40, EndfieldTheme.CJKFont, FontStyle.Bold, EndfieldTheme.Signal, TextAnchor.MiddleCenter);
        resultText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        resultText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        resultText.rectTransform.anchoredPosition = Vector2.zero;
        resultText.rectTransform.sizeDelta = new Vector2(900f, 100f);
        resultText.gameObject.SetActive(false);
    }

    private void AddSegment(RectTransform parent, float x, float y, float w, float h)
    {
        var seg = UIKit.Panel("seg", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, EndfieldTheme.Signal);
        seg.anchoredPosition = new Vector2(x, y);
        seg.sizeDelta = new Vector2(w, h);
        seg.GetComponent<Image>().raycastTarget = false;
    }

    /// <summary>斜向命中臂：以 (x,y) 为中心、绕自身 pivot 旋转 rot°。</summary>
    private void AddDiagonal(RectTransform parent, float x, float y, float rot, float w, float h)
    {
        var seg = UIKit.Panel("seg", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, EndfieldTheme.Signal);
        seg.anchoredPosition = new Vector2(x, y);
        seg.sizeDelta = new Vector2(w, h);
        seg.localRotation = Quaternion.Euler(0f, 0f, rot);
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

        // 刷新角色信息
        if (roleText != null && root != null && root.activeSelf)
        {
            var rc = GetComponentInParent<RoleComponent>();
            if (rc != null)
            {
                string team = rc.IsZombie ? "玩家丧尸" : RoleDef.DisplayName(rc.Role);
                if (rc.IsCorpse) team = "尸体";
                roleText.text = team;
            }
        }

        // 感染倒计时（客户端本地计时）
        if (infDuration > 0f)
        {
            float remain = infDuration - (Time.time - infStartTime);
            if (remain <= 0f) remain = 0f;
            if (infText != null) infText.text = "已感染  " + Mathf.CeilToInt(remain).ToString() + "s";
        }
    }

    private float nextCountTime;
    private void LateUpdate()
    {
        if (Time.time >= nextCountTime)
        {
            nextCountTime = Time.time + 0.3f;
            int alive = 0;
            foreach (var z in FindObjectsByType<SimpleZombie>())
                if (!z.IsDead) alive++;
            if (zombieNum != null) zombieNum.text = alive.ToString("00");
        }
    }

    // ============ 事件 ============

    public void OnHealthChanged(int hp, int max)
    {
        if (healthNum != null) healthNum.text = hp.ToString();
    }

    /// <summary>是否有枪（人类=显示准星；丧尸/尸体=隐藏准星）。</summary>
    public void SetHasGun(bool on)
    {
        if (Crosshair.Instance != null) Crosshair.Instance.SetVisible(on);
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

    public void OnInfectionStart(float t)
    {
        if (infRoot != null)
        {
            infRoot.SetActive(true);
            infText.text = "已感染";
            infStartTime = Time.time;
            infDuration = t;
        }
    }

    public void OnInfectionEnd()
    {
        if (infRoot != null) infRoot.SetActive(false);
        infDuration = 0f;
    }

    public void OnRoundState(GameFlow.RoundStateBroadcast msg)
    {
        if (evacRoot == null) return;
        if (msg.state == (int)GameFlow.RoundState.Evacuation)
        {
            evacRoot.SetActive(true);
            evacText.text = "撤离倒计时 " + Mathf.CeilToInt(msg.countdown).ToString() + "s  │  座位 " + msg.seatsTaken + "/" + msg.seatsTotal;
        }
        else if (msg.state == (int)GameFlow.RoundState.Playing)
        {
            evacRoot.SetActive(false);
        }
        else if (msg.state == (int)GameFlow.RoundState.Ended)
        {
            evacRoot.SetActive(false);
        }
    }

    public void OnLobbyState(GameFlow.LobbyStateBroadcast msg)
    {
        if (root == null) return;
        root.SetActive(msg.started);
    }

    public void OnResult(string text)
    {
        if (resultText != null)
        {
            resultText.text = text;
            resultText.gameObject.SetActive(true);
        }
    }
}
