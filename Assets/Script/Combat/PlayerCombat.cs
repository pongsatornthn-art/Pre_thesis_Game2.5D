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

    [Header("Unarmed Stats")]
    public int defaultDamage = 5;
    public float defaultRange = 1.5f;
    public float defaultCooldown = 0.5f;

    [Header("Gun States")]
    public int currentAmmoInMag;
    public float currentSpreadAngle;
    private bool isReloading = false;
    public bool isAiming = false;

    // 🌟 1. เพิ่มตั้งค่าความไวของการหันเมาส์ตอนเล็ง
    [Header("360 Aiming Settings (เล็งหมุนรอบตัว)")]
    public float aimSensitivity = 3f; // ความไวเมาส์ (ปรับใน Inspector ได้เลย)
    private float currentAimAngle = 0f; // องศาการหันหน้าปัจจุบัน

    private Dictionary<ItemData, int> weaponAmmoMemory = new Dictionary<ItemData, int>();
    private float nextAttackTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnEquipChanged += HandleWeaponEquipped;

            if (Inventory.Instance.currentEquippedItem != null)
            {
                HandleWeaponEquipped(Inventory.Instance.currentEquippedItem);
            }
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

        if (holdingGun)
        {
            if (weaponAmmoMemory.ContainsKey(newWeapon))
            {
                currentAmmoInMag = weaponAmmoMemory[newWeapon];
            }
            else
            {
                currentAmmoInMag = newWeapon.magazineSize;
                weaponAmmoMemory[newWeapon] = currentAmmoInMag;
            }
            currentSpreadAngle = newWeapon.maxSpreadAngle;
        }
    }

    void Update()
    {
        if (isReloading) return;

        ItemData weapon = GetEquippedWeapon();
        bool isHoldingGun = (weapon != null && weapon.itemType == ItemType.RangedWeapon);

        if (isHoldingGun)
        {
            isAiming = Input.GetMouseButton(1); // เล็งเมื่อคลิกขวา

            // 🌟 อัปเดตการหันหน้าตลอดเวลาที่ถือปืน
            FaceMouseCursor();

            HandleCrosshairFocus(weapon);

            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazineSize)
            {
                StartCoroutine(ReloadSequence(weapon));
                return;
            }
        }
        else
        {
            isAiming = false;
            // ถ้าไม่ได้ถือปืน ปลดล็อคเมาส์
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (Time.time >= nextAttackTime)
        {
            if (Input.GetMouseButtonDown(0)) // กดยิง (คลิกซ้าย)
            {
                if (weapon == null || weapon.itemType == ItemType.MeleeWeapon || weapon.itemType == ItemType.General)
                {
                    PerformMeleeAttack(weapon);
                }
                else if (isHoldingGun)
                {
                    if (isAiming)
                    {
                        PerformRangeAttack(weapon);
                    }
                    else
                    {
                        Debug.Log("<color=red>ยิงไม่ได้! ต้องคลิกขวาเพื่อเล็งก่อน!</color>");
                    }
                }
            }
        }
    }

    private void HandleCrosshairFocus(ItemData gun)
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

    private void PerformRangeAttack(ItemData gun)
    {
        if (currentAmmoInMag <= 0)
        {
            return;
        }

        nextAttackTime = Time.time + gun.lightAttackCooldown;
        currentAmmoInMag--;
        weaponAmmoMemory[gun] = currentAmmoInMag;
        currentSpreadAngle = Mathf.Min(gun.maxSpreadAngle, currentSpreadAngle + gun.recoilSpread);

        // 🌟 ยิง Raycast จากลํากล้องกล้อง (Main Camera) ไปยังตำแหน่งกึ่งกลางหน้าจอ (Crosshair)
        // หรือถ้าอยากให้ยิงตามเมาส์ ใช้ Input.mousePosition แทนได้ครับ
        Ray ray = mainCam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        Plane groundPlane = new Plane(Vector3.up, attackPoint.position);

        Vector3 targetPoint = attackPoint.position + (attackPoint.forward * gun.attackRange);

        if (groundPlane.Raycast(ray, out float distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        // คำนวณทิศทางจากปากกระบอกปืน (attackPoint) ไปยังเป้าหมายที่ Crosshair ชี้
        Vector3 aimDirection = (targetPoint - attackPoint.position).normalized;
        aimDirection.y = 0f; // ล็อกแกน Y ไม่ให้กระสุนเหิน
        if (aimDirection.sqrMagnitude > 0.001f) aimDirection.Normalize();

        // คำนวณความกระจายของกระสุน (Spread)
        float randomSpread = Random.Range(-currentSpreadAngle, currentSpreadAngle);
        Vector3 shootDirection = Quaternion.Euler(0, randomSpread, 0) * aimDirection;

        Vector3 hitPoint;

        // ยิง Raycast ออกไปเช็กการชนของกระสุน
        if (Physics.Raycast(attackPoint.position, shootDirection, out RaycastHit hit, gun.attackRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;

            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null && hit.collider.gameObject != this.gameObject)
            {
                damageable.TakeDamage(gun.damage, gun.knockback);
            }
        }
        else
        {
            hitPoint = attackPoint.position + (shootDirection * gun.attackRange);
        }

        // สร้างเส้นวิถีกระสุน (Tracer)
        if (bulletTracerPrefab != null)
        {
            BulletTracer tracer = Instantiate(bulletTracerPrefab);
            tracer.Setup(attackPoint.position, hitPoint);
        }

        // เอฟเฟกต์ปากกระบอกปืน
        MuzzleFlashEffect flash = attackPoint.GetComponentInChildren<MuzzleFlashEffect>();
        if (flash != null) flash.PlayFlash();
    }

    private IEnumerator ReloadSequence(ItemData gun)
    {
        int ammoNeeded = gun.magazineSize - currentAmmoInMag;
        int ammoInInventory = Inventory.Instance.GetItemCount(gun.ammoType);

        if (ammoInInventory <= 0) yield break;

        isReloading = true;
        yield return new WaitForSeconds(gun.reloadTime);

        int ammoToReload = Mathf.Min(ammoNeeded, ammoInInventory);
        Inventory.Instance.RemoveItem(gun.ammoType, ammoToReload);

        currentAmmoInMag += ammoToReload;
        weaponAmmoMemory[gun] = currentAmmoInMag;
        isReloading = false;
    }

    private void PerformMeleeAttack(ItemData weapon)
    {
        float cooldown = weapon != null ? weapon.lightAttackCooldown : defaultCooldown;
        nextAttackTime = Time.time + cooldown;
        PerformStrikeDamage();
    }

    public void PerformStrikeDamage()
    {
        ItemData weapon = GetEquippedWeapon();
        int damage = weapon != null ? weapon.damage : defaultDamage;
        float range = weapon != null ? weapon.attackRange : defaultRange;
        float knockback = weapon != null ? weapon.knockback : 0f;

        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, range);
        foreach (Collider enemy in hitEnemies)
        {
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && enemy.gameObject != this.gameObject)
            {
                damageable.TakeDamage(damage, knockback);
            }
        }
    }

    private ItemData GetEquippedWeapon()
    {
        if (Inventory.Instance != null) return Inventory.Instance.currentEquippedItem;
        return null;
    }

    public void ReceiveDamageInterrupt()
    {
        ItemData weapon = GetEquippedWeapon();
        if (weapon != null && weapon.itemType == ItemType.RangedWeapon)
        {
            currentSpreadAngle = weapon.maxSpreadAngle;
        }
    }

    // 🌟 3. ระบบล็อคเมาส์กลางจอ และรับค่าการสะบัดเมาส์หมุนตัว 360 องศา
    private void FaceMouseCursor()
    {
        if (isAiming)
        {
            // ล็อคเมาส์ไว้ตรงกลาง และซ่อนเมาส์
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

 
            float mouseX = Input.GetAxisRaw("Mouse X");

            // แปลงองศาเป็นทิศทาง (แกน X, Z)
            float rad = currentAimAngle * Mathf.Deg2Rad;
            Vector3 lookDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            // ส่งข้อมูลให้ Animator เพื่อเปลี่ยนภาพหันซ้ายขวาหน้าหลัง
            if (lookDirection.sqrMagnitude > 0.001f && animator != null)
            {
                animator.SetFloat("AimX", lookDirection.x);
                animator.SetFloat("AimZ", lookDirection.z);
            }
        }
        else
        {
            // ถ้าไม่ได้เล็ง (ปล่อยคลิกขวา) ปลดล็อคเมาส์คืนให้
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // ให้ตัวละครหันตามเมาส์ปกติไปก่อน เพื่อเก็บข้อมูลองศาล่าสุด (เวลายกปืนเล็งจะได้ไม่หันกระตุก)
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPoint = ray.GetPoint(distance);
                Vector3 lookDirection = (targetPoint - transform.position).normalized;
                lookDirection.y = 0f;

                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    lookDirection.Normalize();

                    // เซฟองศาปัจจุบันเอาไว้
                    currentAimAngle = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;

                    if (animator != null)
                    {
                        animator.SetFloat("AimX", lookDirection.x);
                        animator.SetFloat("AimZ", lookDirection.z);
                    }
                }
            }
        }
    }
}