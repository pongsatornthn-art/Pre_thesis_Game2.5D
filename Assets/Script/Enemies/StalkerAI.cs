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
        // PTSDMonsterAI.OnMinionShout += HandleMinionShout;
    }

    private void Update()
    {
        if (isInMinigame) return; // ถ้ากำลังยื้อยุดอยู่ บอสจะหยุดทำงานชั่วคราว

        currentState?.UpdateState(this);
        CheckJumpscareDistance(); // เช็กระยะว่าบอสถึงตัวผู้เล่นหรือยัง
    }

    // ----- ระบบเข้าหน้ายื้อยุด -----
    private void CheckJumpscareDistance()
    {
        if (PlayerTransform == null || isInMinigame) return;

        // ถ้าผู้เล่นอยู่ในระยะฆ่า (killDistance) ให้เข้ามินิเกม
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);
        if (dist <= killDistance && jumpscareUI != null)
        {
            StartJumpscare();
        }
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

        TeleportAway();
        ChangeState(new StalkerStateLurk());
    }

    private void TeleportAway()
    {
        if (PlayerTransform == null) return;

        // สุ่มหาตำแหน่งด้านหลังหรือรอบๆ ผู้เล่นที่อยู่ไกลออกไป
        Vector2 randomDir = Random.insideUnitCircle.normalized * postEscapeTeleportRadius;
        Vector3 randomPos = PlayerTransform.position + new Vector3(randomDir.x, 0, randomDir.y);

        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            Agent.Warp(hit.position);
            Agent.isStopped = false;
        }
    }

    public void ChangeState(IStalkerState newState)
    {
        if (isInMinigame) return; // ห้ามเปลี่ยนสถานะตอนโดนจับ
        currentState?.ExitState(this);
        currentState = newState;
        currentState?.EnterState(this);
    }

    public void UpdateDetection()
    {
        if (PlayerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

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
                        return;
                    }
                }
                else
                {
                    Awareness.ForceMaxAwareness();
                    return;
                }
            }
        }

        PlayerMovement pm = PlayerTransform.GetComponent<PlayerMovement>();
        if (pm != null && !pm.isSneaking && pm.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > 1f)
        {
            if (distToPlayer <= soundSenseRadius)
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
                // ChangeState(new StalkerStateStun(stunDuration)); // ปลดคอมเมนต์ถ้ามีสคริปต์นี้

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
            debugStateText.text = key;
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