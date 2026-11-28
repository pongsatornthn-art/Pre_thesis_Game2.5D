using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] เสียงเท้าผู้เล่น — ไม่แตะโค้ดผู้เล่นของปอ (อ่านความเร็วจริงจาก Rigidbody + สถานะวิ่ง/ย่อจาก PlayerMovement)
/// แปะที่ตัว Player (ตัวเดียวกับ PlayerMovement) · ต้องมี AudioSource (ใส่คลิปเท้าแบบวนในช่อง Clip ของสคริปต์นี้)
///   เดิน = ความเร็วปกติ · วิ่ง (Shift) = เร็วขึ้น+ดังขึ้น · ย่อตัว (C) = เบาลง · หยุด/โดนล็อก = จางหาย
/// ส่งเสียงเข้ากลุ่ม SFX ของ AudioService (ปรับความดังในเมนูตั้งค่าได้)
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("คลิปเสียงเท้าแบบวน (หลายก้าวต่อกัน)")]
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float runVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float sneakVolume = 0.2f;
    [SerializeField, Range(0.5f, 2f)] private float walkPitch = 1f;
    [SerializeField, Range(0.5f, 2f)] private float runPitch = 1.4f;
    [SerializeField, Range(0.5f, 2f)] private float sneakPitch = 0.75f;
    [Tooltip("เร็วกว่านี้ (ม./วิ) ถึงนับว่าเดิน")]
    [SerializeField, Min(0f)] private float moveThreshold = 0.6f;
    [SerializeField, Min(0.1f)] private float fadeSpeed = 8f;

    private AudioSource source;
    private Rigidbody body;
    private PlayerMovement movement;
    private Vector3 lastPos;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // เสียงของตัวเราเอง — ไม่ลดตามระยะกล้อง
        source.volume = 0f;
        lastPos = transform.position;
    }

    private void Start()
    {
        if (ServiceLocator.GetOptional<IAudioService>() is AudioService svc && svc.sfxMixerGroup != null)
            source.outputAudioMixerGroup = svc.sfxMixerGroup;
        if (clip == null) Debug.LogWarning("[PlayerFootsteps] ยังไม่ได้ใส่คลิปเสียงเท้า", this);
    }

    private void Update()
    {
        if (clip == null) return;
        if (Time.timeScale == 0f) { if (source.isPlaying) source.Pause(); return; }

        float dt = Time.deltaTime;
        Vector3 v = body != null ? body.linearVelocity : (transform.position - lastPos) / Mathf.Max(dt, 0.0001f);
        lastPos = transform.position;
        v.y = 0f;

        bool locked = movement != null && (movement.isStunned || movement.IsActionLocked);
        bool moving = (!locked && v.magnitude > moveThreshold)
                      || PlayerAnimationController.ScriptedMove.HasValue;   // ฉากเดินอัตโนมัติ (ฉากเปิด) ก็มีเสียงเท้า
        bool running = moving && movement != null && movement.IsRunning;
        bool sneaking = moving && movement != null && movement.isSneaking;

        float targetVol = !moving ? 0f : running ? runVolume : sneaking ? sneakVolume : walkVolume;
        source.pitch = running ? runPitch : sneaking ? sneakPitch : walkPitch;
        source.volume = Mathf.MoveTowards(source.volume, targetVol, dt * fadeSpeed);

        if (source.volume > 0.001f && !source.isPlaying) source.Play();
        else if (source.volume <= 0.001f && source.isPlaying) source.Pause();
    }
}
