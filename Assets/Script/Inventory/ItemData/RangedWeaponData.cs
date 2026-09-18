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

    [Header("กระสุน (โปรเจกไทล์)")]
    [Tooltip("Prefab กระสุนที่มีคอมโพเนนต์ ProjectileBase (เช่น BulletProjectile)")]
    public GameObject projectilePrefab;
    [Tooltip("ความเร็วในการเคลื่อนที่ของกระสุน (ม./วิ) - ค่าเริ่มต้น 25 ช้าพอให้มอนสเตอร์หลบได้ตามธรรมชาติ")]
    public float projectileSpeed = 25f;
    [Tooltip("รัศมีหัวกระสุนสำหรับตรวจจับการชน (SphereCast) ป้องกันกระสุนทะลุกำแพง")]
    public float projectileRadius = 0.08f;

    [Header("กรวยกระสุน (เพิ่มเติม)")]
    [Tooltip("มุมกรวยแคบสุดเมื่อยืนนิ่งและเล็งศัตรูครบเวลา (ตาม GDD กำหนดให้เป็น 0 องศาเพื่อความแม่นยำ)")]
    public float minSpreadAngle = 0f;
    [Tooltip("ตัวคูณขนาดกรวยกระสุนเมื่อกดคลิกขวาค้างเพื่อช่วยให้นิ่งขึ้น")]
    [Range(0f, 1f)] public float steadySpreadMultiplier = 0.35f;
    [Tooltip("ตัวคูณความเร็วในการหุบเป้าเมื่อกดคลิกขวาค้าง (เช่น 2 เท่า)")]
    public float steadyFocusSpeedMultiplier = 2f;
    [Tooltip("รัศมีช่วยเล็งตรวจจับศัตรู (Aim Assist) เนื่องจากสไปรท์บิลบอร์ดมีความกว้างแคบ")]
    public float aimAssistRadius = 0.6f;

    [Header("เสียง (เฟส 2)")]
    [Tooltip("ไฟล์เสียง SFX ขณะยิงกระสุนออกไป")]
    public AudioClip fireSfx;
    [Tooltip("ไฟล์เสียง SFX คลิกปืนเปล่าเมื่อยิงตอนกระสุนหมด")]
    public AudioClip emptySfx;
    [Tooltip("ไฟล์เสียง SFX ขณะเริ่มรีโหลดกระสุน")]
    public AudioClip reloadSfx;
    [Tooltip("ตัวคูณความดังของเสียงปืนที่มีผลต่อระยะได้ยินของสตอล์กเกอร์")]
    public float shotLoudness = 1f;

    [Header("เอฟเฟกต์ (เฟส 2)")]
    [Tooltip("Prefab สะเก็ดไฟเมื่อกระสุนกระทบกำแพง")]
    public GameObject impactWallPrefab;
    [Tooltip("Prefab เลือด/ชิ้นส่วนเมื่อกระสุนกระทบศัตรู")]
    public GameObject impactFleshPrefab;

    private void Reset() { itemType = ItemType.RangedWeapon; }

    /// <summary>
    /// ถอดเฉพาะตัวเลขกรวยกระสุนออกมาให้ WeaponSpread ใช้
    /// (WeaponSpread เป็นสูตรคณิตศาสตร์ล้วน จงใจไม่ให้รู้จัก ScriptableObject จะได้เขียนเทสได้)
    /// </summary>
    public Combat.Gun.SpreadSettings ToSpreadSettings()
    {
        return new Combat.Gun.SpreadSettings
        {
            maxSpreadAngle = maxSpreadAngle,
            minSpreadAngle = minSpreadAngle,
            focusTime = focusTime,
            recoilSpread = recoilSpread,
            steadySpreadMultiplier = steadySpreadMultiplier,
            steadyFocusSpeedMultiplier = steadyFocusSpeedMultiplier
        };
    }
}