using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [ข้อ 1] UI ของ Door Barricade Struggle — หลอดแนวตั้ง 0–100 + โซนล็อก 80–100 + ตัวนับ HOLD + คำใบ้ลากเมาส์ขึ้น
/// [แก้ 2026-11-20] เพิ่มปุ่มวงกลมด้านขวาสำหรับแบบใหม่ (กดค้างที่ปุ่ม → ลากขึ้น · ปุ่มอยู่กับที่)
///   แบบเก่า = โชว์ลูกศรข้างหลอดเหมือนเดิม · แบบใหม่ = โชว์ปุ่มวงกลม · สลับเองตามแบบที่ใช้อยู่
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
    [Tooltip("คำแนะนำ Tutorial ของแบบใหม่ (ปุ่มวงกลม)")]
    public string tutorialHintButton = "Click and HOLD the circle, then drag UP to push the door shut.\nKeep it in the top zone until it locks!";
    [Tooltip("ข้อความใต้ปุ่มวงกลม (แบบใหม่)")]
    public string buttonHint = "HOLD + DRAG UP";
    [Tooltip("โชว์ป้ายมุมจอว่ากำลังใช้แบบไหน + ปุ่มสลับ (ไว้ทดสอบ ตอนส่งงานจริงปิดได้)")]
    public bool showControlModeLabel = true;
    public string holdLabel = "HOLD";
    public string retryText = "TRY AGAIN";

    [Header("ฟอนต์")]
    [Tooltip("ฟอนต์ของข้อความทั้งหมด — เว้นว่าง = หาจากชื่อด้านล่าง (ฟอนต์ที่ซีนโหลดไว้แล้ว)")]
    public TMP_FontAsset font;
    [Tooltip("ชื่อฟอนต์ที่ใช้หาเอง เมื่อช่อง Font ว่าง (UI สร้างอัตโนมัติจะใช้ทางนี้)")]
    public string fontAssetName = "Iannnnn-COW-Bold SDF 1";

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

    [Header("ปุ่มวงกลม (แบบใหม่)")]
    [Tooltip("ตำแหน่งปุ่ม วัดจากขอบขวากลางจอ (x ติดลบ = เข้ามาทางซ้าย · y ติดลบ = ต่ำลง)")]
    public Vector2 buttonPosition = new Vector2(-300f, -140f);
    [Tooltip("เส้นผ่านศูนย์กลางปุ่ม (หน่วย UI ที่จอ 1920×1080)")]
    public float buttonDiameter = 170f;
    [Tooltip("ขยายพื้นที่กดออกไปรอบปุ่ม (กดพลาดขอบนิดหน่อยก็ยังนับ)")]
    public float buttonHitPadding = 20f;
    public Sprite buttonSprite;
    public Color buttonIdle = new Color(0.92f, 0.9f, 0.85f, 0.85f);
    public Color buttonPressed = new Color(0.95f, 0.75f, 0.25f, 1f);

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
    private RectTransform dragHintRoot;      // แบบเก่า: ลูกศรข้างหลอด
    private RectTransform buttonRoot;        // แบบใหม่: ปุ่มวงกลม
    private RectTransform buttonCircle;
    private Image buttonCore;
    private Image buttonRing;
    private RectTransform buttonArrow;
    private TextMeshProUGUI modeLabel;
    private DoorBarricadeStruggle.PushControl? shownControl;
    private bool lastTutorial;
    private static Sprite generatedCircle;
    private static Sprite generatedRing;

    private float flashAlpha;
    private float retryTimer;
    private float shake;
    private bool built;
    private TMP_FontAsset resolvedFont;

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
        lastTutorial = s.IsTutorial;
        shownControl = null; // บังคับจัดหน้าใหม่ตามแบบที่ใช้
        RefreshControlLayout(s);
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

        // แบบเก่า/ใหม่ (สลับด้วย F8 ได้ระหว่างเล่น)
        if (shownControl != s.CurrentControl) RefreshControlLayout(s);

        // ลูกศรเด้งขึ้นลง
        float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f;
        if (hintArrow != null) hintArrow.anchoredPosition = new Vector2(0f, 30f + bob);

        // ปุ่มวงกลม: กดค้าง = ยุบ + เปลี่ยนสี · ไม่ได้กด = เต้นเบาๆ ชวนให้กด (ปุ่มไม่ขยับตำแหน่ง)
        if (buttonRoot != null && buttonRoot.gameObject.activeSelf)
        {
            bool held = s.IsHoldingButton;
            float targetScale = held ? 0.9f : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.03f;
            buttonCircle.localScale = Vector3.one * Mathf.Lerp(buttonCircle.localScale.x, targetScale, dt * 18f);
            buttonCore.color = Color.Lerp(buttonCore.color, held ? buttonPressed : buttonIdle, dt * 14f);
            buttonRing.color = Color.Lerp(buttonRing.color, held ? buttonPressed : new Color(1f, 1f, 1f, 0.6f), dt * 14f);
            if (buttonArrow != null)
                buttonArrow.anchoredPosition = new Vector2(0f, buttonDiameter * 0.5f + 40f + (held ? bob * 1.6f : bob * 0.6f));
        }

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

    /// <summary>แบบใหม่: จุดบนจอ (พิกัดเมาส์) อยู่บนปุ่มวงกลมไหม</summary>
    public bool IsOverPushButton(Vector2 screenPos)
    {
        if (!built || buttonCircle == null || !buttonRoot.gameObject.activeInHierarchy) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonCircle, screenPos, null, out Vector2 local)) return false;
        float r = buttonDiameter * 0.5f + buttonHitPadding;
        return local.sqrMagnitude <= r * r;
    }

    /// <summary>โชว์ส่วนที่ตรงกับแบบที่ใช้อยู่ (แบบเก่า = ลูกศรข้างหลอด · แบบใหม่ = ปุ่มวงกลม)</summary>
    private void RefreshControlLayout(DoorBarricadeStruggle s)
    {
        if (!built || s == null) return;
        DoorBarricadeStruggle.PushControl mode = s.CurrentControl;
        shownControl = mode;
        bool isButton = mode == DoorBarricadeStruggle.PushControl.HoldButtonDrag;

        if (dragHintRoot != null) dragHintRoot.gameObject.SetActive(!isButton);
        if (buttonRoot != null) buttonRoot.gameObject.SetActive(isButton);
        if (tutorialText != null)
        {
            tutorialText.gameObject.SetActive(lastTutorial);
            tutorialText.text = isButton ? tutorialHintButton : tutorialHint;
        }
        if (modeLabel != null)
        {
            modeLabel.gameObject.SetActive(showControlModeLabel);
            string key = s.toggleControlKey != KeyCode.None ? $"  [{s.toggleControlKey}] switch" : "";
            modeLabel.text = (isButton ? "CONTROL: NEW (hold circle)" : "CONTROL: OLD (drag mouse)") + key;
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
        resolvedFont = ResolveFont();

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

        // คำใบ้ลากเมาส์ขึ้น (แบบเก่า)
        RectTransform hint = NewRect("DragHint", root);
        dragHintRoot = hint;
        Place(hint, new Vector2(0.5f, 0.5f), gaugePosition + new Vector2(130f, 0f), new Vector2(160f, 200f));
        TextMeshProUGUI arrow = NewText("Arrow", hint, "\u25B2", 46, FontStyles.Bold, TextAlignmentOptions.Center);
        if (TMP_Settings.defaultFontAsset != null) arrow.font = TMP_Settings.defaultFontAsset; // ฟอนต์ไทยอาจไม่มีตัว ▲ → ใช้ฟอนต์มาตรฐานเฉพาะลูกศร
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

        // ปุ่มวงกลมด้านขวา (แบบใหม่) — ตัวปุ่มอยู่กับที่ ลากเมาส์ขึ้นแล้วปุ่มไม่ขยับตาม
        buttonRoot = NewRect("PushButton", root);
        buttonRoot.anchorMin = buttonRoot.anchorMax = new Vector2(1f, 0.5f);
        buttonRoot.pivot = new Vector2(0.5f, 0.5f);
        buttonRoot.anchoredPosition = buttonPosition;
        buttonRoot.sizeDelta = new Vector2(buttonDiameter, buttonDiameter);

        buttonCircle = NewRect("Circle", buttonRoot);
        Stretch(buttonCircle);
        buttonRing = NewImage("Ring", buttonCircle, buttonSprite != null ? null : GetRingSprite(), new Color(1f, 1f, 1f, 0.6f));
        Stretch(buttonRing.rectTransform, -10f);
        buttonCore = NewImage("Core", buttonCircle, buttonSprite != null ? buttonSprite : GetCircleSprite(), buttonIdle);
        Stretch(buttonCore.rectTransform);
        if (buttonSprite != null) buttonRing.enabled = false;

        TextMeshProUGUI btnArrow = NewText("Arrow", buttonRoot, "\u25B2", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        if (TMP_Settings.defaultFontAsset != null) btnArrow.font = TMP_Settings.defaultFontAsset;
        buttonArrow = btnArrow.rectTransform;
        Place(buttonArrow, new Vector2(0.5f, 0.5f), new Vector2(0f, buttonDiameter * 0.5f + 40f), new Vector2(80f, 60f));

        TextMeshProUGUI btnText = NewText("Text", buttonRoot, buttonHint, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(btnText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -buttonDiameter * 0.5f - 26f), new Vector2(320f, 28f));
        buttonRoot.gameObject.SetActive(false);

        // ป้ายบอกแบบที่ใช้อยู่ (ไว้ทดสอบ)
        modeLabel = NewText("ControlMode", root, "", 16, FontStyles.Normal, TextAlignmentOptions.Right);
        modeLabel.color = new Color(1f, 1f, 1f, 0.55f);
        modeLabel.rectTransform.anchorMin = modeLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        modeLabel.rectTransform.pivot = new Vector2(1f, 0f);
        modeLabel.rectTransform.anchoredPosition = new Vector2(-24f, 18f);
        modeLabel.rectTransform.sizeDelta = new Vector2(700f, 26f);

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

    // ───────── วงกลม (สร้างเอง ไม่ต้องมีไฟล์รูป) ─────────
    private static Sprite GetCircleSprite()
    {
        if (generatedCircle == null) generatedCircle = MakeDisc("UI_PushCircle", 0f);
        return generatedCircle;
    }

    private static Sprite GetRingSprite()
    {
        if (generatedRing == null) generatedRing = MakeDisc("UI_PushRing", 0.86f);
        return generatedRing;
    }

    /// <summary>วาดวงกลมขอบนุ่ม — inner > 0 = เป็นวงแหวน (เจาะกลาง)</summary>
    private static Sprite MakeDisc(string name, float inner)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        float edge = 1.5f / c;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((1f - d) / edge);
                if (inner > 0f) a *= Mathf.Clamp01((d - inner) / edge);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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

    private TMP_FontAsset ResolveFont()
    {
        if (font != null) return font;
        if (!string.IsNullOrEmpty(fontAssetName))
        {
            // ฟอนต์นี้ใช้ทั่วซีนอยู่แล้ว (ถูกโหลดในหน่วยความจำ) → หาจากชื่อได้ ทั้งใน Editor และตอน Build
            foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name == fontAssetName) return f;
            Debug.LogWarning($"[DoorBarricadeStruggleUI] หาฟอนต์ '{fontAssetName}' ไม่เจอ — ใช้ฟอนต์มาตรฐานแทน (ลากฟอนต์ใส่ช่อง Font ได้)", this);
        }
        return null;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (resolvedFont != null) t.font = resolvedFont;
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