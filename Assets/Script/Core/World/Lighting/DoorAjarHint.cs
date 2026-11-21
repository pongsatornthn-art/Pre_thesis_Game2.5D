using UnityEngine;

/// <summary>
/// [ข้อ 11] ประตูแง้ม — ในโลก PTSD บานประตูแง้มออกช้าๆ (+ เสียงเอี๊ยด + ไฟลอดช่อง)
///   RandomInPtsd     = แง้มเองเป็นพักๆ แบบสุ่ม แล้วค่อยปิด (บรรยากาศหลอน) — ค่าเริ่ม ใส่ได้หลายบาน
///   PlayerApproaches = แง้มเมื่อผู้เล่นเดินเข้าใกล้ (ใช้บอกทางไปต่อ)
///
/// แปะที่ประตู (ตัวที่มี DoorController เช่น Door00) — ไม่ต้องแก้ DoorController/Animator
/// หมุน "บานลูก" (เช่น Door 1) รอบจุดบานพับของประตู (ตำแหน่งตัวแม่) → Animator เปิด/ปิดเดิมที่หมุนตัวแม่ไม่ตีกัน
/// ผู้เล่นกด E เปิดประตูจริง → บานกลับที่เดิมทันที แล้ว Animator เดิมเปิดให้ตามปกติ
/// </summary>
public class DoorAjarHint : MonoBehaviour
{
    public enum TriggerMode { RandomInPtsd, PlayerApproaches }

    [Header("โหมด")]
    public TriggerMode mode = TriggerMode.RandomInPtsd;

    [Header("สุ่มแง้ม (RandomInPtsd)")]
    [Tooltip("เว้นช่วงระหว่างแง้มแต่ละครั้ง (วินาที ต่ำสุด–สูงสุด)")]
    public Vector2 randomInterval = new Vector2(10f, 30f);
    [Tooltip("แง้มค้างนานกี่วินาที (ต่ำสุด–สูงสุด)")]
    public Vector2 randomDuration = new Vector2(2.5f, 6f);
    [Tooltip("แง้มเฉพาะตอนผู้เล่นอยู่ในระยะนี้ (เมตร) — ให้เห็น/ได้ยิน · 0 = ทุกที่")]
    [Min(0f)] public float randomOnlyWithin = 14f;
    [Tooltip("ไม่แง้มตอนผู้เล่นยืนชิดประตู (เมตร) — กันบานกระแทกหน้า")]
    [Min(0f)] public float randomNotCloserThan = 1.5f;
    [Tooltip("ทั้งด่านแง้มพร้อมกันได้สูงสุดกี่บาน (นับเฉพาะโหมดสุ่ม)")]
    [Min(1)] public int maxRandomAtOnce = 2;

    [Header("ประตู")]
    [Tooltip("บานที่แง้ม — เว้นว่าง = ลูกตัวแรกที่มีโมเดล (เช่น Door 1)")]
    public Transform panel;
    [Tooltip("เว้นว่าง = หาในตัวเอง")]
    public DoorController doorController;
    [Tooltip("แง้มกี่องศา (ติดลบ = แง้มอีกทาง)")]
    public float ajarAngle = 14f;
    [Tooltip("ใช้เวลาแง้มประมาณกี่วินาที")]
    [Min(0.05f)] public float openSeconds = 1.2f;
    [Tooltip("บานโยกเบาๆ ตอนแง้มค้าง (องศา) — 0 = นิ่ง")]
    [Min(0f)] public float swayDegrees = 1.2f;
    [Min(0f)] public float swaySpeed = 0.7f;

    [Header("เงื่อนไข")]
    [Tooltip("โหมด PlayerApproaches: ผู้เล่นเข้าใกล้ประตูกี่เมตร (แนวราบ)")]
    [Min(0.1f)] public float radius = 3f;
    [Tooltip("แง้มเฉพาะในโลก PTSD")]
    public bool requirePtsd = true;
    [Tooltip("ไม่แง้มถ้าประตูล็อกอยู่ (ประตูที่ไปต่อไม่ได้ ไม่ควรชวนเข้า)")]
    public bool onlyIfUnlocked = true;
    [Tooltip("แง้มเฉพาะตอนบอสฉากแรกกำลังไล่ — เว้นว่าง = ไม่เช็ค")]
    public FirstChaseStalker onlyWhileFirstChaseActive;
    [Tooltip("โหมด PlayerApproaches: ผู้เล่นเดินออกไปแล้ว ประตูค่อยๆ ปิดกลับ (ปิด = แง้มค้างไว้)")]
    public bool closeWhenPlayerLeaves = true;
    [Tooltip("โหมด PlayerApproaches: เปิดประตูจริงไปแล้ว 1 ครั้ง = เลิกแง้ม (ผู้เล่นรู้ทางแล้ว)")]
    public bool stopAfterOpened = true;

    [Header("เสียง / ไฟลอดช่อง")]
    public AudioClip creakSound;
    [Range(0f, 1f)] public float creakVolume = 0.8f;
    [Tooltip("ไฟฝั่งหลังประตู (สว่างขึ้นตอนแง้ม) — ห้ามอยู่ใต้ Lamps Root ของ WorldLighting")]
    public Light guideLight;
    public Color guideColor = new Color(1f, 0.9f, 0.72f);
    [Min(0f)] public float guideIntensity = 1.5f;

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private float angle;
    private float angleVel;
    private bool creaked;
    private bool everAjar;
    private bool finished;
    private bool applied;
    private AudioSource audioSource;
    private Transform player;

    // สุ่มแง้ม
    private static readonly System.Collections.Generic.HashSet<DoorAjarHint> randomOpenNow = new System.Collections.Generic.HashSet<DoorAjarHint>();
    private float nextRandomAt = -1f;
    private float randomOpenUntil = -1f;
    private bool wasAllowed;

    public bool IsAjar => angle > 0.5f;

    private void Awake()
    {
        if (doorController == null) doorController = GetComponent<DoorController>();
        if (panel == null) panel = FindPanel();
        if (panel == null || panel == transform)
        {
            Debug.LogWarning($"[DoorAjarHint] '{name}' ต้องมีบานลูก (เช่น Door 1) ให้หมุน — ใส่ช่อง Panel", this);
            enabled = false;
            return;
        }
        baseLocalPos = panel.localPosition;
        baseLocalRot = panel.localRotation;

        if (guideLight != null)
        {
            guideLight.color = guideColor;
            guideLight.intensity = 0f;
            guideLight.enabled = false;
        }
    }

    private Transform FindPanel()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.GetComponentInChildren<Renderer>(true) != null) return c;
        }
        return null;
    }

    private void OnDisable()
    {
        randomOpenNow.Remove(this);
        nextRandomAt = -1f;
        randomOpenUntil = -1f;
        if (panel != null && applied) { angle = 0f; ApplyAngle(0f); }
        if (guideLight != null) { guideLight.intensity = 0f; guideLight.enabled = false; }
    }

    // LateUpdate = หลัง Animator ของประตู
    private void LateUpdate()
    {
        if (finished) return;

        // ผู้เล่นเปิดประตูจริง → คืนบานทันที ให้ Animator เดิมทำงาน
        if (doorController != null && doorController.isOpen)
        {
            Snap();
            EndRandom();
            if (mode == TriggerMode.PlayerApproaches && stopAfterOpened && everAjar) finished = true;
            return;
        }

        // ประตูบานนี้กำลังถูกยัน (ข้อ 1) → อย่าแตะ
        DoorBarricadeStruggle s = DoorBarricadeStruggle.Active;
        if (s != null && s.doorController == doorController && doorController != null)
        {
            Snap();
            EndRandom();
            return;
        }

        bool allowed = ConditionsOk();
        bool want;
        if (mode == TriggerMode.RandomInPtsd)
        {
            want = RandomWant(allowed);
        }
        else
        {
            bool near = allowed && PlayerNear();
            want = near || (allowed && !closeWhenPlayerLeaves && everAjar);
        }

        if (want && !creaked)
        {
            creaked = true;
            PlayCreak();
        }
        if (!want && angle < 0.5f) creaked = false;   // ปิดสนิทแล้ว รอบหน้าเอี๊ยดใหม่
        if (want) everAjar = true;

        float target = want ? ajarAngle : 0f;
        angle = Mathf.SmoothDamp(angle, target, ref angleVel, openSeconds * 0.4f);

        float k = Mathf.Abs(ajarAngle) > 0.01f ? Mathf.Clamp01(Mathf.Abs(angle / ajarAngle)) : 0f;
        float sway = want && swayDegrees > 0f ? Mathf.Sin(Time.time * swaySpeed * Mathf.PI * 2f) * swayDegrees * k * Mathf.Sign(ajarAngle) : 0f;

        if (Mathf.Abs(angle) < 0.01f && !want) Snap();
        else ApplyAngle(angle + sway);

        if (guideLight != null)
        {
            guideLight.intensity = guideIntensity * k;
            guideLight.enabled = guideLight.intensity > 0.01f;
        }
    }

    private bool ConditionsOk()
    {
        if (onlyIfUnlocked && doorController != null && !doorController.isUnlocked) return false;
        if (onlyWhileFirstChaseActive != null && !onlyWhileFirstChaseActive.IsChasing) return false;
        if (requirePtsd)
        {
            IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
            bool ptsd = world != null ? world.IsInPtsd : (PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None);
            if (!ptsd) return false;
        }
        return true;
    }

    private bool PlayerNear() => PlayerDistance() <= radius;

    private float PlayerDistance()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return float.MaxValue;
            player = p.transform;
        }
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // แง้มเองเป็นพักๆ: รอเวลาสุ่ม → แง้มค้างช่วงหนึ่ง → ปิด → รอใหม่
    private bool RandomWant(bool allowed)
    {
        float now = Time.time;
        if (!allowed)
        {
            EndRandom();
            wasAllowed = false;
            return false;
        }
        if (!wasAllowed)   // เพิ่งเข้า PTSD → เริ่มนับใหม่ (บานต่างๆ ไม่แง้มพร้อมกันทันที)
        {
            wasAllowed = true;
            nextRandomAt = now + Random.Range(2f, Mathf.Max(2f, randomInterval.y));
        }

        if (randomOpenUntil > now) return true;
        if (randomOpenNow.Contains(this)) EndRandom();    // ครบเวลาแง้ม → ปิด แล้วรอรอบใหม่

        if (now >= nextRandomAt)
        {
            float dist = PlayerDistance();
            bool inRange = (randomOnlyWithin <= 0f || dist <= randomOnlyWithin) && dist >= randomNotCloserThan;
            if (inRange && randomOpenNow.Count < maxRandomAtOnce)
            {
                randomOpenUntil = now + RandomIn(randomDuration);
                randomOpenNow.Add(this);
                return true;
            }
            nextRandomAt = now + Random.Range(1f, 3f);   // ยังไม่ได้จังหวะ → ลองใหม่เร็วๆ
        }
        return false;
    }

    private void EndRandom()
    {
        if (!randomOpenNow.Remove(this) && randomOpenUntil < 0f) return;
        randomOpenUntil = -1f;
        nextRandomAt = Time.time + RandomIn(randomInterval);
    }

    private static float RandomIn(Vector2 r) => Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));

    private void Snap()
    {
        angle = 0f;
        angleVel = 0f;
        if (applied)
        {
            panel.localPosition = baseLocalPos;
            panel.localRotation = baseLocalRot;
            applied = false;
        }
        if (guideLight != null && guideLight.enabled) { guideLight.intensity = 0f; guideLight.enabled = false; }
    }

    /// <summary>หมุนบานรอบจุดบานพับ (ตำแหน่งของประตูตัวแม่) แกนชี้ขึ้น</summary>
    private void ApplyAngle(float a)
    {
        Transform parent = panel.parent;
        Vector3 baseWorldPos = parent != null ? parent.TransformPoint(baseLocalPos) : baseLocalPos;
        Quaternion baseWorldRot = parent != null ? parent.rotation * baseLocalRot : baseLocalRot;

        Quaternion q = Quaternion.AngleAxis(a, Vector3.up);
        Vector3 hinge = transform.position;
        panel.position = hinge + q * (baseWorldPos - hinge);
        panel.rotation = q * baseWorldRot;
        applied = true;
    }

    private void PlayCreak()
    {
        if (creakSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 15f;
        }
        audioSource.pitch = Random.Range(0.9f, 1.05f);
        audioSource.PlayOneShot(creakSound, creakVolume);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.6f);
        Vector3 c = transform.position;
        const int seg = 32;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
        }
    }
}
