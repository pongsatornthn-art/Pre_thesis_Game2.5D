using UnityEngine;

/// <summary>
/// [Claude 2026-11-26] เล่นภาพเคลื่อนไหวของผู้เล่นจาก SpriteAnimSet — แบบเดียวกับมอนสเตอร์
/// ไม่ต้องต่อเส้น/ตั้ง Transition/Blend Tree ใน Animator อีก: ใส่ชุดภาพ → ใช้ได้เลย
///
/// วิธีใช้: Tools ▸ Player Sprites ▸ Build Player Anim Set (สร้างชุดภาพ) → แปะสคริปต์นี้ที่ GFX ของ Player → ลากชุดภาพใส่
///
/// เลือกท่าเองจากสถานะในเกม (ไม่อ่านกล่องใน Animator):
///   อาวุธ  : ปืน → *_Gun · ท่อ/อาวุธประชิด → *_Pipe · มือเปล่า → ปกติ
///   การขยับ: ยืน → Idle · เดิน → Walk · วิ่ง (Shift) → Run
///   ยิง    : คลิกซ้ายค้างตอนถือปืน → Shoot
///   ตีท่อ  : ระหว่างท่าตีของ PlayerCombat → Attack_Pipe (เล่นรอบเดียว)
///   ไม่มีรูปท่าไหน → ใช้ท่าใกล้เคียงแทนอัตโนมัติ (เช่น Run_Gun → Walk_Gun → Idle_Gun → Idle)
/// เลือกทิศเอง: ถือปืน = หันตามเมาส์ · ไม่ถือปืน = หันตามทางที่เดิน
///
/// Animator เดิมยังอยู่ได้ (ใช้ตรวจท่าตี) — สคริปต์นี้ทำงานทีหลังสุดแล้ววางภาพทับ เลยไม่ตีกัน
/// ปิดสคริปต์นี้ = กลับไปใช้ Animator แบบเดิมทันที
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Tooltip("ชุดภาพผู้เล่น (สร้างจาก Tools ▸ Player Sprites ▸ Build Player Anim Set)")]
    public SpriteAnimSet animSet;

    [Header("ความเร็ว")]
    [Min(0.05f)] public float playSpeed = 1f;
    [Tooltip("เร็วกว่านี้ (ม./วิ) = นับว่าเดิน")]
    [Min(0f)] public float moveThreshold = 0.2f;
    [Tooltip("เดินถอยหลังตอนเล็งปืน → เล่นภาพย้อนกลับ (เท้าไม่ไถล)")]
    public bool reverseWhenBackpedal = true;

    [Header("การหัน")]
    [Range(0f, 30f)] public float directionHysteresis = 10f;

    [Header("ชื่อสถานะท่าตีใน Animator (ใช้ตรวจว่ากำลังตีอยู่)")]
    public string[] attackStateNames = { "Test_Attack", "Heavy_Attack" };

    [Header("Debug")]
    [SerializeField] private string currentAction;
    [SerializeField] private SpriteAnimSet.Dir currentDir;

    private SpriteRenderer sr;
    private Animator animator;
    private PlayerMovement movement;
    private Rigidbody body;
    private Vector3 lastPos;
    private Vector3 velocity;
    private Vector3 facing = Vector3.back;

    private SpriteAnimSet.Clip clip;
    private float clipTime;
    private bool attackDone;
    private bool wasAttacking;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<PlayerMovement>();
        body = movement != null ? movement.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
        lastPos = Root.position;
    }

    private Transform Root => movement != null ? movement.transform : transform;

    private void OnEnable()
    {
        lastPos = Root.position;
        velocity = Vector3.zero;
        clip = null;
    }

    private void LateUpdate()
    {
        if (animSet == null || sr == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // เกมหยุด (Pause) → ภาพค้าง

        // 1) ความเร็วจริงแนวราบ
        Vector3 v = body != null ? body.linearVelocity : (Root.position - lastPos) / dt;
        lastPos = Root.position;
        v.y = 0f;
        velocity = Vector3.Lerp(velocity, v, 1f - Mathf.Exp(-dt * 15f));
        bool moving = velocity.magnitude > moveThreshold;
        bool running = moving && movement != null && movement.IsRunning;

        // 2) อาวุธ
        ItemType? equipped = Inventory.Instance != null && Inventory.Instance.currentEquippedItem != null
            ? Inventory.Instance.currentEquippedItem.itemType : (ItemType?)null;
        bool gun = equipped == ItemType.RangedWeapon;
        bool pipe = equipped == ItemType.MeleeWeapon;

        // 3) ยิง / ตี
        bool shooting = gun && Input.GetMouseButton(0) && !GameplayBlocked();
        bool attacking = IsAttacking();
        if (attacking && !wasAttacking) attackDone = false;
        wasAttacking = attacking;

        // 4) ทิศ: ถือปืน = ตามเมาส์ (ค่าเดียวกับที่ PlayerMovement คำนวณ) · ไม่ถือ = ตามทางที่เดิน
        Vector3 aim = AimDirection();
        if ((gun || attacking) && aim.sqrMagnitude > 0.001f) facing = aim;
        else if (moving) facing = velocity;

        // 5) ท่า
        string move = running ? "Run" : moving ? "Walk" : "Idle";
        string action;
        if (attacking && !attackDone) action = "Attack_Pipe";
        else if (shooting) action = "Shoot";
        else if (gun) action = move + "_Gun";
        else if (pipe) action = move + "_Pipe";
        else action = move;
        action = Resolve(action);
        if (action == null) return;

        // 6) เลือกคลิปตามมุม
        SpriteAnimSet.Clip next = animSet.FindByAngle(action, FacingAngle(facing), clip, directionHysteresis);
        if (next == null) return;
        if (next != clip)
        {
            bool sameAction = clip != null && string.Equals(clip.action, next.action, System.StringComparison.OrdinalIgnoreCase);
            if (!sameAction) clipTime = 0f;  // ท่าใหม่เริ่มเฟรมแรก · แค่เปลี่ยนทิศ = เล่นต่อ ไม่สะดุด
            clip = next;
        }
        currentAction = clip.action;
        currentDir = clip.dir;

        // 7) เดินเวลา (ถอยหลังขณะเล็ง = ย้อนภาพ)
        float rate = playSpeed;
        if (reverseWhenBackpedal && gun && moving && Vector3.Dot(velocity.normalized, facing.normalized) < -0.3f) rate = -rate;
        clipTime += dt * rate;

        int count = clip.frames.Length;
        float len = count / Mathf.Max(clip.fps, 0.01f);
        int frame;
        bool oneShot = !clip.loop || action == "Attack_Pipe";
        if (oneShot)
        {
            frame = Mathf.FloorToInt(Mathf.Max(0f, clipTime) * clip.fps);
            if (frame >= count)
            {
                frame = count - 1;
                if (action == "Attack_Pipe") attackDone = true;
            }
        }
        else
        {
            float t = Mathf.Repeat(clipTime, len);
            frame = Mathf.FloorToInt(t * clip.fps) % count;
        }

        Sprite s = clip.frames[Mathf.Clamp(frame, 0, count - 1)];
        if (s != null && sr.sprite != s) sr.sprite = s;
        if (sr.flipX) sr.flipX = false;
    }

    /// <summary>หาท่าที่มีรูปจริง — ไม่มีก็ลดลงเป็นท่าใกล้เคียง</summary>
    private string Resolve(string action)
    {
        string a = action;
        for (int guard = 0; guard < 8 && a != null; guard++)
        {
            if (animSet.HasAction(a)) return a;
            a = Fallback(a);
        }
        return null;
    }

    private static string Fallback(string a)
    {
        switch (a)
        {
            case "Attack_Pipe": return "Idle_Pipe";
            case "Shoot": return "Idle_Gun";
            case "Run_Gun": return "Walk_Gun";
            case "Walk_Gun": return "Idle_Gun";
            case "Idle_Gun": return "Idle";
            case "Run_Pipe": return "Walk_Pipe";
            case "Walk_Pipe": return "Walk";
            case "Idle_Pipe": return "Idle";
            case "Run": return "Walk";
            case "Walk": return "Idle";
            default: return null;
        }
    }

    private bool IsAttacking()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || attackStateNames == null) return false;
        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string n in attackStateNames) if (!string.IsNullOrEmpty(n) && now.IsName(n)) return true;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nx = animator.GetNextAnimatorStateInfo(0);
            foreach (string n in attackStateNames) if (!string.IsNullOrEmpty(n) && nx.IsName(n)) return true;
        }
        return false;
    }

    /// <summary>ทิศเล็ง (โลก) — PlayerMovement เขียนลง Animator AimX/AimZ ทุกเฟรมอยู่แล้ว</summary>
    private Vector3 AimDirection()
    {
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
        {
            Vector3 a = new Vector3(animator.GetFloat("AimX"), 0f, animator.GetFloat("AimZ"));
            if (a.sqrMagnitude > 0.0001f) return a;
        }
        return Vector3.zero;
    }

    private static bool GameplayBlocked()
    {
        return Time.timeScale == 0f;
    }

    /// <summary>มุมที่หัน เทียบกล้อง: 0 = หันหลังให้กล้อง (Back) · 90 = ขวาจอ · 180 = หันหน้าเข้ากล้อง · 270 = ซ้ายจอ</summary>
    private static float FacingAngle(Vector3 dir)
    {
        Camera cam = Camera.main;
        Vector3 fwd = Vector3.forward;
        if (cam != null)
        {
            fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) { fwd = cam.transform.up; fwd.y = 0f; }
        }
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        float a = Vector3.SignedAngle(fwd.normalized, dir, Vector3.up);
        return a < 0f ? a + 360f : a;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animSet == null || Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || animSet == null) return;
            SpriteRenderer r = GetComponent<SpriteRenderer>();
            SpriteAnimSet.Clip c = animSet.Find("Idle", SpriteAnimSet.Dir.Front) ?? (animSet.clips.Count > 0 ? animSet.clips[0] : null);
            if (r != null && c != null && c.frames.Length > 0 && c.frames[0] != null && r.sprite != c.frames[0])
            {
                UnityEditor.Undo.RecordObject(r, "Preview player sprite");
                r.sprite = c.frames[0];
            }
        };
    }
#endif
}
