using UnityEngine;

[CreateAssetMenu(fileName = "New Melee Weapon", menuName = "Inventory/Items/Melee Weapon")]
public class MeleeWeaponData : ItemData
{
    [Header("Combat Stats (Melee)")]
    public int damage = 10;
    public int heavyAttackDamage = 20;
    public float attackRange = 2f;
    public float lightAttackCooldown = 0.5f;
    public float heavyAttackCooldown = 1.0f;
    [Tooltip("สตามิน่าที่ใช้ต่อการตีเบา (คลิกซ้าย) — เต็มหลอด 100")]
    public float staminaCost = 10f;
    [Tooltip("สตามิน่าที่ใช้ต่อการตีหนัก (คลิกขวา) — ตารางต้า 2026-11-12: 90")]
    public float heavyStaminaCost = 90f;
    public float knockback = 3f;

    [Header("Durability & Animation")]
    public float maxDurability = 100f;
    public AnimatorOverrideController weaponAnimatorOverride;

    [Header("Special Effects")]
    public bool causesBleeding = false;
    public float bleedDuration = 10f;
    public int bleedDamagePerSec = 2;

    private void Reset() { itemType = ItemType.MeleeWeapon; } // ตั้งค่าประเภทอัตโนมัติ
}