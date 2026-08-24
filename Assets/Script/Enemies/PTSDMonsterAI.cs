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
    public float sightRadius = 15f; 
    [Range(0, 360)] public float sightAngle = 90f; 
    public LayerMask obstacleMask; 
    public LayerMask playerMask; 

    [Header("Combat Settings")]
    public float attackDistance = 1.5f; 
    public int attackDamage = 20; 
    public float attackCooldown = 2f;
    public int maxHealth = 100;
    private int currentHealth;

    // อ่านค่าได้อย่างเดียวให้คนอื่นดึงไปโชว์
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    // 🌟 [SOLID: SRP] สร้าง Event ส่งสัญญาณออกไปเมื่อเลือดลด
    public event System.Action<int, int> OnHealthChanged;

    [Header("Audio Settings (AAA)")]
    public AudioClip hitSound;
    public AudioClip dieSound;

    [Header("Movement Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;
    [Tooltip("ระยะทางไกลสุดที่มันจะวิ่งไล่ตามออกจากจุดเกิด/โซน ถ้าเกินนี้จะเลิกตาม (หมาเฝ้าบ้าน)")]
    public float chaseLeashDistance = 20f;

    // --- State Machine ---
    private IMonsterState currentState;
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
    public void TakeDamage(int damageAmount, float knockback)
    {
        TakeDamage(damageAmount);
        // (ส่วนกระเด็น knockback ค่อยทำเพิ่มทีหลังถ้าต้องการ)
    }

    public void TakeDamage(int damage)
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
            Debug.Log($"<color=red>{gameObject.name} โดนโจมตี {damage} ดาเมจ! ติดสตัน!</color>");
            ChangeState(new StateStun(1.0f)); 
            
            // 🎵 [AAA Audio Service] เล่นเสียงโดนตีแบบ 3D
            if (hitSound != null)
                ServiceLocator.Get<IAudioService>()?.PlaySFX(hitSound, transform.position);
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
        
        // 1. ตรวจจับระยะใกล้ (วงกลมสีเหลือง) - ไม่ทะลุกำแพง
        if (distToPlayer <= hearingRadius)
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 target = PlayerTransform.position + Vector3.up * 1f;
            Vector3 rayDir = (target - origin).normalized;
            
            if (Physics.Raycast(origin, rayDir, out RaycastHit hit, hearingRadius, obstacleMask))
            {
                if (hit.collider.transform == PlayerTransform || hit.collider.transform.root == PlayerTransform.root) 
                    canSeeOrHear = true;
            }
            else canSeeOrHear = true; // ทางโล่ง
        }

        // 2. ตรวจจับด้วยสายตา (รูปพัดสีแดง)
        if (!canSeeOrHear && distToPlayer <= sightRadius)
        {
            Vector3 dirToPlayer = (PlayerTransform.position - transform.position);
            dirToPlayer.y = 0; 
            Vector3 forward = transform.forward;
            forward.y = 0;

            if (Vector3.Angle(forward, dirToPlayer) <= sightAngle / 2f)
            {
                Vector3 origin = transform.position + Vector3.up * 1f;
                Vector3 target = PlayerTransform.position + Vector3.up * 1f;
                Vector3 rayDir = (target - origin).normalized;

                if (Physics.Raycast(origin, rayDir, out RaycastHit hit, distToPlayer, obstacleMask))
                {
                    if (hit.collider.transform == PlayerTransform || hit.collider.transform.root == PlayerTransform.root)
                        canSeeOrHear = true;
                }
                else canSeeOrHear = true;
            }
        }
        
        // อัปเดตลงหลอดเกจ (ถ้าเห็นให้เพิ่ม ถ้าไม่เห็นให้ลด)
        if (canSeeOrHear)
        {
            Awareness.FillAwareness();
        }
        else
        {
            Awareness.DecayAwareness();
        }
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
