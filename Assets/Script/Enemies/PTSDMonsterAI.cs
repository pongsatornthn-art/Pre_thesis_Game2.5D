using UnityEngine;
using UnityEngine.AI;

public class PTSDMonsterAI : MonoBehaviour
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
    private PlayerMovement playerScript;
    private Vector3 spawnPosition; // จำจุดเกิดของตัวเองไว้เผื่อต้องเดินกลับ

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        spawnPosition = transform.position; // บันทึกจุดที่เกิดมาครั้งแรก
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
            
        if (defaultBehavior == PatrolBehaviorType.FollowRoute && assignedRoute != null)
            return assignedRoute.transform.position;
            
        return spawnPosition; // ถ้ายืนนิ่งๆ ก็ยึดจุดเกิดเป็นบ้าน
    }

    public void AttackPlayer()
    {
        Debug.Log($"<color=orange>{gameObject.name} โจมตีผู้เล่น {attackDamage} ดาเมจ!</color>");
        if (playerScript != null) playerScript.TakeDamage(attackDamage);
    }

    /// <summary>
    /// เช็คว่าเจอผู้เล่นไหม (แยกเป็น public เพื่อให้ State เรียกใช้ได้)
    /// </summary>
    public bool CheckDetection()
    {
        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        
        // 1. ตรวจจับด้วยเสียง (วงกลมสีเหลือง - รอบตัวทะลุกำแพง)
        if (distToPlayer <= hearingRadius) return true; 

        // 2. ตรวจจับด้วยสายตา (รูปพัดสีแดง - ต้องไม่โดนบัง)
        if (distToPlayer <= sightRadius)
        {
            Vector3 dirToPlayer = (PlayerTransform.position - transform.position);
            dirToPlayer.y = 0; 
            Vector3 forward = transform.forward;
            forward.y = 0;

            // เช็คว่าผู้เล่นอยู่ใน "กรวยสายตา" หรือไม่ (ระหว่างเส้นสีแดง)
            if (Vector3.Angle(forward, dirToPlayer) <= sightAngle / 2f)
            {
                // ยกจุดยิงเลเซอร์ขึ้นมาที่หน้าอก กันยิงติดขอบพื้น
                Vector3 origin = transform.position + Vector3.up * 1f;
                Vector3 target = PlayerTransform.position + Vector3.up * 1f;
                Vector3 rayDir = (target - origin).normalized;

                // ยิงเลเซอร์เช็คกำแพง
                if (Physics.Raycast(origin, rayDir, out RaycastHit hit, distToPlayer, obstacleMask))
                {
                    // ถ้าเลเซอร์ชนโดนของที่อยู่ใน ObstacleMask
                    // เช็คให้ชัวร์ว่าสิ่งที่ชน ดันเป็นตัวผู้เล่นเองหรือเปล่า? 
                    // (แก้บั๊กเผื่อ Level Designer เอาผู้เล่นไปไว้ในเลเยอร์ Default เดียวกับกำแพง)
                    if (hit.collider.transform == PlayerTransform || hit.collider.transform.root == PlayerTransform.root)
                    {
                        return true; // มองเห็นผู้เล่นเต็มๆ!
                    }
                    return false; // โดนกำแพงบัง
                }
                
                // ไม่ชนอะไรเลย = ทางโล่ง มองเห็นแน่นอน
                return true;
            }
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
