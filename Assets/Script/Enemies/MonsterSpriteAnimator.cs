using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [2026-11-22] เล่นภาพเคลื่อนไหวของมอนสเตอร์ (Stalker / มินเนียน) จาก SpriteAnimSet — ไม่ต้องใช้ Animator Controller
///
/// วิธีใช้: แปะที่ GameObject ที่มี SpriteRenderer ของมอน (มินเนียน = GFX · Stalker ใน prefab = StalkerAI)
///         แล้วลากไฟล์ชุดภาพ (…_AnimSet) ใส่ช่อง Anim Set — เท่านี้
///
/// เลือกท่าเอง:
///   Attack = AI อยู่ในท่าตี/ตะครุบ/จับ (เล่นรอบเดียวตอนเริ่ม แล้วกลับเป็น Idle จนกว่าจะออกจากท่า)
///   Run    = เคลื่อนที่เร็ว (มินเนียน: เร็วเกินครึ่งทางระหว่าง Patrol Speed กับ Chase Speed)
///   Walk   = เคลื่อนที่ช้า · Idle = ยืนนิ่ง
///   ไม่มีรูปท่าไหน → ใช้ท่าที่ใกล้เคียงแทน (Run → Walk → Idle · Attack → Idle) — Stalker ที่มีแค่ Idle ก็ใช้ได้เลย
/// เลือกทิศเอง (เทียบกับกล้อง): เดินอยู่ = ทิศที่เดิน · ตีอยู่ = หันหาผู้เล่น · หยุด = หันทิศเดิม
///   ไม่มีรูปทิศนั้น → ใช้ทิศที่ใกล้ที่สุด (เช่น Stalker มีแค่ซ้าย/ขวา)
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)] // ทำงานหลังสคริปต์อื่น (เช่น FirstChaseStalker ที่พลิกภาพ) → ภาพสุดท้ายเป็นของเรา
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterSpriteAnimator : MonoBehaviour
{
    [Tooltip("ชุดภาพ (สร้างจาก Tools ▸ Monster Sprites ▸ Build Anim Set)")]
    public SpriteAnimSet animSet;

    [Header("ความเร็ว")]
    [Tooltip("คูณความเร็วเล่นภาพทุกท่า (1 = ตาม FPS ในชุดภาพ)")]
    [Min(0.05f)] public float playSpeed = 1f;
    [Tooltip("เร็วกว่านี้ (ม./วิ) = นับว่าเดิน")]
    [Min(0f)] public float moveThreshold = 0.15f;
    [Tooltip("เร็วกว่านี้ = นับว่าวิ่ง · 0 = คิดเอง (มินเนียน: กึ่งกลาง Patrol Speed–Chase Speed · อื่นๆ: 3.5)")]
    [Min(0f)] public float runThreshold = 0f;
    [Tooltip("เดินภาพตามความเร็วจริง (เดินช้า = ภาพขยับช้า) — กันอาการเท้าไถล")]
    public bool scaleWalkWithSpeed = true;

    [Header("การหัน")]
    [Tooltip("องศาที่ต้องเลยเส้นแบ่ง ก่อนจะยอมเปลี่ยนรูปทิศ (กันภาพสลับไปมาตอนเดินเฉียง)")]
    [Range(0f, 30f)] public float directionHysteresis = 12f;
    [Tooltip("ปิดการพลิกภาพ (flipX) จากสคริปต์อื่น — ชุดภาพมีรูปซ้าย/ขวาแยกกันอยู่แล้ว")]
    public bool forceNoFlip = true;

    [Header("Debug")]
    [SerializeField] private string currentAction;
    [SerializeField] private SpriteAnimSet.Dir currentDir;

    private SpriteRenderer sr;
    private NavMeshAgent agent;
    private PTSDMonsterAI minion;
    private StalkerAI stalker;
    private Transform motion;
    private Vector3 lastPos;
    private Vector3 velocity;
    private Vector3 facing = Vector3.back;

    private SpriteAnimSet.Clip clip;
    private float clipTime;
    private bool attackOneShotDone;
    private bool wasAttacking;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        agent = GetComponentInParent<NavMeshAgent>();
        minion = GetComponentInParent<PTSDMonsterAI>();
        stalker = GetComponentInParent<StalkerAI>();
        // วัดความเร็วจากตัวที่เดินจริง (ตัวแม่ที่มี NavMeshAgent) — ไม่ใช่ตัวภาพ ซึ่งอาจโดนสั่น/ขยับเล็กๆ ตอนกรีดร้อง
        motion = agent != null ? agent.transform : transform;
        lastPos = motion.position;
    }

    private void OnEnable()
    {
        if (motion != null) lastPos = motion.position;
        velocity = Vector3.zero;
        clip = null;
    }

    private void LateUpdate()
    {
        if (animSet == null || sr == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1) ความเร็ว (แนวราบ)
        Vector3 v = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity : (motion.position - lastPos) / dt;
        lastPos = motion.position;
        v.y = 0f;
        velocity = Vector3.Lerp(velocity, v, 1f - Mathf.Exp(-dt * 12f));
        float speed = velocity.magnitude;

        // 2) ท่า
        string state = minion != null ? minion.CurrentStateName
                     : stalker != null && stalker.CurrentState != null ? stalker.CurrentState.GetType().Name : "";
        bool attacking = state == "StateAttack" || state == "StateGrabScream" || state == "StalkerStateKill";
        if (attacking && !wasAttacking) attackOneShotDone = false; // เพิ่งเข้าท่าตี → เล่นท่าตีใหม่
        wasAttacking = attacking;

        // 3) ทิศ
        Transform target = minion != null ? minion.PlayerTransform : stalker != null ? stalker.PlayerTransform : null;
        if (attacking && target != null)
        {
            Vector3 toTarget = target.position - motion.position; toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f) facing = toTarget;
        }
        else if (speed > moveThreshold)
        {
            facing = velocity;
        }

        string action;
        if (attacking && !attackOneShotDone && animSet.HasAction("Attack")) action = "Attack";
        else if (speed > RunThreshold()) action = "Run";
        else if (speed > moveThreshold) action = "Walk";
        else action = "Idle";

        // ไม่มีรูปท่านี้ → ใช้ท่าใกล้เคียง
        if (!animSet.HasAction(action))
        {
            if (action == "Run" && animSet.HasAction("Walk")) action = "Walk";
            else action = "Idle";
        }

        // 4) เลือกคลิปตามมุมที่หัน (เทียบกล้อง)
        float angle = FacingAngle(facing);
        SpriteAnimSet.Clip next = animSet.FindByAngle(action, angle, clip, directionHysteresis);
        if (next == null) return;

        if (next != clip)
        {
            // ท่าเดิม แค่เปลี่ยนทิศ → เล่นต่อจากเวลาเดิม ภาพไม่สะดุด · ท่าใหม่ → เริ่มเฟรมแรก
            bool sameAction = clip != null && string.Equals(clip.action, next.action, System.StringComparison.OrdinalIgnoreCase);
            if (!sameAction) clipTime = 0f;
            clip = next;
        }
        currentAction = clip.action;
        currentDir = clip.dir;

        // 5) เดินเวลา
        float rate = playSpeed;
        if (scaleWalkWithSpeed && (action == "Walk" || action == "Run"))
        {
            float refSpeed = action == "Run" ? Mathf.Max(RunThreshold() * 1.4f, 0.01f) : Mathf.Max(RunThreshold() * 0.6f, 0.01f);
            rate *= Mathf.Clamp(speed / refSpeed, 0.5f, 1.6f);
        }
        clipTime += dt * rate;

        int count = clip.frames.Length;
        int frame = Mathf.FloorToInt(clipTime * Mathf.Max(clip.fps, 0.01f));
        if (clip.loop || action != "Attack")
        {
            frame %= count;
        }
        else if (frame >= count)
        {
            frame = count - 1;
            attackOneShotDone = true; // จบท่าตีแล้ว → เฟรมหน้าจะกลับเป็น Idle (จนกว่าจะเข้าท่าตีรอบใหม่)
        }

        Sprite s = clip.frames[Mathf.Clamp(frame, 0, count - 1)];
        if (s != null && sr.sprite != s) sr.sprite = s;
        if (forceNoFlip && sr.flipX) sr.flipX = false;
    }

    private float RunThreshold()
    {
        if (runThreshold > 0f) return runThreshold;
        if (minion != null) return (minion.patrolSpeed + minion.chaseSpeed) * 0.5f;
        return 3.5f;
    }

    /// <summary>มุมที่หัน เทียบกับกล้อง: 0 = หันหลังให้กล้อง (Back) · 90 = ขวาจอ · 180 = หันหน้าเข้ากล้อง (Front) · 270 = ซ้ายจอ</summary>
    private static float FacingAngle(Vector3 dir)
    {
        Camera cam = Camera.main;
        Vector3 fwd = Vector3.forward;
        if (cam != null)
        {
            fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) { fwd = cam.transform.up; fwd.y = 0f; } // กล้องมองตรงลงพื้น → ใช้ทิศบนจอแทน
        }
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        float a = Vector3.SignedAngle(fwd.normalized, dir, Vector3.up);
        return a < 0f ? a + 360f : a;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ใส่ชุดภาพแล้ว → โชว์เฟรมแรกของ Idle ในฉากเลย (ไม่ต้องกด Play ก็เห็นหน้าตา)
        if (animSet == null || Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || animSet == null) return; // ถูกลบไปแล้ว
            SpriteRenderer r = GetComponent<SpriteRenderer>();
            SpriteAnimSet.Clip c = animSet.Find("Idle", SpriteAnimSet.Dir.Front) ?? (animSet.clips.Count > 0 ? animSet.clips[0] : null);
            if (r != null && c != null && c.frames.Length > 0 && c.frames[0] != null && r.sprite != c.frames[0])
            {
                UnityEditor.Undo.RecordObject(r, "Preview monster sprite");
                r.sprite = c.frames[0];
            }
        };
    }
#endif
}
