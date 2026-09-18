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
    // 🌟 [Alien Shooter Update] isAiming ตอนนี้ไม่ใช่ "โหมดเล็ง FPS" อีกต่อไป
    // แค่บอกว่า "กำลังกดคลิกขวาค้าง" เพื่อทำให้ปืนนิ่งขึ้น (แคบกรวยกระสุน + เดินช้าลง) เท่านั้น
    // ถือปืนคือยิงได้เลยโดยไม่ต้องกดคลิกขวาก่อน
    public bool isAiming = false;

    [Header("Alien Shooter Aim Settings")]
    [Tooltip("ตอนกดคลิกขวาค้าง (นิ่งขึ้น) กรวยกระสุนจะแคบลงเหลือกี่เท่าของค่าปกติ")]
    [Range(0f, 1f)] public float steadySpreadMultiplier = 0.35f;
    [Tooltip("ตอนกดคลิกขวาค้าง (นิ่งขึ้น) ความเร็วเดินจะเหลือกี่เท่า")]
    [Range(0f, 1f)] public float steadyAimSpeedMultiplier = 0.5f;

    /// <summary>ตัวละครถือปืนอยู่ไหม (ให้ PlayerMovement ใช้ตัดสินใจเรื่องหันหน้า/ความเร็ว)</summary>
    public bool HasRangedWeaponEquipped => GetEquippedWeapon()?.itemType == ItemType.RangedWeapon;

    private Dictionary<ItemData, int> weaponAmmoMemory = new Dictionary<ItemData, int>();
    private float nextAttackTime = 0f;

    // สำหรับระบบง้างฟัน (Charge Attack)
    [HideInInspector] public bool isChargingMelee = false;
    private float holdChargeTime = 0f;
    
    [Header("Combat Feel Settings")]
    public float heavyChargeThreshold = 0.4f; // กดค้างขั้นต่ำเพื่อให้นับว่าเป็นการโจมตีหนัก
    public float maxHeavyChargeTime = 1.5f; // ชาร์จครบเวลานี้ จะปล่อยฟันอัตโนมัติ
    public float attackMovementPause = 0.4f; // เวลาที่ถูกหยุดเดินตอนกำลังฟันดาบ (เพื่อให้ท่ายืนตีชัดเจน)
    public float heavyChargeSpeedMultiplier = 0.7f; // เดินช้าลง 30% ตอนง้าง

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

        // 🌟 [แก้บั๊กแอนิเมชันกระพริบ] ล้างสถานะค้างของอาวุธชิ้นเก่าให้หมดก่อนเปลี่ยนอาวุธ
        // ปัญหาเดิม: กดค้างจะฟันอยู่ แล้วสลับไปปืน -> โค้ดที่ปล่อยค่าอยู่ในสาขาที่ถูกข้าม
        // ทำให้ isChargingMelee ค้าง true (เดินช้าถาวร) และ trigger ค้างในคิว
        // แล้วไปเด้งทีหลังตอนยิงปืน = ตัวละครกระพริบเข้าท่าฟัน (Test_Attack) เสี้ยววิ
        isChargingMelee = false;
        holdChargeTime = 0f;
        isCurrentAttackHeavy = false;

        bool holdingGun = (newWeapon != null && newWeapon.itemType == ItemType.RangedWeapon);
        if (animator != null)
        {
            animator.SetBool("HasGun", holdingGun);

            // ล้าง trigger ที่อาจค้างอยู่ในคิว ไม่ให้ข้ามมาเด้งกับอาวุธชิ้นใหม่
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
        if (isReloading) return;

        ItemData weapon = GetEquippedWeapon();

        if (weapon != null && weapon.itemType == ItemType.RangedWeapon)
        {
            // 🌟 คลิกขวาค้าง = "นิ่งขึ้น" (แคบกรวย + เดินช้า) ไม่ใช่กดเพื่อให้ยิงได้อีกต่อไป
            isAiming = Input.GetMouseButton(1);

            if (animator != null)
            {
                // 🌟 [Alien Shooter Update] ห้ามส่ง true เด็ดขาด!
                // ใน Player_Anim.controller มี transition ผูกกับ IsAiming ที่ลากตัวละครเข้า state "Aim_Idle"
                // ซึ่งเป็นท่าเล็ง FPS เก่า (หันหลังให้กล้อง) ทำให้สไปรท์หันหน้าขึ้นค้างตอนกดคลิกขวา
                // ตอนนี้คลิกขวา = แค่ "นิ่งขึ้น" ไม่ต้องเปลี่ยนท่า ให้ใช้ blend tree 8 ทิศตามเมาส์เหมือนเดิม
                animator.SetBool("IsAiming", false);
            }

            // 🌟 ถือปืนคือหันตามเมาส์ตลอดเวลา ไม่ต้องกดคลิกขวาก่อน
            FaceMouseCursor();

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

        if (weapon == null || weapon.itemType == ItemType.MeleeWeapon || weapon.itemType == ItemType.General)
        {
            // ระบบง้างฟัน (Melee Charge) - ป้องกันการสแปมตีเบารัวๆ ด้วยการเช็คคูลดาวน์ก่อนเริ่มง้าง
            if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            {
                isChargingMelee = true;
                holdChargeTime = 0f;
            }

            if (isChargingMelee)
            {
                holdChargeTime += Time.deltaTime;

                if (holdChargeTime >= maxHeavyChargeTime || Input.GetMouseButtonUp(0))
                {
                    isChargingMelee = false;
                    bool isHeavy = holdChargeTime >= heavyChargeThreshold;
                    PerformMeleeAttack(weapon as MeleeWeaponData, isHeavy);
                }
            }
        }
        else if (weapon.itemType == ItemType.RangedWeapon)
        {
            // 🌟 ระบบยิงปืนแบบ Alien Shooter: ถือปืนคือยิงได้เลย ไม่ต้องกดคลิกขวาก่อน
            if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            {
                PerformRangeAttack(weapon as RangedWeaponData);
            }
        }
    }

    // กันไฟล์ไอเทมที่ตั้งค่าผิด (itemType = ปืน แต่ไฟล์ไม่ได้สร้างจาก RangedWeaponData)
    // ไม่ให้สาด NullReferenceException ทุกเฟรมจนเกมพัง — เตือนครั้งเดียวพอ
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

    private void PerformMeleeAttack(MeleeWeaponData weapon, bool isHeavy)
    {
        isCurrentAttackHeavy = isHeavy;
        float cooldown = weapon != null ? (isHeavy ? weapon.heavyAttackCooldown : weapon.lightAttackCooldown) : (isHeavy ? defaultCooldown * 2f : defaultCooldown);
        nextAttackTime = Time.time + cooldown;

        // สั่งหยุดเดินชั่วคราวตอนตี
        if (playerMovement != null)
        {
            playerMovement.ApplyAttackPause(attackMovementPause);
        }

        // 🌟 [ทาง B] ไม่สั่งเปลี่ยนท่าแอนิเมชันตอนฟันแล้ว
        // เหตุผล: state "Test_Attack" / "Heavy_Attack" ใน Animator เป็น "คลิปเดี่ยว" ที่วาดท่าหันหลังไว้
        // ไม่สนใจ AimX/AimZ เลย -> กดฟันทีไรตัวละครหันหลังทุกที (และเด้งไปกวนตอนยิงปืนด้วย)
        // ตอนนี้ปล่อยให้ตัวละครคงท่าเดิม (blend tree 8 ทิศ หันตามเมาส์) แล้วสื่อการฟันด้วย VFX รอยดาบแทน
        Debug.Log(isHeavy ? "<color=magenta>💪 ฟันหนัก (Heavy)</color>" : "<color=cyan>🔪 ฟันเบา (Light)</color>");

        // เดิม hitbox เปิด/ปิดด้วย Animation Event ที่ฝังในคลิปท่าฟัน
        // พอไม่เล่นคลิปนั้นแล้ว ต้องเปิด/ปิดเองด้วยโค้ด ไม่งั้นฟันไม่โดนอะไรเลย
        StartCoroutine(MeleeSwingRoutine());
    }

    /// <summary>
    /// จังหวะฟัน 1 ครั้ง: ง้าง -> เปิด hitbox (+เสก VFX รอยดาบ) -> ปิด hitbox
    /// แทนที่ Animation Event เดิมที่ฝังอยู่ในคลิปท่าฟัน (ซึ่งเลิกเล่นแล้วตามทาง B)
    /// </summary>
    private IEnumerator MeleeSwingRoutine()
    {
        if (meleeReceiver == null) meleeReceiver = GetComponentInChildren<CombatAnimationReceiver>();

        if (meleeReceiver == null)
        {
            Debug.LogWarning("PlayerCombat: หา CombatAnimationReceiver ไม่เจอ (ต้องอยู่บนก้อนลูกที่มี Animator) — ฟันแล้วจะไม่เกิดดาเมจ");
            yield break;
        }

        if (meleeWindupTime > 0f) yield return new WaitForSeconds(meleeWindupTime);

        meleeReceiver.OnAttackActive();   // เปิด hitbox + ส่งค่าดาเมจ + เสก VFX รอยดาบ 8 ทิศ

        yield return new WaitForSeconds(meleeActiveTime);

        meleeReceiver.OnAttackDeactive(); // ปิด hitbox
    }

    // ฟังก์ชันสำหรับส่งค่าพลังโจมตีปัจจุบัน ให้ CombatAnimationReceiver เอาไปใช้เปิด Hitbox
    public int GetCurrentWeaponDamage()
    {
        MeleeWeaponData weapon = GetEquippedWeapon() as MeleeWeaponData;
        if (weapon != null)
        {
            return isCurrentAttackHeavy ? weapon.heavyAttackDamage : weapon.damage;
        }
        return isCurrentAttackHeavy ? defaultHeavyDamage : defaultDamage;
    }

    // ฟังก์ชันสำหรับส่งค่าผลักกระเด็น
    public float GetCurrentWeaponKnockback()
    {
        ItemData weapon = GetEquippedWeapon();
        if (weapon is MeleeWeaponData meleeData) return meleeData.knockback;
        if (weapon is RangedWeaponData rangedData) return rangedData.knockback;
        return 0f;
    }

    // ฟังก์ชันสำหรับส่ง Prefab ควัน/แสงดาบ ให้ CombatAnimationReceiver
    public GameObject GetCurrentWeaponVFX()
    {
        return isCurrentAttackHeavy ? heavySlashVFX : lightSlashVFX;
    }

    public void PerformStrikeDamage()
    {
        // [REFACRTOR] ถูกลบทิ้งและย้ายไปใช้ MeleeHitbox + Animation Event แทนแล้ว
        // ฟังก์ชันนี้เก็บไว้เฉยๆ กันสคริปต์อื่นพังถ้าเคยเรียกใช้
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

        // หาจุดตัดบนพื้น (Y=0) สมมติว่าพื้นอยู่ที่ระดับเดียวกับผู้เล่น
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
        // 🌟 ไม่มีกล้อง FPS ซูมกลางจอแล้ว ยิงตามตำแหน่งเมาส์บนจอเสมอ
        return Input.mousePosition;
    }
}