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

    [Header("360 Aiming Settings (เล็งหมุนรอบตัว)")]
    public float aimSensitivity = 3f;
    private float currentAimAngle = 0f;

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

        // 🌟 ตรวจสอบว่าเป็นปืน (RangedWeaponData) หรือไม่
        if (newWeapon is RangedWeaponData gun)
        {
            if (weaponAmmoMemory.ContainsKey(gun))
            {
                currentAmmoInMag = weaponAmmoMemory[gun];
            }
            else
            {
                currentAmmoInMag = gun.magazineSize;
                weaponAmmoMemory[gun] = currentAmmoInMag;
            }
            currentSpreadAngle = gun.maxSpreadAngle;
        }
    }

    void Update()
    {
        if (isReloading) return;

        ItemData weapon = GetEquippedWeapon();

        // 🌟 แยกการทำงานระหว่าง ปืน กับ อาวุธประชิด/มือเปล่า
        if (weapon is RangedWeaponData gun)
        {
            isAiming = Input.GetMouseButton(1);
            FaceMouseCursor();
            HandleCrosshairFocus(gun);

            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < gun.magazineSize)
            {
                StartCoroutine(ReloadSequence(gun));
                return;
            }

            if (Time.time >= nextAttackTime && Input.GetMouseButtonDown(0))
            {
                if (isAiming)
                {
                    PerformRangeAttack(gun);
                }
                else
                {
                    Debug.Log("<color=red>ยิงไม่ได้! ต้องคลิกขวาเพื่อเล็งก่อน!</color>");
                }
            }
        }
        else
        {
            isAiming = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (Time.time >= nextAttackTime && Input.GetMouseButtonDown(0))
            {
                // ถ้าเป็น Melee หรือ มือเปล่า
                MeleeWeaponData melee = weapon as MeleeWeaponData;
                PerformMeleeAttack(melee);
            }
        }
    }

    // 🌟 รับค่าปืนโดยตรง (RangedWeaponData)
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
        if (currentAmmoInMag <= 0) return;

        nextAttackTime = Time.time + gun.lightAttackCooldown;
        currentAmmoInMag--;
        weaponAmmoMemory[gun] = currentAmmoInMag;
        currentSpreadAngle = Mathf.Min(gun.maxSpreadAngle, currentSpreadAngle + gun.recoilSpread);

        Ray ray = mainCam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        Plane groundPlane = new Plane(Vector3.up, attackPoint.position);
        Vector3 targetPoint = attackPoint.position + (attackPoint.forward * gun.attackRange);

        if (groundPlane.Raycast(ray, out float distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        Vector3 aimDirection = (targetPoint - attackPoint.position).normalized;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 0.001f) aimDirection.Normalize();

        float randomSpread = Random.Range(-currentSpreadAngle, currentSpreadAngle);
        Vector3 shootDirection = Quaternion.Euler(0, randomSpread, 0) * aimDirection;
        Vector3 hitPoint;

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

        if (bulletTracerPrefab != null)
        {
            BulletTracer tracer = Instantiate(bulletTracerPrefab);
            tracer.Setup(attackPoint.position, hitPoint);
        }

        MuzzleFlashEffect flash = attackPoint.GetComponentInChildren<MuzzleFlashEffect>();
        if (flash != null) flash.PlayFlash();
    }

    private IEnumerator ReloadSequence(RangedWeaponData gun)
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

    // 🌟 รับค่าอาวุธประชิด (MeleeWeaponData)
    private void PerformMeleeAttack(MeleeWeaponData weapon)
    {
        float cooldown = weapon != null ? weapon.attackCooldown : defaultCooldown;
        nextAttackTime = Time.time + cooldown;
        // ส่งต่อให้ฟังก์ชันแบบมีพารามิเตอร์
        PerformStrikeDamage(weapon);
    }

    // 🌟 เพิ่มฟังก์ชันนี้เพื่อรับจบให้ AnimationEvent
    public void PerformStrikeDamage()
    {
        ItemData currentItem = GetEquippedWeapon();
        MeleeWeaponData meleeWeapon = currentItem as MeleeWeaponData;

        // ส่งต่อข้อมูลไปคำนวณดาเมจ
        PerformStrikeDamage(meleeWeapon);
    }

    public void PerformStrikeDamage(MeleeWeaponData weapon)
    {
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
        if (weapon is RangedWeaponData gun)
        {
            currentSpreadAngle = gun.maxSpreadAngle;
        }
    }

    private void FaceMouseCursor()
    {
        if (isAiming)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            float mouseX = Input.GetAxisRaw("Mouse X");
            currentAimAngle += mouseX * aimSensitivity;

            float rad = currentAimAngle * Mathf.Deg2Rad;
            Vector3 lookDirection = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            if (lookDirection.sqrMagnitude > 0.001f && animator != null)
            {
                animator.SetFloat("AimX", lookDirection.x);
                animator.SetFloat("AimZ", lookDirection.z);
            }
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

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