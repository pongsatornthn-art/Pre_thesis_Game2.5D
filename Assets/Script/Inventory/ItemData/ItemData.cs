using UnityEngine;
using UnityEngine.Scripting;
// ⚠️ เพิ่มชนิดใหม่ให้ "ต่อท้าย" เท่านั้น ห้ามแทรกกลาง
// เพราะ Unity เซฟค่าเป็นตัวเลขลำดับ ถ้าแทรกกลางไอเทมเดิมทุกตัวจะเปลี่ยนประเภทมั่ว
public enum ItemType { General, MeleeWeapon, RangedWeapon, Ammo, Totem, Consumable, Key, Document }

[Preserve]
[CreateAssetMenu(fileName = "New General Item", menuName = "Inventory/Items/General Item")]
public class ItemData : ScriptableObject
{
    [Header("General Info (ข้อมูลพื้นฐาน)")]
    public string itemName = "New Item";
    public Sprite icon;
    [TextArea] public string description;
    public Sprite descriptionImage;

    [Header("Stacking & Type")]
    public bool isStackable = true;
    public int maxStack = 99;
    public ItemType itemType;
    public int price = 50;

    [Header("Equipment Visuals")]
    public Sprite equippedSprite;

    /// <summary>
    /// เก็บไอเทมชิ้นนี้เข้า "คลังที่ถูกต้อง" ของมันเอง
    /// ค่าเริ่มต้น = เข้ากระเป๋าปกติ · ไอเทมชนิดพิเศษให้ override เอา (กุญแจ -> คลังของสำคัญ, เอกสาร -> สมุด)
    ///
    /// ใช้วิธีนี้แทนการเช็ค if (itemType == ...) ตอนเก็บของ
    /// เพิ่มไอเทมชนิดใหม่ในอนาคตแค่ override เมธอดนี้ ไม่ต้องกลับมาแก้ ItemPickup อีก
    /// </summary>
    /// <returns>true = เก็บสำเร็จ (ให้ผู้เรียกลบของออกจากฉากได้)</returns>
    public virtual bool Collect(int amount = 1)
    {
        if (Inventory.Instance == null) return false;
        return Inventory.Instance.AddItem(this, amount);
    }
}