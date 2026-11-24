using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// [Map Builder] จุดเสียงในฉาก — วางจากแท็บ "เสียง" ของ Map Builder
///   • Loop   = เสียงวนตลอด เช่น นาฬิกา น้ำหยด วิทยุซ่า
///   • Random = สุ่มเล่นเป็นระยะ เช่น ไม้ลั่น เสียงกระซิบ ประตูเอี๊ยด (สุ่มคลิป/ระยะเวลา/ตำแหน่งในรัศมี)
///
/// ความดังคิดจาก "ระยะถึงตัวผู้เล่น" ไม่ใช่ระยะถึงกล้อง
/// (หูฟัง AudioListener อยู่ที่กล้องซึ่งลอยสูง ~27 ม. ถ้าใช้ 3D ปกติ เสียงจะเบาหมด/ดังเท่ากันหมด)
/// ส่วนซ้าย-ขวายังใช้ตำแหน่งจริง (Pan) → ได้ยินว่ามาจากทางไหน
/// </summary>
[DisallowMultipleComponent]
public class SoundEmitter : MonoBehaviour
{
    public enum Mode { Loop, Random }
    public enum When { Always, RealOnly, PtsdOnly }

    [Header("เสียง")]
    public Mode mode = Mode.Loop;
    [Tooltip("Loop ใช้คลิปแรก · Random สุ่มจากทุกคลิป")]
    public AudioClip[] clips;
    [Tooltip("Loop: เปลี่ยนเป็นคลิปนี้ตอนอยู่ในโลก PTSD (เว้นว่าง = ใช้คลิปเดิม)")]
    public AudioClip ptsdClip;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("สุ่มระดับเสียงสูงต่ำ (1 = ปกติ)")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    public AudioMixerGroup output;

    [Header("ระยะได้ยิน (จากตัวผู้เล่น)")]
    [Tooltip("ใกล้กว่านี้ = ดังเต็ม")]
    [Min(0f)] public float fullVolumeDistance = 2f;
    [Tooltip("ไกลกว่านี้ = เงียบ")]
    [Min(0.1f)] public float maxDistance = 10f;
    [Tooltip("0 = ได้ยินกลางหัว · 1 = ซ้าย/ขวาชัดตามตำแหน่ง")]
    [Range(0f, 1f)] public float pan = 0.6f;

    [Header("โหมดสุ่ม")]
    [Tooltip("ทุกกี่วินาที (สุ่มระหว่างค่าต่ำสุด–สูงสุด)")]
    public Vector2 interval = new Vector2(8f, 20f);
    [Tooltip("เล่นที่จุดสุ่มในรัศมีนี้ (เมตร) — 0 = จุดเดิมทุกครั้ง")]
    [Min(0f)] public float scatterRadius = 0f;
    [Tooltip("ไม่เล่นซ้ำคลิปเดิมติดกัน")]
    public bool avoidRepeat = true;

    [Header("เงื่อนไข")]
    public When playWhen = When.Always;
    [Tooltip("วินาทีที่ค่อยๆ ดัง/เบาตอนเปลี่ยนโลก")]
    [Min(0f)] public float fade = 1.2f;

    private AudioSource src;
    private Transform player;
    private float nextPlay;
    private int lastIndex = -1;
    private float gate;          // 0..1 เปิด/ปิดตามเงื่อนไข (ค่อยๆ เฟด)
    private bool inPtsd;
    private Vector3 home;

    private void Awake()
    {
        home = transform.position;
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.dopplerLevel = 0f;
        src.rolloffMode = AudioRolloffMode.Custom;   // ความดังเราคุมเอง → ปิดการเบาตามระยะกล้อง
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        src.minDistance = 1f;
        src.maxDistance = 500f;
        src.spatialBlend = pan;
        if (output != null) src.outputAudioMixerGroup = output;
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePtsd;
        inPtsd = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
        gate = Allowed() ? 1f : 0f;
        ScheduleNext(true);
        if (mode == Mode.Loop) StartLoop();
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePtsd;
        if (src != null) src.Stop();
    }

    private void HandlePtsd(bool active)
    {
        inPtsd = active;
        if (mode == Mode.Loop && ptsdClip != null) StartLoop();
    }

    private bool Allowed() =>
        playWhen == When.Always || (playWhen == When.RealOnly && !inPtsd) || (playWhen == When.PtsdOnly && inPtsd);

    private void StartLoop()
    {
        AudioClip c = inPtsd && ptsdClip != null ? ptsdClip : (clips != null && clips.Length > 0 ? clips[0] : null);
        if (c == null) { src.Stop(); return; }
        if (src.clip == c && src.isPlaying) return;
        src.clip = c;
        src.loop = true;
        src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        src.time = Random.Range(0f, c.length * 0.9f);   // หลายจุดใช้คลิปเดียวกันจะได้ไม่ดังพร้อมกันเป๊ะ
        src.Play();
    }

    private void ScheduleNext(bool first)
    {
        float lo = Mathf.Max(0.1f, Mathf.Min(interval.x, interval.y));
        float hi = Mathf.Max(lo, Mathf.Max(interval.x, interval.y));
        nextPlay = Time.time + (first ? Random.Range(0f, hi) : Random.Range(lo, hi));
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        float step = fade <= 0f ? 1f : Time.deltaTime / fade;
        gate = Mathf.MoveTowards(gate, Allowed() ? 1f : 0f, step);

        if (mode == Mode.Random && Time.time >= nextPlay)
        {
            ScheduleNext(false);
            if (gate > 0.5f && Audible(home) > 0.01f) PlayRandom();
        }

        src.spatialBlend = pan;
        float dist = mode == Mode.Random ? Audible(transform.position) : Audible(home);
        float vol = volume * gate * dist;
        if (mode == Mode.Loop) src.volume = vol;
        else if (src.isPlaying) src.volume = vol;
    }

    /// <summary>0..1 ตามระยะถึงตัวผู้เล่น (ไม่มีผู้เล่น = 1)</summary>
    private float Audible(Vector3 at)
    {
        if (player == null) return 1f;
        float d = Vector3.Distance(at, player.position);
        if (d <= fullVolumeDistance) return 1f;
        if (d >= maxDistance) return 0f;
        float t = (d - fullVolumeDistance) / Mathf.Max(0.01f, maxDistance - fullVolumeDistance);
        return (1f - t) * (1f - t);     // จางแบบธรรมชาติ (ใกล้ๆ ลดช้า ไกลๆ ลดเร็ว)
    }

    private void PlayRandom()
    {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (avoidRepeat && clips.Length > 1 && i == lastIndex) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        lastIndex = i;
        if (clips[i] == null) return;

        if (scatterRadius > 0f)
        {
            Vector2 r = Random.insideUnitCircle * scatterRadius;
            transform.position = home + new Vector3(r.x, 0f, r.y);
        }
        src.loop = false;
        src.clip = clips[i];
        src.pitch = Random.Range(pitchRange.x, pitchRange.y);
        src.volume = volume * gate * Audible(transform.position);
        src.Play();
    }

    /// <summary>ให้ระบบอื่นสั่งเล่นทันที (เช่น Story/Trigger)</summary>
    public void PlayNow()
    {
        if (mode == Mode.Loop) StartLoop(); else PlayRandom();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 p = Application.isPlaying ? home : transform.position;
        Gizmos.color = mode == Mode.Loop ? new Color(0.3f, 0.9f, 1f, 0.9f) : new Color(1f, 0.75f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(p, 0.15f);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 p = Application.isPlaying ? home : transform.position;
        UnityEditor.Handles.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(p, Vector3.up, fullVolumeDistance);
        UnityEditor.Handles.color = new Color(0.3f, 0.9f, 1f, 0.3f);
        UnityEditor.Handles.DrawWireDisc(p, Vector3.up, maxDistance);
        if (mode == Mode.Random && scatterRadius > 0f)
        {
            UnityEditor.Handles.color = new Color(1f, 0.75f, 0.3f, 0.7f);
            UnityEditor.Handles.DrawWireDisc(p, Vector3.up, scatterRadius);
        }
    }
#endif
}
