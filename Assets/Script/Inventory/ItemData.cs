using UnityEngine;
using UnityEngine.Scripting;

public enum ItemType { General, Weapon, Totem, Consumable }

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
    public Sprite equippedSprite; // <--- ตัวนี้ที่ Inventory บ่นหาครับ

    [Header("Animation Settings")]
    public AnimatorOverrideController weaponAnimatorOverride;

    [Header("Combat Stats (สำหรับการต่อสู้)")]
    public int damage = 10;
    public float attackRange = 2f;      // ระยะโจมตี (เพิ่มเข้ามาใหม่)
    public float lightAttackCooldown = 0.5f; // เพิ่มคูลดาวน์ (ถ้าของเดิมยังไม่มี)
    public float attackCooldown = 0.5f; // ความเร็วการตี (เพิ่มเข้ามาใหม่)
    public float staminaCost = 10f;
    public float knockback = 3f;

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