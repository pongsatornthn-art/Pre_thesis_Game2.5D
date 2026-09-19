using UnityEngine;

namespace Combat.Gun
{
    /// <summary>
    /// ผู้ควบคุมระบบการยิงปืน (Facade & Mediator)
    /// ทำหน้าที่ประสานงานระหว่าง AimResolver, WeaponSpread, EnemyAimDetector, GunFeedback และ SimplePool
    /// โดยคลาสนี้ไม่ทำงานด้านเสียง/VFX เอง (ย้ายไป GunFeedback)
    /// และรู้จักกระสุนผ่าน ProjectileBase เท่านั้น (Liskov Substitution Principle)
    /// </summary>
    public class GunfireController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ตำแหน่งปลายกระบอกปืน")]
        [SerializeField] private Transform muzzle;
        [Tooltip("คอมโพเนนต์ช่วยหาทิศทางการเล็ง")]
        [SerializeField] private AimResolver aimResolver;
        [Tooltip("คอมโพเนนต์ควบคุมฟีดแบ็ก (เสียง, แฟลช, สั่น)")]
        [SerializeField] private GunFeedback feedback;
        [Tooltip("Animator สำหรับสั่งทริกเกอร์ยิง")]
        [SerializeField] private Animator animator;
        [Tooltip("Rigidbody สำหรับเช็คการเคลื่อนไหว")]
        [SerializeField] private Rigidbody rb;
        [Tooltip("PlayerCombat เพื่ออ่านสถานะกดคลิกขวาค้าง (isAiming)")]
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Aim Assist")]
        [Tooltip("เลเยอร์ของศัตรูสำหรับตรวจจับว่าเล็งโดนศัตรูหรือไม่ (เว้นว่างจะเช็ค IDamageable)")]
        [SerializeField] private LayerMask enemyLayerMask;

        [Header("Pooling")]
        [Tooltip("จำนวนกระสุนที่เตรียมไว้ล่วงหน้าในพูลตอนหยิบปืนครั้งแรก")]
        [SerializeField] private int prewarmCount = 20;

        private readonly WeaponSpread spread = new WeaponSpread();
        private readonly System.Collections.Generic.HashSet<RangedWeaponData> prewarmedGuns
            = new System.Collections.Generic.HashSet<RangedWeaponData>();
        private EnemyAimDetector aimDetector;
        private RangedWeaponData lastEquippedGun;

        /// <summary>องศากรวยกระสุนปัจจุบัน ส่งต่อให้ CrosshairController นำไปวาดเป้า</summary>
        public float CurrentSpread => spread.CurrentSpread;

        /// <summary>ผู้เล่นยืนนิ่งและเมาส์ชี้อยู่บนตัวศัตรูหรือไม่ (สำหรับเปลี่ยนสีเป้าเล็ง)</summary>
        public bool IsFocusing { get; private set; }

        private void OnEnable()
        {
            // GDD Interrupt Condition: โดนศัตรูตี = เสียหลัก กรวยเด้งบานสุดทันที
            // รับผ่าน GameEventBus เพื่อไม่ต้องผูกกับสคริปต์เลือดของผู้เล่นโดยตรง
            GameEventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnPlayerDamaged(PlayerDamagedEvent ev)
        {
            RangedWeaponData gun = GetCurrentRangedWeapon();
            if (gun != null) spread.OnPlayerHit(gun.ToSpreadSettings());
        }

        private void Awake()
        {
            // ส่ง transform ของผู้เล่นตรง ๆ ไม่ใช่ transform.root
            // ถ้าใช้ root แล้ววันหนึ่งเอา Player ไปใส่โฟลเดอร์จัดระเบียบในซีน
            // root จะกลายเป็นตัวโฟลเดอร์ → ของทุกชิ้นในโฟลเดอร์เดียวกันจะถูกมองว่าเป็น "ตัวผู้ยิง" แล้วโดนข้ามหมด
            aimDetector = new EnemyAimDetector(enemyLayerMask, transform);
            if (aimResolver == null) aimResolver = GetComponent<AimResolver>();
            if (feedback == null) feedback = GetComponent<GunFeedback>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
            if (muzzle == null && playerCombat != null) muzzle = playerCombat.attackPoint;
        }

        private void Update()
        {
            RangedWeaponData currentGun = GetCurrentRangedWeapon();
            if (currentGun != lastEquippedGun)
            {
                lastEquippedGun = currentGun;
                if (currentGun != null)
                {
                    spread.OnWeaponChanged(currentGun.ToSpreadSettings());

                    // เตรียมกระสุนไว้ล่วงหน้าตอนหยิบปืนกระบอกใหม่ครั้งแรก
                    // ไม่งั้นนัดแรก ๆ ยังต้อง Instantiate อยู่ดี ซึ่งเป็นจังหวะที่กระตุกเห็นชัดที่สุด
                    if (currentGun.projectilePrefab != null && prewarmedGuns.Add(currentGun))
                    {
                        SimplePool.Prewarm(currentGun.projectilePrefab, prewarmCount);
                        if (currentGun.impactWallPrefab != null) SimplePool.Prewarm(currentGun.impactWallPrefab, 5);
                        if (currentGun.impactFleshPrefab != null) SimplePool.Prewarm(currentGun.impactFleshPrefab, 5);
                    }
                }
            }

            if (currentGun == null) { IsFocusing = false; return; }

            // รวบรวมบริบทสถานะของผู้เล่นเพื่อส่งให้สูตรคำนวณกรวยกระสุน
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.1f;
            bool isSteady = playerCombat != null && playerCombat.isAiming;
            Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position;
            Vector3 flatAimDir = aimResolver != null ? aimResolver.GetFlatAimDirection(muzzlePos) : transform.forward;
            bool isAimingAtEnemy = aimDetector.IsAimingAtEnemy(muzzlePos, flatAimDir, currentGun.attackRange, currentGun.aimAssistRadius);

            SpreadContext ctx = new SpreadContext
            {
                isMoving = isMoving,
                isDashing = false, // หมายเหตุ: PlayerMovement ไม่มี public bool isDashing จึงส่ง false
                isSteady = isSteady,
                isAimingAtEnemy = isAimingAtEnemy
            };

            // ตาม GDD: เป้าจะหุบได้ต้อง "หยุดเดิน" และ "เมาส์เล็งอยู่บนศัตรู" ทั้งสองอย่าง
            IsFocusing = !isMoving && isAimingAtEnemy;
            spread.Tick(Time.deltaTime, ctx, currentGun.ToSpreadSettings());
        }

        /// <summary>ทำการยิงกระสุน 1 นัด (ผู้เรียกต้องเช็คและหักกระสุนในแมกกาซีนมาก่อนแล้ว)</summary>
        public void Fire(RangedWeaponData gun)
        {
            if (gun == null) return;

            Transform muzzlePoint = muzzle != null ? muzzle : transform;
            Vector3 muzzlePos = muzzlePoint.position;

            // 1. หาเวกเตอร์การเล็งแบบแบนราบจาก AimResolver
            Vector3 baseDir = aimResolver != null ? aimResolver.GetFlatAimDirection(muzzlePos) : transform.forward;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude > 0.0001f) baseDir.Normalize();

            // 2. คำนวณการเบี่ยงเบนของกระสุนรอบแกน Y เท่านั้น (ห้ามเบี่ยงขึ้นลงเพื่อไม่ให้กระสุนมุดพื้นในเกม 2.5D)
            float spreadAngle = spread.RollAngle();
            Vector3 shotDir = Quaternion.Euler(0f, spreadAngle, 0f) * baseDir;

            // 3. ขอยืมกระสุนจาก Object Pool แล้วสั่งยิง (รู้จักแค่ ProjectileBase ตามหลัก Liskov)
            if (gun.projectilePrefab != null)
            {
                GameObject bulletObj = SimplePool.Get(gun.projectilePrefab, muzzlePos, Quaternion.LookRotation(shotDir));
                if (bulletObj != null)
                {
                    ProjectileBase proj = bulletObj.GetComponent<ProjectileBase>();
                    if (proj != null) proj.Launch(muzzlePos, shotDir, gun, gameObject);
                    else Debug.LogError($"[GunfireController] Prefab '{gun.projectilePrefab.name}' ไม่มีคอมโพเนนต์ ProjectileBase");
                }
            }
            else Debug.LogWarning($"[GunfireController] อาวุธ '{gun.itemName}' ยังไม่ได้กำหนด projectilePrefab ใน Inspector");

            // 4. เพิ่มแรงดีดสะสมและรีเซ็ตเวลา Focus
            spread.OnShotFired(gun.ToSpreadSettings());

            // 5. สั่งเล่นเอฟเฟกต์เสียงและแสงแฟลชผ่าน GunFeedback (แยกหน้าที่ตาม SRP)
            if (feedback != null) feedback.PlayShot(gun, muzzlePos);

            // 6. สั่งแอนิเมชันยิงของตัวละคร
            if (animator != null) animator.SetTrigger("Shoot");

            // 7. ส่งอีเวนต์เสียงปืนกระจายในระบบ GameEventBus เพื่อให้มอนสเตอร์รับรู้ (Decoupled)
            GameEventBus.Publish(new GunshotEvent(muzzlePos, gun.shotLoudness));
        }

        public void PlayEmptyClick(RangedWeaponData gun)
        {
            Transform muzzlePoint = muzzle != null ? muzzle : transform;
            if (feedback != null) feedback.PlayEmpty(gun, muzzlePoint.position);
        }

        /// <summary>เล่นเสียงรีโหลด (PlayerCombat เรียกตอนเริ่มรีโหลด)</summary>
        public void PlayReloadSound(RangedWeaponData gun)
        {
            Transform muzzlePoint = muzzle != null ? muzzle : transform;
            if (feedback != null) feedback.PlayReload(gun, muzzlePoint.position);
        }

        /// <summary>สั่งให้กรวยเด้งบานสุดด้วยมือ (เผื่อระบบอื่นอยากเรียกตรง ๆ)</summary>
        public void OnPlayerHit(RangedWeaponData gun)
        {
            if (gun != null) spread.OnPlayerHit(gun.ToSpreadSettings());
        }

        private RangedWeaponData GetCurrentRangedWeapon()
        {
            return (Inventory.Instance != null && Inventory.Instance.currentEquippedItem is RangedWeaponData gun) ? gun : null;
        }
    }
}
