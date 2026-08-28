using UnityEngine;

[CreateAssetMenu(fileName = "New Ranged Weapon", menuName = "Inventory/Items/Ranged Weapon")]
public class RangedWeaponData : ItemData
{
    [Header("Combat Stats (Gun)")]
    public int damage = 10;
    public float attackRange = 20f;
    public float lightAttackCooldown = 0.5f;
    public float knockback = 1f;
    public AnimatorOverrideController weaponAnimatorOverride;

    [Header("Gun Mechanics")]
    public ItemData ammoType;
    public int magazineSize = 12;
    public float reloadTime = 1.5f;
    public float focusTime = 1.5f;
    public float maxSpreadAngle = 15f;
    public float recoilSpread = 5f;

    private void Reset() { itemType = ItemType.RangedWeapon; }
}