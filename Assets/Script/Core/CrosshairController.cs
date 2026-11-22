using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    /// <summary>[แก้ 2026-11-20] Image = รูปเป้าเดิม (ขยายทั้งรูป) · Lines = 4 ขีด + จุดกลาง
    /// [2026-11-22] Circle = วงกลมเส้นบาง + จุดกลาง · ไม่เล็ง = วงใหญ่ · กดคลิกขวาค้าง = วงหดลงตามความแม่นจริงของกระสุน</summary>
    public enum CrosshairStyle { Image, Lines, Circle }

    /// <summary>
    /// 🌟 [ข้อ 9] true = มีระบบอื่นคุมเมาส์อยู่ (FirstPersonModeController ตอนเข้ามินิเกม/โต๊ะ/วิทยุ/ดันประตู)
    /// สคริปต์นี้จะซ่อนเป้าและ "ไม่ยุ่งกับ Cursor" จนกว่าจะคืนเป็น false
    /// </summary>
    public static bool IsCursorOverriddenByMinigame = false;

    [Header("References")]
    public RectTransform crosshairRect;
    public PlayerCombat playerCombat;
    [Tooltip("คอมโพเนนต์คุมการยิง (ถ้าเว้นว่างจะค้นหาจาก PlayerCombat ให้อัตโนมัติ)")]
    public Combat.Gun.GunfireController gunfire;
    [Tooltip("รูปภาพสไปรท์เป้าเล็งสำหรับเปลี่ยนสี")]
    public Image crosshairImage;

    [Header("Crosshair Settings")]
    public float baseSize = 40f;
    public float sizeMultiplier = 8f;
    public float lerpSpeed = 15f;

    [Header("Focus State (ข้อ 12: สีบอกสถานะโฟกัส)")]
    [Tooltip("สีเป้าเล็งปกติ (ไม่ได้โฟกัสศัตรู)")]
    public Color normalColor = Color.white;
    [Tooltip("สีเป้าเล็งเมื่อโฟกัส (หยุดเดิน + เล็งโดนศัตรู)")]
    public Color focusColor = Color.yellow;
    [Tooltip("[คริติคอล] สีเป้าตอนหุบสุด = นัดถัดไปคริแน่นอน (ตั้งให้ต่างจากสีโฟกัสพอสังเกตได้ แต่ไม่ฉูดฉาด)")]
    public Color critReadyColor = new Color(1f, 0.32f, 0.28f);

    [Header("รูปแบบเป้า (แก้ 2026-11-20)")]
    [Tooltip("Image = รูปเป้าเดิม · Lines = เป้าแบบใหม่ 4 ขีด + จุดกลาง (สร้างเอง ไม่ต้องมีไฟล์รูป)")]
    public CrosshairStyle style = CrosshairStyle.Lines;
    [Tooltip("ความยาวแต่ละขีด (พิกเซล UI)")]
    public float lineLength = 10f;
    [Tooltip("ความหนาขีด")]
    public float lineThickness = 3f;
    [Tooltip("ช่องว่างจากจุดกลางถึงขีด ตอนยืนนิ่งเล็งแม่นสุด")]
    public float minGap = 5f;
    [Tooltip("ขีดถ่างออกเพิ่มกี่พิกเซล ต่อความบานกระสุน 1 องศา (เดินอยู่/เพิ่งยิง = บานมาก)")]
    public float gapPerSpreadDegree = 2.5f;
    public bool showCenterDot = true;
    public float centerDotSize = 3f;
    [Tooltip("ขอบดำรอบเป้า ให้เห็นชัดบนพื้นสว่าง")]
    public bool outline = true;
    public Color outlineColor = new Color(0f, 0f, 0f, 0.8f);

    [Header("เป้าแบบวงกลม (Style = Circle)")]
    [Tooltip("รัศมีวงตอนไม่ได้กดคลิกขวา (วงใหญ่)")]
    public float circleIdleRadius = 55f;
    [Tooltip("รัศมีเล็กสุด ตอนเล็งนิ่งจนแม่นสุด (เป้าหุบสุด = นัดคริ)")]
    public float circleMinRadius = 6f;
    [Tooltip("รัศมีตอนกดคลิกขวาค้าง แต่กระสุนยังบานสุด (เล็งที่ว่าง/เพิ่งขยับ) — ยิ่งต่างจาก Idle มาก ยิ่งเห็นวงหดชัดตอนกดคลิกขวา\n" +
             "จากนั้นเล็งโดนศัตรูแล้วยืนนิ่ง วงจะหดต่อจนถึง Min Radius (= นัดคริ)")]
    public float circleAimRadius = 30f;
    [HideInInspector] public float circleRadiusPerDegree = 3f; // ค่าเก่า (v1) ไม่ใช้แล้ว
    [Tooltip("ความหนาเส้นวง")]
    public float circleThickness = 1.2f;
    [Tooltip("ขนาดจุดกลาง (0 = ไม่มีจุด)")]
    public float circleDotSize = 2.5f;
    [Tooltip("เส้นเงาดำจางๆ ใต้วง ให้เห็นชัดบนพื้นสว่าง")]
    public bool circleShadow = true;
    [Tooltip("ความเร็ววงหด/ขยาย (ยิ่งเยอะยิ่งไว)")]
    public float circleLerpSpeed = 12f;

    private Vector2 targetSize;
    private RectTransform circleRoot;
    private UIRingGraphic circleRing, circleRingShadow, circleDot;
    private float currentRadius;
    private RectTransform linesRoot;
    private RectTransform lineTop, lineBottom, lineLeft, lineRight;
    private Image[] lineImages;
    private float currentGap;
    private Color currentColor = Color.white;

    // 🌟 [แก้ 2026-11-20] จำผลไว้แล้วค่อยสั่ง Cursor ใน LateUpdate
    // เพราะ InventoryUI / สคริปต์อื่นเคยสั่ง Cursor.visible = true ทุกเฟรมแย่งกัน → เมาส์โผล่ตอนถือปืน
    private bool applyCursor;
    private bool wantCursorVisible = true;

    void Start()
    {
        if (crosshairImage == null && crosshairRect != null)
        {
            crosshairImage = crosshairRect.GetComponent<Image>();
            if (crosshairImage == null) crosshairImage = crosshairRect.GetComponentInChildren<Image>();
        }
        if (gunfire == null && playerCombat != null)
        {
            gunfire = playerCombat.GetComponent<Combat.Gun.GunfireController>();
        }

        currentColor = normalColor;
        currentGap = minGap;
        currentRadius = circleIdleRadius;
        if (style == CrosshairStyle.Lines) BuildLines();
        else if (style == CrosshairStyle.Circle) BuildCircle();
    }

    // ───────── เป้าแบบวงกลม ─────────
    private void BuildCircle()
    {
        if (crosshairRect == null || circleRoot != null) return;
        if (crosshairImage != null) crosshairImage.enabled = false; // เก็บรูปเดิมไว้ สลับกลับได้

        GameObject root = new GameObject("CH_Circle", typeof(RectTransform));
        circleRoot = (RectTransform)root.transform;
        circleRoot.SetParent(crosshairRect, false);
        circleRoot.anchorMin = circleRoot.anchorMax = circleRoot.pivot = new Vector2(0.5f, 0.5f);
        circleRoot.anchoredPosition = Vector2.zero;
        circleRoot.sizeDelta = Vector2.zero;
        UndoParentScale(circleRoot);

        if (circleShadow) circleRingShadow = NewRing("RingShadow", circleThickness + 2f, false, new Color(0f, 0f, 0f, 0.45f));
        circleRing = NewRing("Ring", circleThickness, false, currentColor);
        if (circleDotSize > 0f)
        {
            circleDot = NewRing("Dot", 0f, true, currentColor);
            circleDot.rectTransform.sizeDelta = Vector2.one * circleDotSize;
        }
        SetCircleRadius(currentRadius);
    }

    /// <summary>
    /// [2026-11-22] ในซีน CrosshairImage ถูกย่อไว้ที่ Scale 0.1 (ไว้ใช้กับรูป center_dot เดิม)
    /// ถ้าไม่หักออก วง/ขีดจะเล็กลง 10 เท่า และเส้นบางจนหาย → ตัวเลขขนาดใน Inspector = พิกเซล UI จริงเสมอ
    /// </summary>
    private static void UndoParentScale(RectTransform child)
    {
        Vector3 ps = child.parent != null ? child.parent.localScale : Vector3.one;
        child.localScale = new Vector3(
            Mathf.Abs(ps.x) > 0.0001f ? 1f / ps.x : 1f,
            Mathf.Abs(ps.y) > 0.0001f ? 1f / ps.y : 1f,
            1f);
    }

    private UIRingGraphic NewRing(string n, float thick, bool fill, Color c)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIRingGraphic));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(circleRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        UIRingGraphic g = go.GetComponent<UIRingGraphic>();
        g.raycastTarget = false;
        g.Filled = fill;
        g.Thickness = thick;
        g.color = c;
        return g;
    }

    private void SetCircleRadius(float r)
    {
        float d = r * 2f;
        if (circleRing != null) circleRing.rectTransform.sizeDelta = new Vector2(d, d);
        // เงาใหญ่กว่าเส้นจริง 1 พิกเซลรอบด้าน → เห็นเป็นขอบดำบางๆ ทั้งในและนอกเส้น
        if (circleRingShadow != null) circleRingShadow.rectTransform.sizeDelta = new Vector2(d + 2f, d + 2f);
    }

    // ───────── เป้าแบบใหม่: 4 ขีด + จุดกลาง ─────────
    private void BuildLines()
    {
        if (crosshairRect == null || linesRoot != null) return;

        // ซ่อนรูปเป้าเดิม (เก็บไว้ไม่ลบ สลับกลับเป็น Image ได้)
        if (crosshairImage != null) crosshairImage.enabled = false;

        GameObject root = new GameObject("CH_Lines", typeof(RectTransform));
        linesRoot = (RectTransform)root.transform;
        linesRoot.SetParent(crosshairRect, false);
        linesRoot.anchorMin = linesRoot.anchorMax = linesRoot.pivot = new Vector2(0.5f, 0.5f);
        linesRoot.anchoredPosition = Vector2.zero;
        linesRoot.sizeDelta = Vector2.zero;
        UndoParentScale(linesRoot);

        lineTop = NewLine("Top", new Vector2(lineThickness, lineLength));
        lineBottom = NewLine("Bottom", new Vector2(lineThickness, lineLength));
        lineLeft = NewLine("Left", new Vector2(lineLength, lineThickness));
        lineRight = NewLine("Right", new Vector2(lineLength, lineThickness));
        if (showCenterDot) NewLine("Dot", new Vector2(centerDotSize, centerDotSize));

        lineImages = linesRoot.GetComponentsInChildren<Image>(true);
        PlaceLines(currentGap);
    }

    private RectTransform NewLine(string n, Vector2 size)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(linesRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = currentColor;
        if (outline)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = outlineColor;
            o.effectDistance = new Vector2(1f, -1f);
        }
        return rt;
    }

    private void PlaceLines(float gap)
    {
        if (linesRoot == null) return;
        float off = gap + lineLength * 0.5f;
        lineTop.anchoredPosition = new Vector2(0f, off);
        lineBottom.anchoredPosition = new Vector2(0f, -off);
        lineLeft.anchoredPosition = new Vector2(-off, 0f);
        lineRight.anchoredPosition = new Vector2(off, 0f);
    }

    void Update()
    {
        // 0. 🌟 [ข้อ 9] ถ้ามินิเกมคุมเมาส์อยู่ ห้ามสั่ง Cursor.visible ทับ (เดิมทำให้เมาส์หายตอนถือปืนเข้ามินิเกม)
        if (IsCursorOverriddenByMinigame)
        {
            applyCursor = false;
            if (crosshairRect != null && crosshairRect.gameObject.activeSelf)
                crosshairRect.gameObject.SetActive(false);
            return;
        }

        // 1. เช็กว่าผู้เล่นเปิดกระเป๋าอยู่ไหม
        bool isInventoryOpen = false;
        if (InventoryUI.Instance != null && InventoryUI.Instance.inventoryPanel != null)
        {
            isInventoryOpen = InventoryUI.Instance.inventoryPanel.activeSelf;
        }

        // 2. เช็กว่าอาวุธที่ถืออยู่ตอนนี้คือ "ปืน" (RangedWeapon) ใช่หรือไม่
        bool hasGunEquipped = false;
        if (Inventory.Instance != null && Inventory.Instance.currentEquippedItem != null)
        {
            hasGunEquipped = Inventory.Instance.currentEquippedItem.itemType == ItemType.RangedWeapon;
        }

        // 3. เงื่อนไขการโชว์เป้าเล็ง: ต้องถือปืนอยู่ และ ไม่ได้เปิดหน้าต่าง UI ใดๆ
        //    ⚠️ ต้องเช็คสมุด (JournalController) ด้วย ไม่งั้นบรรทัด Cursor.visible ข้างล่าง
        //    จะซ่อนเมาส์ทุกเฟรมตอนเปิดหน้า K/N แล้วกดอะไรในสมุดไม่ได้เลย
        // 🌟 [แก้ 2026-11-20] หน้าจอที่ต้องใช้เมาส์คลิก (เลือกเซฟ/มินิเกม/ซูมดูของ/จัมป์สแกร์/เมนูหยุด) → ซ่อนเป้า โชว์เมาส์
        bool uiNeedsMouse = GameplayInputGate.Blocked
                            || StoryCloseUpView.Active != null
                            || StalkerJumpscareUI.AnyActive
                            || (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
        bool shouldShowCrosshair = hasGunEquipped && !isInventoryOpen && !JournalController.IsAnyOpen && !uiNeedsMouse;

        // เปิด/ปิด การแสดงผลของเป้าเล็ง
        if (crosshairRect.gameObject.activeSelf != shouldShowCrosshair)
        {
            crosshairRect.gameObject.SetActive(shouldShowCrosshair);
        }

        // โชว์เมาส์ปกติของ Windows เมื่อไม่มีเป้าปืน (มือเปล่า/ถือขวาน/เปิดกระเป๋า)
        applyCursor = true;
        wantCursorVisible = !shouldShowCrosshair;

        // 4. ถ้าเป้าเล็งเปิดใช้งานอยู่ ให้ทำงาน 2 อย่างนี้
        if (shouldShowCrosshair)
        {
            // 🌟 [Alien Shooter Update] ไม่มีโหมดเล็ง FPS แล้ว เป้าจึงตามเมาส์ตลอดเวลา
            // (ของเดิมจะล็อกเป้าไปกลางจอตอนกดคลิกขวา ซึ่งตอนนี้คลิกขวาแปลว่า "นิ่งขึ้น" ไม่ใช่ซูมกล้อง)
            crosshairRect.position = Input.mousePosition;

            // 🎯 คำนวณความบานของเป้า ตามสถานะการเดิน/ยืนนิ่ง
            if (playerCombat != null)
            {
                float spread = playerCombat.currentSpreadAngle;
                float targetWidthHeight = baseSize + (spread * sizeMultiplier);
                targetSize = new Vector2(targetWidthHeight, targetWidthHeight);

                // ค่อยๆ ย่อขยายขนาดให้สมูท
                crosshairRect.sizeDelta = Vector2.Lerp(crosshairRect.sizeDelta, targetSize, Time.deltaTime * lerpSpeed);
            }

            // 🌟 [ข้อ 12] เปลี่ยนสีเป้าเล็งตามสถานะกำลังโฟกัสศัตรู
            // เมื่อผู้เล่นหยุดเดิน + เมาส์เล็งอยู่บนศัตรู เป้าจะเปลี่ยนเป็นสีโฟกัส (เช่น สีเหลือง)
            bool isFocusing = gunfire != null && gunfire.IsFocusing;
            bool isCritReady = gunfire != null && gunfire.IsCritReady;   // [นา 2026-11-21] เป้าหุบสุด = นัดถัดไปคริ
            Color targetColor = isCritReady ? critReadyColor : (isFocusing ? focusColor : normalColor);

            if (style == CrosshairStyle.Circle && circleRoot != null)
            {
                // ไม่กดคลิกขวา = วงใหญ่ · กดค้าง (เล็ง) = วงเท่าความบานกระสุนจริง
                // (กรวยจะหุบตอนยืนนิ่ง + เล็งโดนศัตรู · คลิกขวาค้างหุบไวขึ้น 2 เท่า + แคบลงเหลือ 35% — ตามระบบปืนของนา)
                bool aiming = playerCombat != null && playerCombat.isAiming;
                float spreadNow = playerCombat != null ? playerCombat.currentSpreadAngle : 0f;

                // ความบานเทียบกับบานสุดของปืนที่ถือ (0 = แม่นสุด · 1 = บานสุด)
                float maxSpread = 15f;
                if (Inventory.Instance != null && Inventory.Instance.currentEquippedItem is RangedWeaponData gun && gun.maxSpreadAngle > 0.01f)
                    maxSpread = gun.maxSpreadAngle;
                float spread01 = Mathf.Clamp01(spreadNow / maxSpread);

                // ไม่กด = วงใหญ่ · กดค้าง = หดทันทีเหลือ Aim Radius แล้วหดต่อตามความแม่นจริง จนถึง Min Radius
                float targetR = aiming
                    ? Mathf.Lerp(circleMinRadius, Mathf.Min(circleAimRadius, circleIdleRadius), spread01)
                    : circleIdleRadius;

                float k = Time.unscaledDeltaTime * circleLerpSpeed;
                currentRadius = Mathf.Lerp(currentRadius, targetR, k);
                SetCircleRadius(currentRadius);

                currentColor = Color.Lerp(currentColor, targetColor, Time.unscaledDeltaTime * lerpSpeed);
                if (circleRing != null) circleRing.color = currentColor;
                if (circleDot != null) circleDot.color = currentColor;
            }
            else if (style == CrosshairStyle.Lines && linesRoot != null)
            {
                // ขีดถ่างออกตามความบานกระสุน (ใช้ unscaled กันค้างตอนสโลว์โมชัน)
                float spreadNow = playerCombat != null ? playerCombat.currentSpreadAngle : 0f;
                float targetGap = minGap + spreadNow * gapPerSpreadDegree;
                float k = Time.unscaledDeltaTime * lerpSpeed;
                currentGap = Mathf.Lerp(currentGap, targetGap, k);
                PlaceLines(currentGap);

                currentColor = Color.Lerp(currentColor, targetColor, k);
                foreach (Image img in lineImages) if (img != null) img.color = currentColor;
            }
            else if (crosshairImage != null)
            {
                crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, Time.deltaTime * lerpSpeed);
            }
        }
    }

    // LateUpdate รันหลัง Update ของทุกสคริปต์ → ค่าของเราเป็นค่าสุดท้ายของเฟรม ไม่โดนทับ
    void LateUpdate()
    {
        if (!applyCursor || IsCursorOverriddenByMinigame) return;
        Cursor.visible = wantCursorVisible;
    }
}
