using UnityEngine;
using UnityEngine.AI;

public class PTSDMonsterAI : MonoBehaviour, IDamageable
{
    public enum PatrolBehaviorType { Stationary, RoamZone, FollowRoute }

    [Header("Behavior Data (ถูกตั้งค่าโดย Spawner)")]
    public PatrolBehaviorType defaultBehavior = PatrolBehaviorType.Stationary;
    public PatrolZone assignedZone;
    public PatrolRoute assignedRoute;

    [Header("Detection Settings")]
    public float hearingRadius = 5f; 
    [Tooltip("[Claude 2026-11-12 ต้าขอ] ผู้เล่นวิ่ง (Shift) = เสียงดัง ได้ยินไกลถึงรัศมีนี้ ทะลุกำแพง")]
    public float runNoiseRadius = 12f;
    [Tooltip("ผู้เล่นย่อง = วงได้ยินใกล้ตัวหดเหลือกี่เท่า (0.5 = ครึ่งเดียว)")]
    [Range(0.1f, 1f)] public float sneakHearingMultiplier = 0.5f;
    public float sightRadius = 15f; 
    [Range(0, 360)] public float sightAngle = 90f;
    [Tooltip("[งานข้อ 10] ผู้เล่นเข้าใกล้กว่านี้ (เมตร) = รู้ตัวเต็มหลอดทันที แม้อยู่ข้างหลัง (ต้องไม่มีกำแพงกั้น)")]
    public float instantNoticeRadius = 1.5f;
    [Tooltip("[งานข้อ 10] ในวงได้ยิน (Hearing Radius) ยิ่งใกล้หลอดยิ่งขึ้นเร็ว: ขอบวง x1 → ชิดตัว x ค่านี้")]
    public float closeRangeMultiplier = 4f;
    public LayerMask obstacleMask; 
    public LayerMask playerMask; 

    [Header("Combat Settings")]
    public float attackDistance = 1.5f; 
    public int attackDamage = 20; 
    public float attackCooldown = 2f;
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Super Armor")]
    public bool hasSuperArmor = false;
    public float superArmorDuration = 3f; // ตั้งเวลาได้ว่ามันจะอยู่กี่วิ
    private Coroutine superArmorCoroutine;

    // อ่านค่าได้อย่างเดียวให้คนอื่นดึงไปโชว์
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    // 🌟 [SOLID: SRP] สร้าง Event ส่งสัญญาณออกไปเมื่อเลือดลด
    public event System.Action<int, int> OnHealthChanged;

    [Header("Audio & VFX Settings (AAA)")]
    public AudioClip hitSound;
    public AudioClip dieSound;
    [Tooltip("เอฟเฟกต์เลือดสาดตอนโดนตีเบา")]
    public GameObject hitVFXPrefab;
    [Tooltip("เอฟเฟกต์เลือดสาดอลังการตอนโดนตีหนัก")]
    public GameObject heavyHitVFXPrefab;

    [Header("Movement Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;
    [Tooltip("ระยะทางไกลสุดที่มันจะวิ่งไล่ตามออกจากจุดเกิด/โซน ถ้าเกินนี้จะเลิกตาม (หมาเฝ้าบ้าน)")]
    public float chaseLeashDistance = 20f;

    // --- State Machine ---
    private IMonsterState currentState;

    // ===== [Claude 2026-11-12] สัญญาณเตือน "กำลังกรีดร้องเรียกบอส" + ขัดด้วยตีหนัก =====
    [Header("กรีดร้องเรียกบอส (สัญญาณเตือน)")]
    [Tooltip("เสียงสูดหายใจ/เกร็งตัวก่อนกรีดร้อง (เว้นว่างได้ — ยังมีตัวสั่น+แดงเตือน)")]
    public AudioClip screamChargeSound;
    [Tooltip("สีตัวตอนชาร์จกรีดร้อง")]
    public Color screamTellColor = new Color(1f, 0.45f, 0.45f);
    [Tooltip("ความแรงตัวสั่น (เมตร)")]
    public float screamShakeAmount = 0.06f;

    private SpriteRenderer tellSprite;
    private Color tellBaseColor;
    private Vector3 tellBasePos;
    private bool tellActive;

    public void BeginScreamTell()
    {
        if (tellSprite == null) tellSprite = GetComponentInChildren<SpriteRenderer>();
        if (tellSprite == null) return;
        if (!tellActive) { tellBaseColor = tellSprite.color; tellBasePos = tellSprite.transform.localPosition; }
        tellActive = true;
        if (screamChargeSound != null) ServiceLocator.GetOptional<IAudioService>()?.PlaySFX(screamChargeSound, transform.position, 1f, false);
    }

    public void EndScreamTell()
    {
        if (!tellActive) return;
        tellActive = false;
        if (tellSprite == null) return;
        tellSprite.color = tellBaseColor;
        tellSprite.transform.localPosition = tellBasePos;
    }

    private void LateUpdate()
    {
        if (!tellActive || tellSprite == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        tellSprite.color = Color.Lerp(tellBaseColor, screamTellColor, 0.4f + 0.6f * pulse);
        tellSprite.transform.localPosition = tellBasePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * screamShakeAmount;
    }
    public string CurrentStateName => currentState != null ? currentState.GetType().Name : "None";

    // --- References ---
    public NavMeshAgent Agent { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public AwarenessSystem Awareness { get; private set; }
    private PlayerMovement playerScript;
    private Vector3 spawnPosition; // จำจุดเกิดของตัวเองไว้เผื่อต้องเดินกลับ

    [Header("Debug UI (ข้อความลอยบนหัว)")]
    public TMPro.TMP_Text debugStateText;

    // Event สำหรับตะโกนเรียกบอส Stalker
    public static event System.Action<Vector3> OnMinionShout;

    // Event บอกตอนตัวเองตาย (ให้ Spawner ไปหักลบโควต้า)
    public event System.Action OnDeath;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Awareness = GetComponent<AwarenessSystem>();
        
        // ถ้ายังไม่ได้แปะสคริปต์ AwarenessSystem ให้เพิ่มอัตโนมัติ
        if (Awareness == null) 
            Awareness = gameObject.AddComponent<AwarenessSystem>();
            
        spawnPosition = transform.position; // บันทึกจุดที่เกิดมาครั้งแรก
        currentHealth = maxHealth; // เลือดเต็มตอนเกิด
    }

    private void Start()
    {
        playerScript = Object.FindAnyObjectByType<PlayerMovement>();
        if (playerScript != null)
            PlayerTransform = playerScript.transform;
            
        ReturnToDefaultPatrol(); // เริ่มต้นด้วยการลาดตระเวน
    }

    private void Update()
    {
        if (PlayerTransform == null) return;

        // สั่งให้ State ปัจจุบันทำงาน
        currentState?.UpdateState(this);
    }

    /// <summary>
    /// ฟังก์ชันเปลี่ยน State
    /// </summary>
    public void ChangeState(IMonsterState newState)
    {
        if (currentState != null)
            currentState.ExitState(this);

        currentState = newState;
        
        if (currentState != null)
            currentState.EnterState(this);
    }

    /// <summary>
    /// ฟังก์ชันสั่งให้กลับไปเดินตามพฤติกรรมพื้นฐานที่ Spawner สั่งมา
    /// </summary>
    public void ReturnToDefaultPatrol()
    {
        switch (defaultBehavior)
        {
            case PatrolBehaviorType.Stationary:
                ChangeState(new StateStationary());
                break;
            case PatrolBehaviorType.RoamZone:
                ChangeState(new StateRoam());
                break;
            case PatrolBehaviorType.FollowRoute:
                ChangeState(new StateWaypoint());
                break;
        }
    }

    /// <summary>
    /// ฟังก์ชันรับค่าพฤติกรรมมาจาก Spawner (Dependency Injection)
    /// </summary>
    public void InjectBehavior(PatrolBehaviorType type, PatrolZone zone, PatrolRoute route)
    {
        this.defaultBehavior = type;
        this.assignedZone = zone;
        this.assignedRoute = route;
        
        ReturnToDefaultPatrol();
    }

    /// <summary>
    /// หาพิกัด "บ้าน" ของมัน ว่าควรจะยึดระยะ Leash จากตรงไหน
    /// </summary>
    public Vector3 GetHomePosition()
    {
        if (defaultBehavior == PatrolBehaviorType.RoamZone && assignedZone != null)
            return assignedZone.transform.position;
            
        if (defaultBehavior == PatrolBehaviorType.FollowRoute)
            return transform.position; // สำหรับ Waypoint ให้ใช้จุดที่มันยืนอยู่ตอนนั้นเป็นจุดอ้างอิงสายจูง
            
        return spawnPosition; // ถ้ายืนนิ่งๆ ก็ยึดจุดเกิดเป็นบ้าน
    }

    // อัปเดตให้ตรงกับ Interface IDamageable ของเพื่อน
    public void TakeDamage(int damageAmount, float knockback, bool isHeavyAttack = false)
    {
        TakeDamage(damageAmount, isHeavyAttack);
        // (ส่วนกระเด็น knockback ค่อยทำเพิ่มทีหลังถ้าต้องการ)
    }

    public void TakeDamage(int damage, bool isHeavyAttack = false)
    {
        if (currentHealth <= 0) return; // ตายไปแล้วไม่ต้องตีซ้ำ

        currentHealth -= damage;
        
        // 🌟 [SOLID: SRP] ตะโกนบอกทุกสคริปต์ที่รอฟังอยู่ (เช่น UI) ว่าเลือดลดแล้วนะ
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // [Claude 2026-11-12] ตีหนักโดนตอนกำลังชาร์จกรีดร้อง = ขัดได้ (ไม่เรียกบอส + ติดสตัน) — ทะลุ Super Armor เฉพาะกรณีนี้
            if (isHeavyAttack && currentState is StateAttack charging && charging.IsChargingScream)
            {
                Debug.Log($"<color=cyan>{gameObject.name} โดนตีหนักขัดเสียงกรีดร้อง! ไม่ได้เรียกบอส</color>");
                ChangeState(new StateStun(2.5f));
                if (superArmorCoroutine != null) StopCoroutine(superArmorCoroutine);
                superArmorCoroutine = StartCoroutine(SuperArmorRoutine(superArmorDuration));
            }
            else if (hasSuperArmor)
            {
                Debug.Log($"<color=magenta>{gameObject.name} โดนตี {damage} ดาเมจ! แต่มี Super Armor! ไม่ชะงัก!</color>");
            }
            else
            {
                // ถ้าเป็นตีหนัก (Heavy Attack) ให้ติดสตัน 100% 
                if (isHeavyAttack)
                {
                    Debug.Log($"<color=red>{gameObject.name} โดนตีหนัก {damage} ดาเมจ! ติดสตัน!</color>");
                    ChangeState(new StateStun(2.5f)); 
                    
                    // เมื่อโดนตีหนักจนสตันแล้ว ให้ Super Armor ทำงานทันที
                    if (superArmorCoroutine != null) StopCoroutine(superArmorCoroutine);
                    superArmorCoroutine = StartCoroutine(SuperArmorRoutine(superArmorDuration));
                }
                else
                {
                    Debug.Log($"<color=orange>{gameObject.name} โดนตีเบา {damage} ดาเมจ! (ไม่ติดสตัน)</color>");
                }
            }

            // [เพิ่มโดย Claude 2026-11-11 งานข้อ 10] โดนตี/ยิง = รู้ตัวเต็มหลอดทันที แล้วหันมาไล่ (ไม่ต้องรอหลอดค่อยๆ ขึ้น)
            // ตีหนักติดสตันอยู่ → ปล่อยให้สตันจบก่อน (StateStun จะสั่งไล่เอง)
            Awareness.ForceMaxAwareness();
            if (!(currentState is StateStun) && !(currentState is StateChase) && !(currentState is StateAttack))
            {
                ChangeState(new StateChase());
            }

            // 🎵 [AAA Audio Service] เล่นเสียงโดนตีแบบ 3D
            if (hitSound != null)
                ServiceLocator.Get<IAudioService>()?.PlaySFX(hitSound, transform.position);
                
            // 💥 [VFX] เกิดเอฟเฟกต์ตอนโดนตี (แยกตีหนัก/ตีเบา)
            GameObject vfxToSpawn = isHeavyAttack && heavyHitVFXPrefab != null ? heavyHitVFXPrefab : hitVFXPrefab;
            
            if (vfxToSpawn != null)
            {
                // สุ่มตำแหน่งรอบๆ ตัวมอนสเตอร์ (บน ล่าง ซ้าย ขวา)
                Vector3 randomOffset = Random.insideUnitSphere * 0.5f;
                randomOffset.z = 0; // ล็อคแกน Z ไว้ให้อยู่ระนาบเดียวกับสไปรท์ จะได้ไม่จมไปหลังแผ่น Billboard
                
                // หาจุดกึ่งกลางที่แท้จริงของตัวมอนสเตอร์จาก Collider
                Vector3 centerPos = transform.position;
                Collider col = GetComponent<Collider>();
                if (col != null) centerPos = col.bounds.center;
                
                // คำนวณพิกัดพร้อมสุ่มระยะ
                Vector3 vfxPos = centerPos + randomOffset;
                
                // ดึงตำแหน่งมาใกล้กล้องนิดหน่อย (0.3 หน่วย) เพื่อป้องกันแผ่นมอนสเตอร์บังภาพ (Sorting Issue)
                vfxPos -= Camera.main.transform.forward * 0.3f;
                
                Instantiate(vfxToSpawn, vfxPos, Quaternion.identity);
            }
        }
    }

    private System.Collections.IEnumerator SuperArmorRoutine(float duration)
    {
        hasSuperArmor = true;
        // ถ้ายืนสตันอยู่ ให้ข้อความบนหัวโชว์ว่าติดสตันพร้อมกับมีเกราะ
        if (currentState != null && currentState.GetType() == typeof(StateStun))
        {
            ShowDebugText("STUNNED & ARMORED", Color.cyan);
        }
        else
        {
            ShowDebugText("SUPER ARMOR!", Color.magenta);
        }

        yield return new WaitForSeconds(duration);
        hasSuperArmor = false;
        
        // ถ้าหายจาก Super Armor แล้วไม่ใช่ตอนสตัน ค่อยล้างข้อความ
        if (currentState == null || currentState.GetType() != typeof(StateStun))
        {
            ShowDebugText("", Color.white);
        }
    }

    public void Die()
    {
        Debug.Log($"<color=black>💀 {gameObject.name} ตายแล้ว!</color>");
        
        // หยุดทุกอย่าง
        ChangeState(null);
        Agent.isStopped = true;
        
        // ตะโกนบอก Spawner ว่าฉันตายแล้ว หักโควต้าด้วย!
        OnDeath?.Invoke();

        // 🎵 [AAA Audio Service] เล่นเสียงตาย
        if (dieSound != null)
            ServiceLocator.Get<IAudioService>()?.PlaySFX(dieSound, transform.position);

        // นอนตาย (ซ่อนตัวชั่วคราว)
        gameObject.SetActive(false);
    }

    public void ShoutForStalker()
    {
        Debug.Log("📢 มินเนี่ยนตะโกนเรียก Stalker!!");
        OnMinionShout?.Invoke(transform.position); // ส่งสัญญาณให้ Stalker ทั่วแมพรู้
    }

    public void ShowDebugText(string key, Color color)
    {
        if (debugStateText != null)
        {
            if (string.IsNullOrEmpty(key))
            {
                debugStateText.text = "";
            }
            else
            {
                var loc = ServiceLocator.Get<ILocalizationService>();
                debugStateText.text = loc != null ? loc.GetText(key) : key;
            }
            debugStateText.color = color;
        }
    }

    /// <summary>
    /// อัปเดตระบบการรับรู้ (Awareness) - ทำหน้าที่เติม/ลดหลอดเกจแทนการ Return True/False แบบเดิม
    /// </summary>
    public void UpdateDetection()
    {
        if (PlayerTransform == null) return;
        
        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        bool canSeeOrHear = false;

        // [Claude 2026-11-12] ทุกการรับรู้ต้องไม่มีกำแพงคั่น (เดิมได้ยิน/มองทะลุกำแพงบางชิ้น ดูเอ๋อ)
        Vector3 eye = transform.position + Vector3.up * 1f;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        bool clear = distToPlayer <= Mathf.Max(runNoiseRadius, Mathf.Max(hearingRadius, sightRadius)) && !WallBetween(eye, target);

        // วิ่ง = เสียงดัง ได้ยินไกล · ย่อง = วงได้ยินหด
        PlayerMovement pmv = PlayerTransform.GetComponent<PlayerMovement>();
        bool playerRunning = pmv != null && pmv.IsRunning;
        float hearNow = pmv != null && pmv.isSneaking ? hearingRadius * sneakHearingMultiplier : hearingRadius;

        if (clear)
        {
            // 1. ได้ยินคนวิ่ง (ไกล)
            if (playerRunning && distToPlayer <= runNoiseRadius) canSeeOrHear = true;
            // 2. ระยะใกล้ (วงกลมสีเหลือง)
            else if (distToPlayer <= hearNow) canSeeOrHear = true;
            // 3. สายตา (รูปพัดสีแดง)
            else if (distToPlayer <= sightRadius)
            {
                Vector3 dirToPlayer = PlayerTransform.position - transform.position; dirToPlayer.y = 0;
                Vector3 forward = transform.forward; forward.y = 0;
                if (Vector3.Angle(forward, dirToPlayer) <= sightAngle / 2f) canSeeOrHear = true;
            }
        }
        
        // อัปเดตลงหลอดเกจ (ถ้าเห็นให้เพิ่ม ถ้าไม่เห็นให้ลด)
        if (canSeeOrHear)
        {
            // [แก้โดย Claude 2026-11-11 งานข้อ 10] ยิ่งยืนใกล้ ยิ่งรู้ตัวไว — แม้อยู่ข้างหลัง (ไม่ต้องอยู่ในพัดสายตา)
            // ระยะประชิด (Instant Notice Radius) = รู้ตัวทันที · ในวงได้ยิน = เร็วขึ้นตามความใกล้ (ขอบวง x1 → ชิดตัว x Close Range Multiplier)
            if (distToPlayer <= instantNoticeRadius)
            {
                Awareness.ForceMaxAwareness();
            }
            else if (distToPlayer <= hearingRadius)
            {
                float closeness = 1f - Mathf.InverseLerp(instantNoticeRadius, hearingRadius, distToPlayer);
                Awareness.FillAwareness(Mathf.Lerp(1f, closeRangeMultiplier, closeness));
            }
            else
            {
                Awareness.FillAwareness();
            }
        }
        else
        {
            Awareness.DecayAwareness();
        }
    }

    // [Claude 2026-11-12] เช็คกำแพงคั่น — นับทั้ง Obstacle Mask + เลเยอร์ Default (กำแพงในแมพ 22 ชิ้นอยู่ Default → เดิมมองทะลุ)
    // ข้ามตัวเอง/ตัวผู้เล่น/Trigger
    public bool WallBetween(Vector3 from, Vector3 to)
    {
        int mask = obstacleMask.value | (1 << 0);
        Vector3 dir = to - from; float dist = dir.magnitude;
        if (dist < 0.01f) return false;
        foreach (RaycastHit h in Physics.RaycastAll(from, dir / dist, dist, mask, QueryTriggerInteraction.Ignore))
        {
            Transform r = h.collider.transform.root;
            if (r == transform.root || (PlayerTransform != null && r == PlayerTransform.root)) continue;
            if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;   // มอนตัวอื่นไม่นับเป็นกำแพง
            return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        Gizmos.color = new Color(1, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, sightRadius);

        Vector3 viewAngleA = new Vector3(Mathf.Sin((-sightAngle / 2f + transform.eulerAngles.y) * Mathf.Deg2Rad), 0, Mathf.Cos((-sightAngle / 2f + transform.eulerAngles.y) * Mathf.Deg2Rad));
        Vector3 viewAngleB = new Vector3(Mathf.Sin((sightAngle / 2f + transform.eulerAngles.y) * Mathf.Deg2Rad), 0, Mathf.Cos((sightAngle / 2f + transform.eulerAngles.y) * Mathf.Deg2Rad));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * sightRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * sightRadius);
        
        // วาดวงกลมบอกระยะ Leash (หมาเฝ้าบ้าน)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // สีส้มจางๆ
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(GetHomePosition(), chaseLeashDistance);
        }
    }
}
