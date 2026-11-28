using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// [Map Builder] เสียงบรรยากาศประจำห้อง — เดินเข้าโซนแล้วค่อยๆ ดังขึ้น เดินออกค่อยๆ เบาลง
/// (หลายห้องติดกันจะเฟดข้ามกันเอง) · เข้าโลก PTSD สลับเป็นอีกเสียงแบบเฟดข้าม
/// ต้องมี BoxCollider แบบ Is Trigger (เครื่องมือสร้างให้) · เสียงแบบ 2D (ได้ยินเท่ากันทั้งห้อง)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public class AmbientZone : MonoBehaviour
{
    [Header("เสียง")]
    public AudioClip realClip;
    [Tooltip("เสียงตอนอยู่ในโลก PTSD (เว้นว่าง = ใช้เสียงเดิม)")]
    public AudioClip ptsdClip;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("ปิด = ในโลก PTSD เงียบ (ถ้าไม่ได้ใส่เสียง PTSD)")]
    public bool keepRealInPtsd = true;
    public AudioMixerGroup output;

    [Header("เฟด")]
    [Min(0f)] public float fadeIn = 1.5f;
    [Min(0f)] public float fadeOut = 2f;

    private AudioSource realSrc, ptsdSrc;
    private bool inside, inPtsd;
    private float zoneT;   // 0..1 อยู่ในโซน
    private float ptsdT;   // 0..1 โลก PTSD

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        realSrc = MakeSource(realClip);
        ptsdSrc = MakeSource(ptsdClip);
    }

    private AudioSource MakeSource(AudioClip clip)
    {
        if (clip == null) return null;
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        if (output != null) s.outputAudioMixerGroup = output;
        return s;
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePtsd;
        inPtsd = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
        ptsdT = inPtsd ? 1f : 0f;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePtsd;
        inside = false;
        if (realSrc != null) realSrc.Stop();
        if (ptsdSrc != null) ptsdSrc.Stop();
        zoneT = 0f;
    }

    private void HandlePtsd(bool active) => inPtsd = active;

    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) inside = true; }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) inside = false; }

    private void Update()
    {
        float zoneStep = inside ? (fadeIn <= 0f ? 1f : Time.deltaTime / fadeIn) : (fadeOut <= 0f ? 1f : Time.deltaTime / fadeOut);
        zoneT = Mathf.MoveTowards(zoneT, inside ? 1f : 0f, zoneStep);
        ptsdT = Mathf.MoveTowards(ptsdT, inPtsd ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, fadeIn));

        // ไม่มีเสียง PTSD: เก็บเสียงเดิมไว้ (keepRealInPtsd) หรือเงียบ
        float realW = ptsdSrc != null || !keepRealInPtsd ? 1f - ptsdT : 1f;
        float ptsdW = ptsdT;
        Drive(realSrc, volume * zoneT * realW);
        Drive(ptsdSrc, volume * zoneT * ptsdW);
    }

    private static void Drive(AudioSource s, float v)
    {
        if (s == null) return;
        s.volume = v;
        if (v > 0.001f) { if (!s.isPlaying) { s.time = Random.Range(0f, s.clip.length * 0.9f); s.Play(); } }
        else if (s.isPlaying) s.Stop();   // เงียบสนิท → หยุด ไม่กินเครื่อง
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.06f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
