using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
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

    private Vector2 targetSize;

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
            if (crosshairImage != null)
            {
                bool isFocusing = gunfire != null && gunfire.IsFocusing;
                bool isCritReady = gunfire != null && gunfire.IsCritReady;
                Color targetColor = isCritReady ? critReadyColor : (isFocusing ? focusColor : normalColor);
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
