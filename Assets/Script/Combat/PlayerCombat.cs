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
    public int defaultDamage = 5;
    public float defaultRange = 1.5f;
    public float defaultCooldown = 0.5f;

    [Header("Gun States (สถานะปืน)")]
    public int currentAmmoInMag;
    public float currentSpreadAngle;
    private bool isReloading = false;
    public bool isAiming = false;

    private Dictionary<ItemData, int> weaponAmmoMemory = new Dictionary<ItemData, int>();
    private float nextAttackTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

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
        }

        if (Time.time >= nextAttackTime)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (weapon == null || weapon.itemType == ItemType.MeleeWeapon || weapon.itemType == ItemType.General)
                {
                    PerformMeleeAttack(weapon);
                }
                else if (weapon.itemType == ItemType.RangedWeapon)
                {
                    if (isAiming)
                    {
                        PerformRangeAttack(weapon);
                    }
                    else
                    {
                        Debug.Log("<color=red>ยิงไม่ได้! ต้องคลิกขวาเพื่อเปลี่ยนมุมมองเล็งก่อน!</color>");
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

    private IEnumerator ReloadSequence(ItemData gun)
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

    private void PerformMeleeAttack(ItemData weapon)
    {
        float cooldown = weapon != null ? weapon.lightAttackCooldown : defaultCooldown;
        nextAttackTime = Time.time + cooldown;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
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
        ItemData weapon = GetEquippedWeapon();
        return weapon != null ? weapon.damage : defaultDamage;
    }

    // ฟังก์ชันสำหรับส่งค่าผลักกระเด็น
    public float GetCurrentWeaponKnockback()
    {
        ItemData weapon = GetEquippedWeapon();
        return weapon != null ? weapon.knockback : 0f;
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
        ItemData weapon = GetEquippedWeapon();
        if (weapon != null && weapon.itemType == ItemType.RangedWeapon)
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