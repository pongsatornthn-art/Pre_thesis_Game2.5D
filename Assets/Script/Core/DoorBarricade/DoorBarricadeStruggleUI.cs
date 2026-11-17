using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [ข้อ 1] UI ของ Door Barricade Struggle — หลอดแนวตั้ง 0–100 + โซนล็อก 80–100 + ตัวนับ HOLD + คำใบ้ลากเมาส์ขึ้น
///
/// ไม่ต้องตั้งค่าอะไร: เริ่มยันประตูครั้งแรก สคริปต์สร้าง Canvas ให้เอง
/// อยากใช้รูปของตัวเอง (เช่น healthbar_frame / healthbar_fill_red จากชุด UI ที่แยกไว้):
///   สร้าง GameObject เปล่า → Add Component นี้ → ใส่ Sprite ในช่อง Custom Sprites → ระบบจะใช้ตัวนี้แทน
/// </summary>
public class DoorBarricadeStruggleUI : MonoBehaviour
{
    public static DoorBarricadeStruggleUI Instance { get; private set; }

    [Header("ข้อความ")]
    public string title = "DOOR BARRICADE STRUGGLE";
    public string dragHint = "DRAG MOUSE UP";
    public string tutorialHint = "Drag the mouse UP to push the door shut.\nKeep it in the top zone until it locks!";
    public string holdLabel = "HOLD";
    public string retryText = "TRY AGAIN";

    [Header("Custom Sprites (เว้นว่าง = สี่เหลี่ยมสีล้วน)")]
    public Sprite gaugeFrameSprite;
    public Sprite gaugeFillSprite;
    public Sprite mouseIconSprite;

    [Header("สี")]
    public Color fillNormal = new Color(0.85f, 0.8f, 0.7f, 1f);
    public Color fillLockZone = new Color(0.95f, 0.75f, 0.25f, 1f);
    public Color fillDanger = new Color(0.8f, 0.12f, 0.12f, 1f);
    public Color lockZoneBand = new Color(0.95f, 0.75f, 0.25f, 0.25f);
    [Range(0f, 100f)] public float dangerBelow = 25f;

    [Header("ตำแหน่ง/ขนาด")]
    public Vector2 gaugePosition = new Vector2(-260f, 0f);
    public Vector2 gaugeSize = new Vector2(34f, 340f);

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private RectTransform fillRect;
    private Image fillImage;
    private RectTransform holdBarFill;
    private CanvasGroup holdGroup;
    private RectTransform hintArrow;
    private TextMeshProUGUI tutorialText;
    private TextMeshProUGUI retryLabel;
    private Image flash;
    private RectTransform gaugeRoot;

    private float flashAlpha;
    private float retryTimer;
    private float shake;
    private bool built;

    /// <summary>มี UI ในซีนหรือยัง ถ้าไม่มีสร้างให้</summary>
    public static void Ensure()
    {
        if (Instance != null) return;
        Instance = FindFirstObjectByType<DoorBarricadeStruggleUI>();
        if (Instance == null) new GameObject("DoorBarricadeStruggleUI (auto)").AddComponent<DoorBarricadeStruggleUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        SetVisible(false);
    }

    private void OnEnable()
    {
        DoorBarricadeStruggle.OnAnyStarted += HandleStarted;
        DoorBarricadeStruggle.OnAnySlam += HandleSlam;
        DoorBarricadeStruggle.OnAnyTutorialRetry += HandleRetry;
        DoorBarricadeStruggle.OnAnyEnded += HandleEnded;
    }

    private void OnDisable()
    {
        DoorBarricadeStruggle.OnAnyStarted -= HandleStarted;
        DoorBarricadeStruggle.OnAnySlam -= HandleSlam;
        DoorBarricadeStruggle.OnAnyTutorialRetry -= HandleRetry;
        DoorBarricadeStruggle.OnAnyEnded -= HandleEnded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────── events ─────────
    private void HandleStarted(DoorBarricadeStruggle s)
    {
        SetVisible(true);
        if (tutorialText != null) tutorialText.gameObject.SetActive(s.IsTutorial);
        retryTimer = 0f;
    }

    private void HandleSlam(DoorBarricadeStruggle s)
    {
        flashAlpha = 0.35f;
        shake = 10f;
    }

    private void HandleRetry(DoorBarricadeStruggle s)
    {
        retryTimer = 1.4f;
        flashAlpha = 0.5f;
    }

    private void HandleEnded(DoorBarricadeStruggle s, DoorBarricadeStruggle.Result r)
    {
        SetVisible(false);
    }

    // ───────── update ─────────
    private void Update()
    {
        DoorBarricadeStruggle s = DoorBarricadeStruggle.Active;
        if (s == null || !built) return;

        float dt = Time.unscaledDeltaTime;
        float v = s.DoorPos / 100f;

        // หลอด
        fillRect.anchorMax = new Vector2(1f, v);
        Color c = s.DoorPos >= s.lockZoneMin ? fillLockZone : (s.DoorPos < dangerBelow ? fillDanger : fillNormal);
        fillImage.color = Color.Lerp(fillImage.color, c, dt * 12f);

        // นับ HOLD
        bool inZone = s.InLockZone;
        holdGroup.alpha = Mathf.MoveTowards(holdGroup.alpha, inZone ? 1f : 0.25f, dt * 6f);
        holdBarFill.anchorMax = new Vector2(Mathf.Clamp01(s.WinProgress01), 1f);

        // ลูกศรเด้งขึ้นลง
        if (hintArrow != null) hintArrow.anchoredPosition = new Vector2(0f, 30f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f);

        // กระแทก: จอแดงวาบ + หลอดสั่น
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.6f);
        flash.color = new Color(0.6f, 0f, 0f, flashAlpha);
        shake = Mathf.MoveTowards(shake, 0f, dt * 60f);
        gaugeRoot.anchoredPosition = gaugePosition + Random.insideUnitCircle * shake;

        // TRY AGAIN
        if (retryLabel != null)
        {
            retryTimer = Mathf.Max(0f, retryTimer - dt);
            retryLabel.gameObject.SetActive(retryTimer > 0f);
        }
    }

    private void SetVisible(bool on)
    {
        if (rootGroup != null) rootGroup.alpha = on ? 1f : 0f;
        if (canvas != null) canvas.enabled = on;
    }

    // ───────── สร้าง UI ─────────
    private void Build()
    {
        if (built) return;

        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        RectTransform root = (RectTransform)transform;

        // จอแดงวาบตอนโดนกระแทก
        flash = NewImage("Flash", root, null, new Color(0.6f, 0f, 0f, 0f));
        Stretch(flash.rectTransform);

        // ชื่อ
        TextMeshProUGUI titleText = NewText("Title", root, title, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 60f));
        titleText.characterSpacing = 6f;

        // หลอด
        gaugeRoot = NewRect("Gauge", root);
        Place(gaugeRoot, new Vector2(0.5f, 0.5f), gaugePosition, gaugeSize);

        Image frame = NewImage("Frame", gaugeRoot, gaugeFrameSprite, new Color(0.05f, 0.05f, 0.05f, 0.85f));
        Stretch(frame.rectTransform, -6f);
        if (gaugeFrameSprite != null) frame.color = Color.white;

        RectTransform track = NewRect("Track", gaugeRoot);
        Stretch(track);

        Image band = NewImage("LockZone", track, null, lockZoneBand);
        band.rectTransform.anchorMin = new Vector2(0f, 0.8f);
        band.rectTransform.anchorMax = new Vector2(1f, 1f);
        band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;

        fillImage = NewImage("Fill", track, gaugeFillSprite, fillNormal);
        fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, 0f);

        Image lockLine = NewImage("LockLine", track, null, new Color(1f, 0.85f, 0.4f, 0.9f));
        lockLine.rectTransform.anchorMin = new Vector2(-0.4f, 0.8f);
        lockLine.rectTransform.anchorMax = new Vector2(1.4f, 0.8f);
        lockLine.rectTransform.offsetMin = new Vector2(0f, -1.5f);
        lockLine.rectTransform.offsetMax = new Vector2(0f, 1.5f);

        TextMeshProUGUI top = NewText("100", gaugeRoot, "100", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 22f), new Vector2(80f, 26f));
        TextMeshProUGUI bottom = NewText("0", gaugeRoot, "0", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(bottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(80f, 26f));

        // HOLD (นับถอยหลังในโซนล็อก)
        RectTransform hold = NewRect("Hold", gaugeRoot);
        Place(hold, new Vector2(1f, 0.9f), new Vector2(80f, 0f), new Vector2(110f, 44f));
        holdGroup = hold.gameObject.AddComponent<CanvasGroup>();
        TextMeshProUGUI holdText = NewText("Label", hold, holdLabel, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(holdText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(110f, 22f));
        Image holdBg = NewImage("BarBg", hold, null, new Color(0f, 0f, 0f, 0.7f));
        Place(holdBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(100f, 8f));
        Image holdFill = NewImage("BarFill", holdBg.rectTransform, null, fillLockZone);
        holdBarFill = holdFill.rectTransform;
        holdBarFill.anchorMin = Vector2.zero;
        holdBarFill.anchorMax = new Vector2(0f, 1f);
        holdBarFill.offsetMin = holdBarFill.offsetMax = Vector2.zero;

        // คำใบ้ลากเมาส์ขึ้น
        RectTransform hint = NewRect("DragHint", root);
        Place(hint, new Vector2(0.5f, 0.5f), gaugePosition + new Vector2(130f, 0f), new Vector2(160f, 200f));
        TextMeshProUGUI arrow = NewText("Arrow", hint, "▲", 46, FontStyles.Bold, TextAlignmentOptions.Center);
        hintArrow = arrow.rectTransform;
        Place(hintArrow, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(80f, 60f));
        if (mouseIconSprite != null)
        {
            Image mouse = NewImage("Mouse", hint, mouseIconSprite, Color.white);
            mouse.preserveAspect = true;
            Place(mouse.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(56f, 72f));
        }
        TextMeshProUGUI drag = NewText("Text", hint, dragHint, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(drag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(200f, 26f));

        // Tutorial
        tutorialText = NewText("Tutorial", root, tutorialHint, 26, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(tutorialText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 90f));
        tutorialText.gameObject.SetActive(false);

        retryLabel = NewText("Retry", root, retryText, 54, FontStyles.Bold, TextAlignmentOptions.Center);
        retryLabel.color = new Color(1f, 0.35f, 0.3f, 1f);
        Place(retryLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(700f, 80f));
        retryLabel.gameObject.SetActive(false);

        built = true;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = new Color(0.92f, 0.9f, 0.85f, 1f);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}
