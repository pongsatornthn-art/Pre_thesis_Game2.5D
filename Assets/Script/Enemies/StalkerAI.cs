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
    [Header("[Claude 2026-11-12] เสียงตอนไล่ (ต้าบอกขาด 2 อัน — ใส่ไฟล์แล้วทำงานเลย)")]
    [Tooltip("เสียงคำรามตอนเห็นผู้เล่นแล้วเริ่มไล่ (เล่นครั้งเดียวตอนเข้าช่วงคำราม)")]
    public AudioClip chaseRoarClip;
    [Tooltip("AudioSource บนตัวบอสที่ใส่คลิปเสียงวิ่ง (Loop) — เปิดเองตอนวิ่งไล่ ปิดเองตอนเลิกไล่ · เว้นว่างได้")]
    public AudioSource chaseRunLoop;

    [Header("Director AI (การเดินซุ่มรอบๆ ผู้เล่น)")]
    public float lurkMinRadius = 10f;
    public float lurkMaxRadius = 20f;
    public float minionShoutHearRadius = 50f;
    public float minShoutSuspicion = 20f;
    public float maxShoutSuspicion = 100f;

    [Header("Jumpscare Minigame")]
    public StalkerJumpscareUI jumpscareUI; // ลาก Canvas UI มินิเกมมาใส่ช่องนี้
    public int jumpscareDamage = 40; // ดาเมจที่โดนถ้าหลอดหมด
    public float baseHpDrainRate = 25f; // ความเร็วที่หลอดลดลง
    [Tooltip("ค่าความแรงในการคลิกแต่ละครั้ง (แนะนำ 10-15 สำหรับหลอด 100)")]
    public float drainReductionPerPress = 10f;
    public KeyCode requiredInputButton = KeyCode.E;
    public float postEscapeTeleportRadius = 15f;

    [Header("Sensors (Vision & Sound)")]
    public float sightRadius = 8f;
    public float sightAngle = 60f;
    public float soundSenseRadius = 18f;
    [Tooltip("[Claude 2026-11-12] วงในรอบตัว: ผู้เล่นเข้าใกล้กว่านี้ (เมตร) = รู้ตัวทันที ไม่สนทิศที่หัน (ยกเว้นมีกำแพงคั่น)")]
    public float instantNoticeRadius = 2f;
    public LayerMask obstacleMask;

    [Header("Jumpscare Minigame")]
    public float minigameDuration = 5f;

    public NavMeshAgent Agent { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public AwarenessSystem Awareness { get; private set; }
    private IStalkerState currentState;

    public IStalkerState CurrentState => currentState;

    [Header("Debug UI")]
    public TMPro.TMP_Text debugStateText;

    private bool isInMinigame = false;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Awareness = GetComponent<AwarenessSystem>();
        if (Awareness == null) Awareness = gameObject.AddComponent<AwarenessSystem>();
        Awareness.fillRateBase = 80f;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) PlayerTransform = player.transform;


        if (jumpscareUI == null)
            jumpscareUI = FindFirstObjectByType<StalkerJumpscareUI>();

        ChangeState(new StalkerStateLurk());

        // [คืนโดย Claude 2026-11-11 เจ้าของสั่ง] รับสัญญาณตอนมินเนียนตะโกนเรียก (Director AI) — ปอเคยคอมเมนต์ปิดไว้
        PTSDMonsterAI.OnMinionShout += HandleMinionShout;
    }

    private void OnDestroy()
    {
        PTSDMonsterAI.OnMinionShout -= HandleMinionShout;
    }

    private void HandleMinionShout(Vector3 minionPos)
    {
        if (isInMinigame) return;

        // เช็คระยะห่างว่ามินเนี่ยนที่ร้อง อยู่ในรัศมีการได้ยินหรือไม่
        float distance = Vector3.Distance(transform.position, minionPos);
        if (distance > minionShoutHearRadius) return;

        float distanceFactor = 1f - (distance / minionShoutHearRadius);
        float suspicionLevel = Mathf.Lerp(minShoutSuspicion, maxShoutSuspicion, distanceFactor);
        Debug.Log($"🦇 Stalker ได้ยินเสียง! (ระยะ {distance:F1}m) ความสงสัย: {suspicionLevel:F0}% กำลังเดินไปสำรวจ...");

        Awareness.AddAwareness(suspicionLevel);
        ChangeState(new StalkerStateAlert(minionPos, suspicionLevel));
    }

    private void Update()
    {
        if (isInMinigame) return; // ถ้ากำลังยื้อยุดอยู่ บอสจะหยุดทำงานชั่วคราว

        currentState?.UpdateState(this);
    }

    // ----- ระบบเข้าหน้ายื้อยุด -----
    /// <summary>
    /// [แก้โดย Claude 2026-11-11] จุดเดียวที่ "จับผู้เล่น" — StalkerStateChase เรียกเมื่อถึงระยะ killDistance
    /// เดิมมี 2 ทาง (เช็คระยะทุกเฟรมของปอ + Chase → StalkerStateKill กด E เดิม) → มินิเกมซ้อนกัน / จับตอนบอสยังไม่ได้ไล่
    /// ตอนนี้: มีหน้ามือผี (jumpscareUI) = ใช้ของปอ · ไม่มี = ใช้มินิเกมกด E เดิม (StalkerStateKill)
    /// </summary>
    public void CatchPlayer()
    {
        if (isInMinigame) return;
        if (jumpscareUI != null) StartJumpscare();
        else ChangeState(new StalkerStateKill());
    }

    private void StartJumpscare()
    {
        isInMinigame = true;
        Agent.isStopped = true; // หยุดเดิน

        Debug.Log("🦇 Stalker จับตัวผู้เล่นได้! เข้าสู่หน้า Jumpscare ยื้อยุด!");
        jumpscareUI.StartMinigame(this);

        // คุณพงศธรสามารถเรียกอนิเมเตอร์ "มือผี" ตรงนี้ได้เลยครับ
        // anim.SetTrigger("Jumpscare");
    }

    // รับผลลัพธ์หลังจากมินิเกมจบ
    public void OnJumpscareEnd(bool isWin)
    {
        isInMinigame = false;

        if (isWin)
        {
            Debug.Log("🎉 ผู้เล่นคลิกรัวชนะ! รอดตาย เลือดไม่ลด");
        }
        else
        {
            Debug.Log("💀 ผู้เล่นคลิกไม่ทัน หลอดหมด! โดนดาเมจเต็มๆ");
            // ดึง PlayerMovement ของผู้เล่นมาเพื่อหักเลือด
            PlayerMovement playerHealth = PlayerTransform.GetComponent<PlayerMovement>();
            if (playerHealth != null)
            {
                // ใช้ TakeRawDamage ตามสคริปต์ของ PlayerMovement
                playerHealth.TakeRawDamage(jumpscareDamage);
            }
        }

        Awareness.ForceZeroAwareness();   // [คืนโดย Claude] หลุดแล้วบอสลืมเรา ค่อยย่องตามใหม่ (แบบเดิม)
        TeleportAway();
        ChangeState(new StalkerStateLurk());
    }

    /// <summary>
    /// [แก้โดย Claude 2026-11-11] วาร์ปหนีไปรอบตัวผู้เล่น — คืนการเช็คแบบเดิม: จุดต้อง "เดินถึงได้จริง" (PathComplete)
    /// ไม่งั้นสุ่มได้จุดนอกบ้าน/อีกฝั่งกำแพงที่ไม่มีทางเชื่อม แล้วบอสค้างอยู่ตรงนั้น
    /// </summary>
    private void TeleportAway()
    {
        if (PlayerTransform == null) return;

        bool warped = false;
        for (int i = 0; i < 15 && !warped; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized * postEscapeTeleportRadius;
            Vector3 randomPos = PlayerTransform.position + new Vector3(randomDir.x, 0, randomDir.y);

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                Agent.CalculatePath(hit.position, path);
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    Agent.Warp(hit.position);
                    warped = true;
                }
            }
        }

        // หาจุดดีๆ ไม่ได้ → ถอยไปหลังผู้เล่นใกล้ๆ แทนการโยนออกนอกโลก
        if (!warped)
        {
            Vector3 backupPos = PlayerTransform.position - (PlayerTransform.forward * 5f);
            if (NavMesh.SamplePosition(backupPos, out NavMeshHit backupHit, 10f, NavMesh.AllAreas))
                Agent.Warp(backupHit.position);
        }

        Agent.isStopped = false;
    }

    public void ChangeState(IStalkerState newState)
    {
        if (isInMinigame) return; // ห้ามเปลี่ยนสถานะตอนโดนจับ
        currentState?.ExitState(this);
        currentState = newState;
        currentState?.EnterState(this);
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

    public void UpdateDetection()
    {
        if (PlayerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        // [Claude 2026-11-12 เพลย์เทส] วงในรอบตัว — เข้าใกล้กว่านี้ = รู้ตัวทันที แม้อยู่ข้างหลัง (เดิมเดินผ่านหลังติดตัวแล้วบอสไม่รู้ ดูโง่)
        // ยกเว้นมีกำแพงคั่น (อยู่คนละห้องแต่ใกล้ผนัง)
        if (distToPlayer <= instantNoticeRadius)
        {
            Vector3 o = transform.position + Vector3.up * 1f;
            Vector3 t = PlayerTransform.position + Vector3.up * 1f;
            if (!WallBetween(o, t))
            {
                Awareness.ForceMaxAwareness();
                return;
            }
        }

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
                if (!WallBetween(origin, target))   // [Claude 2026-11-12] นับกำแพงเลเยอร์ Default ด้วย
                {
                    Awareness.ForceMaxAwareness();
                    return;
                }
            }
        }

        PlayerMovement pm = PlayerTransform.GetComponent<PlayerMovement>();
        // [Claude 2026-11-12] วิ่ง = ได้ยินเต็มรัศมี · เดิน = 40% ของรัศมี · ย่อง = ไม่ได้ยิน
        if (pm != null && !pm.isSneaking && pm.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > 1f)
        {
            float hearRadius = pm.IsRunning ? soundSenseRadius : soundSenseRadius * 0.4f;
            // [Claude 2026-11-12] กำแพงคั่น = ได้ยินแค่ครึ่งระยะ (เสียงลอดได้บ้าง แต่ไม่ใช่รู้ทะลุทั้งบ้าน)
            if (distToPlayer <= hearRadius && WallBetween(transform.position + Vector3.up, PlayerTransform.position + Vector3.up)) hearRadius *= 0.5f;
            if (distToPlayer <= hearRadius)
            {
                Awareness.FillAwareness();
                return;
            }
        }

        Awareness.DecayAwareness();
    }

    public void TakeDamage(int damageAmount, float knockback, bool isHeavyAttack = false)
    {
        TakeDamage(damageAmount, isHeavyAttack);
    }

    public void Die() { }

    public void TakeDamage(int attackDamage, bool isHeavyAttack = false)
    {
        if (isInvincible)
        {
            if (hasSuperArmor)
            {
                Debug.Log("<color=magenta>🦇 Stalker มี Super Armor อยู่!</color>");
            }
            else
            {
                Debug.Log("<color=purple>🦇 Stalker อมตะ! โดน Stun</color>");
                // [คืนโดย Claude 2026-11-11] ยิง/ตีบอสแล้วบอสชะงัก (StalkerStateStun มีอยู่แล้ว) — ช่วงยื้อยุด ChangeState ถูกกันไว้อยู่แล้ว
                ChangeState(new StalkerStateStun(stunDuration));

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
            // [คืนโดย Claude] key เป็นรหัสข้อความ → แปลผ่านระบบภาษา (ไม่มีในตาราง = โชว์รหัสตรงๆ เหมือนเดิม)
            if (string.IsNullOrEmpty(key)) debugStateText.text = "";
            else
            {
                ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
                debugStateText.text = loc != null ? loc.GetText(key) : key;
            }
            debugStateText.color = color;
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 forward = transform.forward * sightRadius;
        Quaternion leftRayRotation = Quaternion.AngleAxis(-sightAngle / 2f, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(sightAngle / 2f, Vector3.up);
        Vector3 leftRayDirection = leftRayRotation * forward;
        Vector3 rightRayDirection = rightRayRotation * forward;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftRayDirection);
        Gizmos.DrawRay(transform.position, rightRayDirection);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, soundSenseRadius);

        Gizmos.color = new Color(0.8f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, minionShoutHearRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, killDistance); // วาดรัศมีการโดนจับ
    }
}