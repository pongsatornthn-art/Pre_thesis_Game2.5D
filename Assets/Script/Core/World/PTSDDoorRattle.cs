using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-26] ประตูสั่นตอนอยู่ในโลก PTSD — เหมือนมีอะไรทุบ/เขย่าอยู่อีกฝั่ง (บานสั่น + เสียงปัง)
///
/// วิธีใช้: สร้าง GameObject เปล่า 1 ตัวในซีน (เช่น "PTSD_DoorRattle") แล้วแปะสคริปต์นี้ — ตัวเดียวคุมประตูทั้งด่าน
///   หา DoorController ทุกบานในซีนให้เอง ไม่ต้องแก้ prefab ประตู / Animator
///   ใส่เสียงในช่อง Bang Sounds (เช่น Door_Slam_01-06, Door_Pounding) — เว้นว่าง = สั่นเงียบๆ
///
/// ไม่ตีกับของเดิม: ข้ามประตูที่เปิดอยู่ / กำลังแง้ม (DoorAjarHint) / กำลังเล่นมินิเกมดันประตู
/// เสียงคำนวณจากระยะถึงตัวผู้เล่น (กล้องอยู่สูง ใช้ 3D ปกติจะไม่ได้ยิน) เหมือน SoundEmitter
/// </summary>
[DefaultExecutionOrder(100)]   // หลัง DoorAjarHint / Animator
[DisallowMultipleComponent]
public class PTSDDoorRattle : MonoBehaviour
{
    [Header("ประตู")]
    [Tooltip("เว้นว่าง = หา DoorController ทุกบานในซีนเอง")]
    public DoorController[] doors;
    [Tooltip("ให้ประตูที่ล็อกอยู่สั่นด้วย (ประตูล็อกสั่น = หลอนดี เพราะเปิดไปดูไม่ได้)")]
    public bool includeLockedDoors = true;

    [Header("จังหวะ")]
    [Tooltip("เว้นช่วงระหว่างสั่นแต่ละครั้ง (วินาที ต่ำสุด–สูงสุด)")]
    public Vector2 interval = new Vector2(8f, 18f);
    [Tooltip("เพิ่งเข้า PTSD ไม่ถึงกี่วินาที ยังไม่สั่น")]
    [Min(0f)] public float graceSeconds = 3f;
    [Tooltip("ทุบกี่ที ต่อการสั่น 1 ครั้ง (ต่ำสุด–สูงสุด)")]
    public Vector2Int bangsPerRattle = new Vector2Int(2, 5);
    [Tooltip("ห่างระหว่างแต่ละที (วินาที)")]
    public Vector2 bangGap = new Vector2(0.22f, 0.45f);
    [Tooltip("โอกาสเป็นแบบ 'เขย่ารัวๆ' แทนทุบเป็นจังหวะ")]
    [Range(0f, 1f)] public float frenzyChance = 0.25f;

    [Header("ระยะจากผู้เล่น")]
    [Tooltip("เลือกเฉพาะประตูที่ห่างผู้เล่นไม่เกินเท่านี้ (เมตร)")]
    [Min(1f)] public float maxDistance = 12f;
    [Tooltip("ไม่เลือกประตูที่ชิดผู้เล่นเกินไป (เมตร)")]
    [Min(0f)] public float minDistance = 1.5f;

    [Header("การสั่นของบาน")]
    [Tooltip("บานโยกกี่องศาต่อการทุบ 1 ที")]
    [Min(0f)] public float bangAngle = 2.5f;
    [Tooltip("บานขยับเข้าออก (เมตร) ต่อการทุบ 1 ที")]
    [Min(0f)] public float bangPush = 0.015f;
    [Tooltip("ความถี่การสั่น (Hz)")]
    [Min(1f)] public float shakeFrequency = 16f;
    [Tooltip("สั่นแล้วหยุดเร็วแค่ไหน (มาก = หยุดไว)")]
    [Min(0.5f)] public float damping = 9f;

    [Header("เสียง")]
    public AudioClip[] bangSounds;
    [Range(0f, 1f)] public float volume = 0.9f;
    [Tooltip("ได้ยินดังเต็มที่ในระยะนี้ (เมตร)")]
    [Min(0f)] public float fullVolumeDistance = 3f;
    [Tooltip("ห่างเกินนี้ = เงียบ (เมตร)")]
    [Min(0.1f)] public float silentDistance = 14f;
    public UnityEngine.Audio.AudioMixerGroup output;

    [Header("Debug")]
    [Tooltip("ติ๊ก = สั่นได้แม้ไม่อยู่ใน PTSD (ไว้เทสต์) — ตอนส่งงานปิด")]
    public bool debugIgnorePtsd = false;

    // ---------------------------------------------------------------
    private class DoorInfo
    {
        public DoorController door;
        public DoorAjarHint ajar;
        public Transform panel;
        public Vector3 baseLocalPos;
        public Quaternion baseLocalRot;
        public bool applied;
    }

    private readonly List<DoorInfo> infos = new List<DoorInfo>();
    private DoorInfo current;
    private readonly List<float> bangTimes = new List<float>();
    private int nextBang;
    private float rattleEnd;
    private float sign = 1f;
    private float nextRattleAt = -1f;
    private float ptsdSince = -1f;
    private Transform player;
    private AudioSource src;

    private void Start()
    {
        DoorController[] list = doors != null && doors.Length > 0
            ? doors
            : FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (DoorController d in list)
        {
            if (d == null) continue;
            DoorAjarHint ajar = d.GetComponent<DoorAjarHint>();
            Transform panel = ajar != null && ajar.panel != null ? ajar.panel : FindPanel(d.transform);
            if (panel == null || panel == d.transform) continue;
            infos.Add(new DoorInfo
            {
                door = d, ajar = ajar, panel = panel,
                baseLocalPos = panel.localPosition, baseLocalRot = panel.localRotation
            });
        }

        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (output != null) src.outputAudioMixerGroup = output;

        if (infos.Count == 0) Debug.LogWarning("[PTSDDoorRattle] ไม่เจอประตูที่มีบานลูกให้สั่นเลย", this);
    }

    private static Transform FindPanel(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.GetComponentInChildren<Renderer>(true) != null) return c;
        }
        return null;
    }

    private void OnDisable() => StopCurrent();

    private void LateUpdate()
    {
        float now = Time.time;
        bool ptsd = debugIgnorePtsd || InPtsd();

        if (!ptsd)
        {
            ptsdSince = -1f;
            StopCurrent();
            return;
        }
        if (ptsdSince < 0f)
        {
            ptsdSince = now;
            nextRattleAt = now + graceSeconds + Random.Range(0f, 3f);
        }

        if (current != null)
        {
            if (!StillOk(current)) { StopCurrent(); return; }
            UpdateRattle(now);
            return;
        }

        if (now >= nextRattleAt)
        {
            DoorInfo pick = PickDoor();
            if (pick != null) StartRattle(pick, now);
            else nextRattleAt = now + Random.Range(1.5f, 3f);   // ยังไม่มีบานเหมาะ ลองใหม่เร็วๆ
        }
    }

    // ---------------------------------------------------------------
    private bool InPtsd()
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        if (world != null) return world.IsInPtsd;
        return PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
    }

    private bool StillOk(DoorInfo d)
    {
        if (d.door == null || d.panel == null || !d.door.isActiveAndEnabled) return false;
        if (d.door.isOpen) return false;
        if (d.ajar != null && d.ajar.isActiveAndEnabled && d.ajar.IsAjar) return false;
        DoorBarricadeStruggle s = DoorBarricadeStruggle.Active;
        if (s != null) return false;   // กำลังเล่นมินิเกมดันประตู = อย่ากวน
        return true;
    }

    private DoorInfo PickDoor()
    {
        Vector3 p = PlayerPos();
        if (float.IsInfinity(p.x)) return null;

        List<DoorInfo> ok = new List<DoorInfo>();
        foreach (DoorInfo d in infos)
        {
            if (!StillOk(d)) continue;
            if (!includeLockedDoors && !d.door.isUnlocked) continue;
            float dist = Flat(d.door.transform.position - p);
            if (dist > maxDistance || dist < minDistance) continue;
            ok.Add(d);
        }
        return ok.Count > 0 ? ok[Random.Range(0, ok.Count)] : null;
    }

    private void StartRattle(DoorInfo d, float now)
    {
        current = d;
        d.baseLocalPos = d.panel.localPosition;   // เผื่อประตูถูกขยับตอนจัดฉาก
        d.baseLocalRot = d.panel.localRotation;
        sign = Random.value < 0.5f ? -1f : 1f;

        bangTimes.Clear();
        bool frenzy = Random.value < frenzyChance;
        int n = frenzy ? Random.Range(6, 10) : Random.Range(Mathf.Max(1, bangsPerRattle.x), Mathf.Max(bangsPerRattle.x, bangsPerRattle.y) + 1);
        float t = now;
        for (int i = 0; i < n; i++)
        {
            bangTimes.Add(t);
            t += frenzy ? Random.Range(0.08f, 0.14f) : Random.Range(Mathf.Min(bangGap.x, bangGap.y), Mathf.Max(bangGap.x, bangGap.y));
        }
        nextBang = 0;
        rattleEnd = t + 4f / damping;
    }

    private void UpdateRattle(float now)
    {
        // เสียงตามจังหวะทุบ
        while (nextBang < bangTimes.Count && now >= bangTimes[nextBang])
        {
            PlayBang(current, nextBang);
            nextBang++;
        }

        // รวมแรงสั่นของทุกทีที่ทุบไปแล้ว (แต่ละทีสั่นแล้วค่อยๆ หยุด)
        float a = 0f, push = 0f;
        for (int i = 0; i < nextBang; i++)
        {
            float dt = now - bangTimes[i];
            float env = Mathf.Exp(-dt * damping);
            a += Mathf.Sin(dt * shakeFrequency * Mathf.PI * 2f) * env;
            push += env * Mathf.Exp(-dt * damping);   // ดันเข้าแล้วเด้งกลับ
        }
        a = Mathf.Clamp(a, -1.5f, 1.5f) * bangAngle * sign;
        push = Mathf.Clamp01(push) * bangPush;
        Apply(current, a, push);

        if (now >= rattleEnd)
        {
            StopCurrent();
            nextRattleAt = now + Random.Range(Mathf.Min(interval.x, interval.y), Mathf.Max(interval.x, interval.y));
        }
    }

    /// <summary>หมุนบานรอบจุดบานพับ (ตำแหน่งประตูตัวแม่) + ดันเข้าออกเล็กน้อย — แบบเดียวกับ DoorAjarHint</summary>
    private static void Apply(DoorInfo d, float angle, float push)
    {
        Transform parent = d.panel.parent;
        Vector3 baseWorldPos = parent != null ? parent.TransformPoint(d.baseLocalPos) : d.baseLocalPos;
        Quaternion baseWorldRot = parent != null ? parent.rotation * d.baseLocalRot : d.baseLocalRot;

        Quaternion q = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 hinge = d.door.transform.position;
        Vector3 normal = d.door.transform.forward;
        d.panel.position = hinge + q * (baseWorldPos - hinge) + normal * push;
        d.panel.rotation = q * baseWorldRot;
        d.applied = true;
    }

    private void StopCurrent()
    {
        if (current != null && current.applied && current.panel != null)
        {
            // ถ้าระหว่างสั่นมีคนเปิดประตู/แง้ม → ไม่คืนตำแหน่งทับ ให้สคริปต์นั้นคุมต่อ
            bool otherOwns = current.door != null && current.door.isOpen
                             || current.ajar != null && current.ajar.IsAjar;
            if (!otherOwns)
            {
                current.panel.localPosition = current.baseLocalPos;
                current.panel.localRotation = current.baseLocalRot;
            }
            current.applied = false;
        }
        current = null;
        bangTimes.Clear();
        nextBang = 0;
    }

    private void PlayBang(DoorInfo d, int index)
    {
        if (bangSounds == null || bangSounds.Length == 0 || src == null) return;
        AudioClip clip = bangSounds[Random.Range(0, bangSounds.Length)];
        if (clip == null) return;

        Vector3 p = PlayerPos();
        if (float.IsInfinity(p.x)) return;
        Vector3 off = d.door.transform.position - p;
        float dist = Flat(off);
        float k = 1f - Mathf.InverseLerp(fullVolumeDistance, Mathf.Max(fullVolumeDistance + 0.1f, silentDistance), dist);
        if (k <= 0.001f) return;

        src.panStereo = Mathf.Clamp(off.x / Mathf.Max(1f, silentDistance * 0.6f), -0.8f, 0.8f);
        src.pitch = Random.Range(0.9f, 1.08f);
        float v = volume * k * k * (index == 0 ? 1f : Random.Range(0.65f, 1f));
        src.PlayOneShot(clip, v);
    }

    private Vector3 PlayerPos()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindWithTag("Player");
            if (go == null) return Vector3.positiveInfinity;
            player = go.transform;
        }
        return player.position;
    }

    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
