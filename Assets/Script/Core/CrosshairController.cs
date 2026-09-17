using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("References")]
    public RectTransform crosshairRect;
    public PlayerCombat playerCombat;

    [Header("Crosshair Settings")]
    public float baseSize = 40f;
    public float sizeMultiplier = 8f;
    public float lerpSpeed = 15f;

    private Vector2 targetSize;

    void Update()
    {
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
        bool shouldShowCrosshair = hasGunEquipped && !isInventoryOpen && !JournalController.IsAnyOpen;

        // เปิด/ปิด การแสดงผลของเป้าเล็ง
        if (crosshairRect.gameObject.activeSelf != shouldShowCrosshair)
        {
            crosshairRect.gameObject.SetActive(shouldShowCrosshair);
        }

        // โชว์เมาส์ปกติของ Windows เมื่อไม่มีเป้าปืน (มือเปล่า/ถือขวาน/เปิดกระเป๋า)
        Cursor.visible = !shouldShowCrosshair;

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
        }
    }
}