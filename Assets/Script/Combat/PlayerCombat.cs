using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Transform attackPoint;
    private Rigidbody rb;
    private Camera mainCam;
    public BulletTracer bulletTracerPrefab;
    [SerializeField] private Combat.Gun.GunfireController gunfire;

    [Header("Unarmed Stats (สเตตัสมือเปล่า)")]
    public int defaultDamage = 20;
    public int defaultHeavyDamage = 35;
    public float defaultRange = 1.5f;
    public float defaultCooldown = 0.4f;

    [Header("Global Melee VFX")]
    [Tooltip("ควัน/แสงรอยดาบ สำหรับท่าฟันเบา (ใช้กับทุกอาวุธประชิด)")]
    public GameObject lightSlashVFX;
    [Tooltip("ควัน/แสงรอยดาบ สำหรับท่าฟันหนัก (ใช้กับทุกอาวุธประชิด)")]
    public GameObject heavySlashVFX;

    [Header("Gun States (สถานะปืน)")]
    public int currentAmmoInMag;
    public float currentSpreadAngle;
    private bool isReloading = false;
    public bool isAiming = false;

    [Header("Alien Shooter Aim Settings")]
    [Tooltip("ตอนกดคลิกขวาค้าง (นิ่งขึ้น) กรวยกระสุนจะแคบลงเหลือกี่เท่าของค่าปกติ")]
    [Range(0f, 1f)] public float steadySpreadMultiplier = 0.35f;
    [Tooltip("ตอนกดคลิกขวาค้าง (นิ่งขึ้น) ความเร็วเดินจะเหลือกี่เท่า")]
    [Range(0f, 1f)] public float steadyAimSpeedMultiplier = 0.5f;

    public bool HasRangedWeaponEquipped => GetEquippedWeapon()?.itemType == ItemType.RangedWeapon;

    private Dictionary<ItemData, int> weaponAmmoMemory = new Dictionary<ItemData, int>();
    private float nextAttackTime = 0f;

    [HideInInspector] public bool isChargingMelee = false;
    private float holdChargeTime = 0f;

    [Header("Combat Feel Settings")]
    public float heavyChargeThreshold = 0.4f;
    public float maxHeavyChargeTime = 1.5f;
    public float attackMovementPause = 0.4f;
    public float heavyChargeSpeedMultiplier = 0.7f;

    [Header("Melee Swing Timing (จังหวะฟัน — คุมด้วยโค้ด ไม่ใช้ Animation Event แล้ว)")]
    [Tooltip("หน่วงก่อนเปิด hitbox (วินาที) — เหมือนจังหวะง้างดาบ")]
    public float meleeWindupTime = 0.08f;
    [Tooltip("hitbox เปิดค้างกี่วินาที (ยิ่งนานยิ่งฟันโดนง่าย)")]
    public float meleeActiveTime = 0.18f;

    private PlayerMovement playerMovement;
    private CombatAnimationReceiver meleeReceiver;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        playerMovement = GetComponent<PlayerMovement>();
        if (gunfire == null) gunfire = GetComponent<Combat.Gun.GunfireController>();

        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnEquipChanged += HandleWeaponEquipped;
        }
    }

    void OnDestroy()
    {
        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnEquipChanged -= HandleWeaponEquipped;
        }
    }

    private void HandleWeaponEquipped(ItemData newWeapon)
    {
        isReloading = false;
        isAiming = false;

        isChargingMelee = false;
        holdChargeTime = 0f;
        isCurrentAttackHeavy = false;

        bool holdingGun = (newWeapon != null && newWeapon.itemType == ItemType.RangedWeapon);
        // 🌟 [อัปเดต] เช็กอาวุธประชิดส่งให้ Animator
        bool holdingMelee = (newWeapon != null && newWeapon.itemType == ItemType.MeleeWeapon);

        if (animator != null)
        {
            animator.SetBool("HasGun", holdingGun);
            animator.SetBool("HasMelee", holdingMelee); // 🌟 [อัปเดต] ส่งสถานะ HasMelee 

            animator.ResetTrigger("Attack");
            animator.ResetTrigger("HeavyAttack");
            animator.ResetTrigger("Shoot");
        }

        if (holdingGun && newWeapon is RangedWeaponData gunData)
        {
            if (weaponAmmoMemory.ContainsKey(newWeapon))
            {
                currentAmmoInMag = weaponAmmoMemory[newWeapon];
            }
            else
            {
                currentAmmoInMag = gunData.magazineSize;
                weaponAmmoMemory[newWeapon] = currentAmmoInMag;
            }
            currentSpreadAngle = gunData.maxSpreadAngle;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (GameplayInputGate.Blocked || (playerMovement != null && (playerMovement.isStunned || playerMovement.IsActionLocked)))
        {
            isAiming = false;
            if (animator != null) animator.SetBool("IsAiming", false);
            return;
        }

        if (isReloading) return;

        ItemData weapon = GetEquippedWeapon();

        FaceMouseCursor();

        if (weapon != null && weapon.itemType == ItemType.RangedWeapon)
        {
            isAiming = Input.GetMouseButton(1);
            if (animator != null)
            {
                animator.SetBool("IsAiming", isAiming);
            }

            HandleCrosshairFocus(weapon as RangedWeaponData);

            if (Input.GetKeyDown(KeyCode.R) && weapon is RangedWeaponData gunData && currentAmmoInMag < gunData.magazineSize)
            {
                StartCoroutine(ReloadSequence(gunData));
                return;
            }
        }
        else
        {
            isAiming = false;
        }

        if (weapon != null && weapon.itemType == ItemType.MeleeWeapon)
        {
            isChargingMelee = false;
            if (Time.time >= nextAttackTime)
            {
                if (Input.GetMouseButtonDown(0)) TryMeleeAttack(weapon as MeleeWeaponData, false);
                else if (Input.GetMouseButtonDown(1)) TryMeleeAttack(weapon as MeleeWeaponData, true);
            }
        }
        else if (weapon != null && weapon.itemType == ItemType.RangedWeapon)
        {
            if (isAiming && Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            {
                PerformRangeAttack(weapon as RangedWeaponData);
            }
        }
    }

    private ItemData warnedBadGun;
    private bool IsGunDataValid(RangedWeaponData gun)
    {
        if (gun != null) return true;

        ItemData eq = GetEquippedWeapon();
        if (eq != null && warnedBadGun != eq)
        {
            warnedBadGun = eq;
            Debug.LogError($"[PlayerCombat] ไอเทม \"{eq.itemName}\" ตั้ง Item Type เป็น RangedWeapon " +
                           $"แต่ไฟล์ asset ไม่ได้สร้างจาก RangedWeaponData -> ใช้ยิงไม่ได้\n" +
                           $"วิธีแก้: สร้างไอเทมใหม่ผ่าน Create > Inventory > Items > Ranged Weapon แล้วใส่ค่าให้เหมือนเดิม");
        }
        return false;
    }

    private void HandleCrosshairFocus(RangedWeaponData gun)
    {
        currentSpreadAngle = gunfire != null ? gunfire.CurrentSpread : 0f;
    }

    private void PerformRangeAttack(RangedWeaponData gun)
    {
        if (!IsGunDataValid(gun)) return;

        if (currentAmmoInMag <= 0)
        {
            if (gunfire != null) gunfire.PlayEmptyClick(gun);
            return;
        }

        nextAttackTime = Time.time + gun.lightAttackCooldown;
        currentAmmoInMag--;
        weaponAmmoMemory[gun] = currentAmmoInMag;
        if (gunfire != null) gunfire.Fire(gun);
    }

    private IEnumerator ReloadSequence(RangedWeaponData gun)
    {
        int ammoNeeded = gun.magazineSize - currentAmmoInMag;
        int ammoInInventory = Inventory.Instance.GetItemCount(gun.ammoType);

        if (ammoInInventory <= 0)
        {
            Debug.Log("<color=red>ไม่มีกระสุนสำรองเหลือแล้ว!</color>");
            yield break;
        }

        isReloading = true;
        if (animator != null) animator.SetTrigger("Reload");
        if (gunfire != null) gunfire.PlayReloadSound(gun);

        yield return new WaitForSeconds(gun.reloadTime);

        int ammoToReload = Mathf.Min(ammoNeeded, ammoInInventory);
        Inventory.Instance.RemoveItem(gun.ammoType, ammoToReload);

        currentAmmoInMag += ammoToReload;
        weaponAmmoMemory[gun] = currentAmmoInMag;

        isReloading = false;
    }

    [HideInInspector] public bool isCurrentAttackHeavy = false;

    public static event System.Action OnOutOfStamina;

    private void TryMeleeAttack(MeleeWeaponData weapon, bool isHeavy)
    {
        float cost = weapon != null ? (isHeavy ? weapon.heavyStaminaCost : weapon.staminaCost) : 0f;
        if (playerMovement != null && !playerMovement.TryUseStamina(cost))
        {
            nextAttackTime = Time.time + 0.25f;
            OnOutOfStamina?.Invoke();
            return;
        }
        PerformMeleeAttack(weapon, isHeavy);
    }

    private void PerformMeleeAttack(MeleeWeaponData weapon, bool isHeavy)
    {
        isCurrentAttackHeavy = isHeavy;
        float cooldown = weapon != null ? (isHeavy ? weapon.heavyAttackCooldown : weapon.lightAttackCooldown) : (isHeavy ? defaultCooldown * 2f : defaultCooldown);
        nextAttackTime = Time.time + cooldown;

        if (playerMovement != null)
        {
            playerMovement.ApplyAttackPause(attackMovementPause);
        }

        if (animator != null)
        {
            if (isHeavy)
            {
                animator.SetTrigger("HeavyAttack");
            }
            else
            {
                animator.SetTrigger("Attack");
            }
        }

        Debug.Log(isHeavy ? "<color=magenta>💪 ฟันหนัก (Heavy)</color>" : "<color=cyan>🔪 ฟันเบา (Light)</color>");

        StartCoroutine(MeleeSwingRoutine());
    }

    private IEnumerator MeleeSwingRoutine()
    {
        if (meleeReceiver == null) meleeReceiver = GetComponentInChildren<CombatAnimationReceiver>();

        if (meleeReceiver == null)
        {
            Debug.LogWarning("PlayerCombat: หา CombatAnimationReceiver ไม่เจอ (ต้องอยู่บนก้อนลูกที่มี Animator) — ฟันแล้วจะไม่เกิดดาเมจ");
            yield break;
        }

        if (meleeWindupTime > 0f) yield return new WaitForSeconds(meleeWindupTime);

        meleeReceiver.OnAttackActive();

        yield return new WaitForSeconds(meleeActiveTime);

        meleeReceiver.OnAttackDeactive();
    }

    public int GetCurrentWeaponDamage()
    {
        MeleeWeaponData weapon = GetEquippedWeapon() as MeleeWeaponData;
        if (weapon != null)
        {
            return isCurrentAttackHeavy ? weapon.heavyAttackDamage : weapon.damage;
        }
        return isCurrentAttackHeavy ? defaultHeavyDamage : defaultDamage;
    }

    public float GetCurrentWeaponKnockback()
    {
        ItemData weapon = GetEquippedWeapon();
        if (weapon is MeleeWeaponData meleeData) return meleeData.knockback;
        if (weapon is RangedWeaponData rangedData) return rangedData.knockback;
        return 0f;
    }

    public GameObject GetCurrentWeaponVFX()
    {
        return isCurrentAttackHeavy ? heavySlashVFX : lightSlashVFX;
    }

    public void PerformStrikeDamage()
    {
        Debug.LogWarning("PerformStrikeDamage ถูกยกเลิก! กรุณาใช้ Animation Event (OnAttackActive) แทน");
    }

    private ItemData GetEquippedWeapon()
    {
        if (Inventory.Instance != null) return Inventory.Instance.currentEquippedItem;
        return null;
    }

    public void ReceiveDamageInterrupt()
    {
        RangedWeaponData weapon = GetEquippedWeapon() as RangedWeaponData;
        if (weapon != null)
        {
            currentSpreadAngle = weapon.maxSpreadAngle;
        }
    }

    private void FaceMouseCursor()
    {
        Ray ray = mainCam.ScreenPointToRay(GetAimScreenPosition());
        Vector3 targetPoint = ray.GetPoint(100f);

        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (groundPlane.Raycast(ray, out float enter))
        {
            targetPoint = ray.GetPoint(enter);
        }

        Vector3 lookDirection = (targetPoint - transform.position).normalized;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            lookDirection.Normalize();
        }

        if (animator != null)
        {
            animator.SetFloat("AimX", lookDirection.x);
            animator.SetFloat("AimZ", lookDirection.z);
        }
    }

    private Vector3 GetAimScreenPosition()
    {
        return Input.mousePosition;
    }
}