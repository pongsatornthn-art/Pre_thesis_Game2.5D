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

    private PlayerMovement playerMovement;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;
        playerMovement = GetComponent<PlayerMovement>();

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

        bool holdingGun = (newWeapon != null && newWeapon.itemType == ItemType.RangedWeapon);
        if (animator != null)
        {
            animator.SetBool("HasGun", holdingGun);
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

    private void HandleCrosshairFocus(RangedWeaponData gun)
    {
        // 🌟 กระสุนสุ่มกระจายในกรวยตามปกติ (ยิ่งเดินยิ่งกว้าง ยืนนิ่งยิ่งแคบ)
        // ถ้ากดคลิกขวาค้าง (นิ่งขึ้น) กรวยจะแคบลงอีกชั้น ไม่ว่าจะเดินอยู่หรือไม่ก็ตาม
        bool isMoving = rb.linearVelocity.magnitude > 0.1f;
        float targetSpread = isMoving ? gun.maxSpreadAngle : 0f;

        if (isAiming) targetSpread *= steadySpreadMultiplier;

        float moveRate = gun.maxSpreadAngle / gun.focusTime;
        currentSpreadAngle = Mathf.MoveTowards(currentSpreadAngle, targetSpread, moveRate * Time.deltaTime);
    }

    private void PerformRangeAttack(RangedWeaponData gun)
    {
        if (currentAmmoInMag <= 0)
        {
            Debug.Log("<color=yellow>แชะ! กระสุนหมด (เล่นเสียงปืนเปล่า)</color>");
            return;
        }

        nextAttackTime = Time.time + gun.lightAttackCooldown;
        currentAmmoInMag--;

        weaponAmmoMemory[gun] = currentAmmoInMag;
        currentSpreadAngle = Mathf.Min(gun.maxSpreadAngle, currentSpreadAngle + gun.recoilSpread);

        Ray ray = mainCam.ScreenPointToRay(GetAimScreenPosition());
        Vector3 targetPoint = ray.GetPoint(100f);

        // 🌟 อัปเดต 1: ใส่ QueryTriggerInteraction.Ignore เพื่อสั่งให้กล้องเมิน Trigger ล่องหน 🌟
        RaycastHit[] cameraHits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closestDistance = Mathf.Infinity;

        foreach (RaycastHit camHit in cameraHits)
        {
            if (camHit.collider.gameObject != this.gameObject && camHit.collider.transform.root != this.transform)
            {
                if (camHit.distance < closestDistance)
                {
                    closestDistance = camHit.distance;
                    targetPoint = camHit.point;
                }
            }
        }

        Vector3 aimDirection = (targetPoint - attackPoint.position).normalized;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 0.001f) aimDirection.Normalize();

        float randomSpread = Random.Range(-currentSpreadAngle, currentSpreadAngle);
        Vector3 shootDirection = Quaternion.Euler(0, randomSpread, 0) * aimDirection;

        Vector3 hitPoint;

        // 🌟 อัปเดต 2: ใส่ QueryTriggerInteraction.Ignore ตอนยิงกระสุนด้วย 🌟
        if (Physics.Raycast(attackPoint.position, shootDirection, out RaycastHit hit, gun.attackRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;

            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null && hit.collider.gameObject != this.gameObject)
            {
                damageable.TakeDamage(gun.damage, gun.knockback);
                Debug.Log($"<color=orange>ยิงโดนเป้าหมาย! ดาเมจ {gun.damage}</color>");
            }
        }
        else
        {
            hitPoint = attackPoint.position + (shootDirection * gun.attackRange);
        }

        if (bulletTracerPrefab != null)
        {
            BulletTracer tracer = Instantiate(bulletTracerPrefab);
            tracer.Setup(attackPoint.position, hitPoint);
        }

        // เล่นแสงแฟลชที่ปลายปืน (ถ้ามี)
        MuzzleFlashEffect flash = attackPoint.GetComponentInChildren<MuzzleFlashEffect>();
        if (flash != null) flash.PlayFlash();

        if (animator != null) animator.SetTrigger("Shoot");
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

        if (animator != null)
        {
            if (isHeavy)
            {
                Debug.Log("<color=magenta>💪 ชาร์จฟันหนัก (Heavy Attack)! ตรวจสอบว่าใน Animator มี Trigger ชื่อ HeavyAttack หรือยัง</color>");
                animator.SetTrigger("HeavyAttack");
            }
            else
            {
                Debug.Log("<color=cyan>🔪 ฟันเบา (Light Attack)!</color>");
                animator.SetTrigger("Attack");
            }
        }
        else
        {
            // ถ้าไม่มี animator ให้ถือว่าโจมตีไม่ได้ (บังคับใช้ Animation Event)
            Debug.LogWarning("PlayerCombat: ไม่มี Animator! ระบบตีระยะประชิดต้องการ Animator เพื่อสร้าง Event");
        }
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