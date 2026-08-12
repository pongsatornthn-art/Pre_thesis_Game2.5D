using UnityEngine;
using UnityEngine.Scripting;

public enum ItemType { General, MeleeWeapon, RangedWeapon, Ammo, Totem, Consumable }

[Preserve]
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("General Info")]
    public string itemName = "New Item";
    public Sprite icon;
    [TextArea] public string description;
    public Sprite descriptionImage;

    [Header("Stacking & Type")]
    public bool isStackable = true;
    public int maxStack = 99;
    public ItemType itemType;

    [Header("Equipment Visuals")]
    public Sprite equippedSprite;

    [Header("Animation Settings")]
    public AnimatorOverrideController weaponAnimatorOverride;

    [Header("Combat Stats (Melee & General)")]
    public int damage = 10;
    public float attackRange = 2f;
    public float lightAttackCooldown = 0.5f;
    public float attackCooldown = 0.5f;
    public float staminaCost = 10f;
    public float knockback = 3f;

    [Header("Range Combat Stats (สำหรับปืน)")]
    public ItemData ammoType;           // ชนิดกระสุนที่ต้องใช้
    public int magazineSize = 12;       // จำนวนกระสุนสูงสุดในแมกกาซีน
    public float reloadTime = 1.5f;     // เวลาที่ใช้รีโหลด
    public float focusTime = 1.5f;      // เวลาบีบเป้าให้แคบสุด
    public float maxSpreadAngle = 15f;  // เป้าบานสูงสุด (องศา)
    public float recoilSpread = 5f;     // เป้าบานขึ้นทุกครั้งที่ยิง 1 นัด

    [Header("Durability & Economy")]
    public float maxDurability;
    public int price = 50;

    [Header("Consumable Stats (ยา/อาหาร)")]
    public float digestionReduceAmount = 20f;
    public int healAmount = 50;

    [Header("Special Effects")]
    public bool causesBleeding = false;
    public float bleedDuration = 10f;
    public int bleedDamagePerSec = 2;
}