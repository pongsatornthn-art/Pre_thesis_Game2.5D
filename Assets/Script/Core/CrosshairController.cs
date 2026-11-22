using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    /// <summary>[แก้ 2026-11-20] Image = รูปเป้าเดิม (ขยายทั้งรูป) · Lines = เป้าแบบใหม่ 4 ขีด + จุดกลาง (ขีดถ่างออกตามความบานกระสุน)</summary>
    public enum CrosshairStyle { Image, Lines }

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

    private Vector2 targetSize;
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
        if (style == CrosshairStyle.Lines) BuildLines();
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

            if (style == CrosshairStyle.Lines && linesRoot != null)
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
