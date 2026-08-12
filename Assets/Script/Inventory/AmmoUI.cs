using UnityEngine;
using TMPro; // 🌟 เรียกใช้ TextMeshPro

public class AmmoUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject ammoPanel;         // เปิด/ปิด กรอบ UI กระสุนทั้งหมด
    public TextMeshProUGUI ammoText;     // ตัวหนังสือแสดงจำนวนกระสุน

    [Header("System References")]
    public PlayerCombat playerCombat;    // ลาก Player มาใส่เพื่อดึงค่ากระสุนในแมก

    [Header("Color Settings")]
    public Color normalColor = Color.white;
    public Color emptyColor = Color.red; // สีแดงเตือนกระสุนหมด

    void Update()
    {
        // เช็กความพร้อมของระบบ
        if (Inventory.Instance == null || playerCombat == null) return;

        ItemData currentWeapon = Inventory.Instance.currentEquippedItem;

        // 1. เช็กว่าผู้เล่นกำลังถือ "ปืน" อยู่ใช่หรือไม่?
        if (currentWeapon != null && currentWeapon.itemType == ItemType.RangedWeapon)
        {
            // เปิดโชว์หน้าต่าง UI กระสุน
            if (!ammoPanel.activeSelf) ammoPanel.SetActive(true);

            // 2. ดึงจำนวนกระสุนในรังเพลิง (จาก PlayerCombat)
            int ammoInMag = playerCombat.currentAmmoInMag;

            // 3. นับจำนวนกระสุนสำรองในกระเป๋า (จาก Inventory)
            int spareAmmo = 0;
            if (currentWeapon.ammoType != null)
            {
                spareAmmo = Inventory.Instance.GetItemCount(currentWeapon.ammoType);
            }

            // 4. อัปเดตตัวหนังสือตามรูปแบบ GDD เช่น [ 6 / 12 ]
            if (ammoText != null)
            {
                ammoText.text = $"[ {ammoInMag} / {spareAmmo} ]";

                // 5. ระบบแจ้งเตือน: เปลี่ยนสีเมื่อไม่มีกระสุนสำรองเหลือแล้ว
                if (spareAmmo <= 0)
                {
                    ammoText.color = emptyColor;
                }
                else
                {
                    ammoText.color = normalColor;
                }
            }
        }
        else
        {
            // ซ่อน UI กระสุนทันที ถ้าถือมีด ถือขวาน หรือมือเปล่า
            if (ammoPanel.activeSelf) ammoPanel.SetActive(false);
        }
    }
}