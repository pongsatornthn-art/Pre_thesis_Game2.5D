using UnityEngine;
using UnityEngine.AI;

public class StalkerAI : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float searchSpeed = 2.5f;
    public float chaseSpeed = 5.0f;
    public float stunDuration = 2.5f;
    public bool isInvincible = true;
    public float killDistance = 2.0f;

    [Header("Super Armor (Boss)")]
    public bool hasSuperArmor = false;
    private Coroutine superArmorCoroutine;

    [Header("Audio")]
    public AudioClip screamClip;
    [Range(0f, 1f)] public float screamVolume = 1f;

    [Header("Director AI (การเดินซุ่มรอบๆ ผู้เล่น)")]
    [Tooltip("ระยะใกล้สุดที่บอสจะเดินวนรอบๆ ตัวผู้เล่น (เมตร)")]
    public float lurkMinRadius = 10f;
    [Tooltip("ระยะไกลสุดที่บอสจะเดินวนรอบๆ ตัวผู้เล่น (เมตร)")]
    public float lurkMaxRadius = 20f;
    [Tooltip("รัศมีการได้ยินเสียงมินเนี่ยนร้องเรียกบอส (เมตร)")]
    public float minionShoutHearRadius = 50f;
    [Tooltip("ค่าความสงสัยขั้นต่ำที่จะเพิ่มให้บอส (เมื่อมินเนี่ยนอยู่ไกลขอบรัศมีสุดๆ)")]
    public float minShoutSuspicion = 20f;
    [Tooltip("ค่าความสงสัยขั้นสูงที่จะเพิ่มให้บอส (เมื่อมินเนี่ยนอยู่ใกล้บอสมาก)")]
    public float maxShoutSuspicion = 100f;

    [Header("Jumpscare Minigame")]
    public float minigameDuration = 5f;
    public float baseHpDrainRate = 25f;
    public float drainReductionPerPress = 1.5f;
    public KeyCode requiredInputButton = KeyCode.E;
    public float postEscapeTeleportRadius = 15f;

    [Header("Sensors (Vision & Sound)")]
    public float sightRadius = 8f;
    public float sightAngle = 60f;
    public float soundSenseRadius = 18f; // ระยะได้ยินเสียงวิ่ง/ปืน
    public LayerMask obstacleMask;

    public NavMeshAgent Agent { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public AwarenessSystem Awareness { get; private set; }
    private IStalkerState currentState;

    [Header("Debug UI (ข้อความลอยบนหัว)")]
    public TMPro.TMP_Text debugStateText;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Awareness = GetComponent<AwarenessSystem>();
        if (Awareness == null) Awareness = gameObject.AddComponent<AwarenessSystem>();
        
        // Custom Awareness settings for Stalker based on GDD
        Awareness.fillRateBase = 80f; // LOS Detection Speed = 80%/s
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) PlayerTransform = player.transform;

        ChangeState(new StalkerStateLurk());
        
        // รับสัญญาณตอน Minion ตะโกนเรียก (Director AI system)
        PTSDMonsterAI.OnMinionShout += HandleMinionShout;
    }

    private void OnDestroy()
    {
        PTSDMonsterAI.OnMinionShout -= HandleMinionShout;
    }

    private void HandleMinionShout(Vector3 minionPos)
    {
        // เช็คระยะห่างว่ามินเนี่ยนที่ร้อง อยู่ในรัศมีการได้ยินหรือไม่
        float distance = Vector3.Distance(transform.position, minionPos);
        if (distance <= minionShoutHearRadius)
        {
            float distanceFactor = 1f - (distance / minionShoutHearRadius); 
            float suspicionLevel = Mathf.Lerp(minShoutSuspicion, maxShoutSuspicion, distanceFactor);
            
            Debug.Log($"🦇 Stalker ได้ยินเสียง! (ระยะ {distance:F1}m) ความสงสัย: {suspicionLevel:F0}% กำลังเดินไปสำรวจ...");
            
            // เพิ่ม AG สะสมไว้เผื่อหลอดเต็ม
            Awareness.AddAwareness(suspicionLevel);
            
            // ไม่ว่าความสงสัยจะเท่าไหร่ ก็ให้หันหน้าเดินไปสำรวจจุดนั้นเสมอ (แต่จะเดินช้าหรือเร็วขึ้นอยู่กับค่า suspicion)
            ChangeState(new StalkerStateAlert(minionPos, suspicionLevel));
        }
        else
        {
            Debug.Log($"🦇 Stalker ไม่ได้ยินเสียง Minion (ระยะ {distance:F1}m ไกลเกินรัศมี {minionShoutHearRadius}m)");
        }
    }

    private void Update()
    {
        currentState?.UpdateState(this);
    }

    public void ChangeState(IStalkerState newState)
    {
        currentState?.ExitState(this);
        currentState = newState;
        currentState?.EnterState(this);
    }

    public void UpdateDetection()
    {
        if (PlayerTransform == null) return;
        
        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        
        // 1. ตรวจจับด้วยสายตารูปกรวย (Cone Vision) -> เจอแล้วเกจพุ่ง 100% ทันที ตาม GDD
        if (distToPlayer <= sightRadius)
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
                    {
                        Awareness.ForceMaxAwareness();
                        return; // เจอตัวแล้ว จบเลย
                    }
                }
                else 
                {
                    Awareness.ForceMaxAwareness();
                    return;
                }
            }
        }
        
        // 2. ตรวจจับเสียงผู้เล่นวิ่ง (ถ้าไม่ได้ย่องเบา)
        PlayerMovement pm = PlayerTransform.GetComponent<PlayerMovement>();
        if (pm != null && !pm.isSneaking && pm.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > 1f)
        {
            if (distToPlayer <= soundSenseRadius)
            {
                Awareness.FillAwareness(); // เสียงวิ่งทำให้หลอดเกจขึ้น
                return;
            }
        }

        Awareness.DecayAwareness(); // ถ้าไม่เห็นไม่ได้ยิน เกจลด
    }

    // อัปเดตให้ตรงกับ Interface IDamageable ของเพื่อน
    public void TakeDamage(int damageAmount, float knockback, bool isHeavyAttack = false)
    {
        TakeDamage(damageAmount, isHeavyAttack); // เรียกใช้ฟังก์ชันเดิม
    }

    public void Die() { } // บอสอมตะ ไม่ตาย

    public void TakeDamage(int attackDamage, bool isHeavyAttack = false)
    {
        if (isInvincible)
        {
            if (hasSuperArmor)
            {
                Debug.Log("<color=magenta>🦇 Stalker มี Super Armor อยู่! ยิงซ้ำไม่สะทกสะท้าน!</color>");
            }
            else
            {
                Debug.Log("<color=purple>🦇 Stalker อมตะ! กระสุนทำอะไรไม่ได้ ทำได้แค่ชะงัก (Stun)</color>");
                ChangeState(new StalkerStateStun(stunDuration));
                
                // ให้ Super Armor 5 วินาทีหลังจากโดนยิง เพื่อไม่ให้ผู้เล่นยิงสแปมจนบอสเดินไม่ได้
                if (superArmorCoroutine != null) StopCoroutine(superArmorCoroutine);
                superArmorCoroutine = StartCoroutine(SuperArmorRoutine(5f));
            }
        }
    }

    private System.Collections.IEnumerator SuperArmorRoutine(float duration)
    {
        hasSuperArmor = true;
        ShowDebugText("SUPER ARMOR", Color.magenta);
        yield return new WaitForSeconds(duration);
        hasSuperArmor = false;
        ShowDebugText("", Color.white);
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

    private void OnDrawGizmos()
    {
        // วาดรูปพัดสายตา
        Vector3 forward = transform.forward * sightRadius;
        Quaternion leftRayRotation = Quaternion.AngleAxis(-sightAngle / 2f, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(sightAngle / 2f, Vector3.up);
        Vector3 leftRayDirection = leftRayRotation * forward;
        Vector3 rightRayDirection = rightRayRotation * forward;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftRayDirection);
        Gizmos.DrawRay(transform.position, rightRayDirection);

        // วาดวงกลมการได้ยินเสียงฝีเท้าผู้เล่น
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, soundSenseRadius);

        // วาดวงกลมรัศมีการได้ยินเสียงมินเนี่ยนร้องเรียก (วงใหญ่สุด)
        Gizmos.color = new Color(0.8f, 0f, 1f, 0.3f); // สีม่วงอ่อนๆ จะได้ไม่แย่งซีนวงอื่น
        Gizmos.DrawWireSphere(transform.position, minionShoutHearRadius);
    }
}
