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
    public bool isAiming = false;

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
            isAiming = Input.GetMouseButton(1);

            if (animator != null)
            {
                animator.SetBool("IsAiming", isAiming);
            }

            if (isAiming)
            {
                FaceMouseCursor();
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
            // ระบบยิงปืน (Ranged) ต้องกดเล็งคลิกขวาก่อน ถึงจะยิงได้
            if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            {
                if (isAiming)
                {
                    PerformRangeAttack(weapon as RangedWeaponData);
                }
                else
                {
                    Debug.Log("<color=red>ยิงไม่ได้! ต้องคลิกขวาเพื่อเปลี่ยนมุมมองเล็งก่อน!</color>");
                }
            }
        }
    }

    private void HandleCrosshairFocus(RangedWeaponData gun)
    {
        bool isMoving = rb.linearVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            currentSpreadAngle = gun.maxSpreadAngle;
        }
        else
        {
            float shrinkRate = gun.maxSpreadAngle / gun.focusTime;
            currentSpreadAngle -= shrinkRate * Time.deltaTime;
            currentSpreadAngle = Mathf.Max(0f, currentSpreadAngle);
        }
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
        if (isAiming)
        {
            return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        }
        return Input.mousePosition;
    }
}