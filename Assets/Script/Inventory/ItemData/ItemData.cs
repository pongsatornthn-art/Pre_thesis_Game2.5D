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

    [Header("แปลภาษา (เว้นว่าง = ใช้ข้อความด้านบนตรง ๆ)")]
    [Tooltip("Key ใน LocalizationData.csv สำหรับชื่อไอเทม\n" +
             "เว้นว่างไว้ = ใช้ค่าในช่อง Item Name เหมือนเดิม")]
    public string itemNameKey;

    [Tooltip("Key ใน LocalizationData.csv สำหรับคำอธิบาย\n" +
             "เว้นว่างไว้ = ใช้ค่าในช่อง Description เหมือนเดิม")]
    public string descriptionKey;

    /// <summary>
    /// ชื่อไอเทมที่เอาไปโชว์บนจอ — แปลภาษาให้ถ้าใส่ key ไว้
    ///
    /// ทำแบบ "เพิ่มช่องใหม่ ไม่ทับของเดิม" เพื่อให้ asset ไอเทมที่พิมพ์ชื่อไว้แล้วไม่พัง
    /// ทยอยแปลทีละชิ้นได้ ไอเทมที่ยังไม่ได้ใส่ key ก็ยังโชว์ชื่อเดิมตามปกติ
    /// </summary>
    public string DisplayName => Localize(itemNameKey, itemName);

    /// <summary>คำอธิบายที่เอาไปโชว์บนจอ — แปลภาษาให้ถ้าใส่ key ไว้</summary>
    public string DisplayDescription => Localize(descriptionKey, description);

    private static string Localize(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;

        ILocalizationService loc = ServiceLocator.Get<ILocalizationService>();
        if (loc == null) return fallback;

        string text = loc.GetText(key);

        // LocalizationService คืนตัว key กลับมาเมื่อหาคำแปลไม่เจอ
        // กรณีนั้นให้ใช้ข้อความเดิมแทน ผู้เล่นจะได้ไม่เห็นคำแปลก ๆ อย่าง "ITEM_PISTOL_NAME" บนจอ
        return (string.IsNullOrEmpty(text) || text == key) ? fallback : text;
    }

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