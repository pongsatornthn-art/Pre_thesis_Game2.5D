using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>ผู้เล่นเปลี่ยนความสว่างในหน้าตั้งค่า (ค่า 0–1 · 0.5 = ปกติ)</summary>
public readonly struct BrightnessChangedEvent
{
    public readonly float Value;
    public BrightnessChangedEvent(float value) { Value = value; }
}

/// <summary>
/// [2026-11-25 นา] ความสว่างจากหน้าตั้งค่า — ยกโทนกลาง (Gamma) ทั้งจอ ไม่แตะสีไฟ / ไม่แตะ Volume PTSD
/// ใช้ Lift Gamma Gain เพราะ Volume อื่นในเกมไม่ได้ใช้ (PTSD ใช้ Color Adjustments → ถ้าใช้ตัวเดียวกันจะทับค่ากัน)
///
/// วิธีทำในซีน (ทุกซีนที่อยากให้ความสว่างมีผล รวมเมนูหลัก):
///   GameObject "BrightnessVolume" → Volume (Is Global ✔ · Priority 100 · Profile = Settings/BrightnessVolumeProfile)
///   + สคริปต์นี้ · Profile ต้องมี Lift Gamma Gain (เปิด override ช่อง Gamma)
/// </summary>
[RequireComponent(typeof(Volume))]
public class BrightnessVolume : MonoBehaviour
{
    public const string PrefKey = "Game_Brightness";
    public const float DefaultValue = 0.5f;

    [Tooltip("ปรับสุดทาง (ค่า 0 หรือ 1) แล้วโทนกลางเลื่อนได้มากแค่ไหน — 0.4 = มืดลง/สว่างขึ้นพอเห็นชัด ไม่ซีดจนเสียบรรยากาศ")]
    [SerializeField, Range(0.05f, 1f)] private float maxGammaShift = 0.4f;

    private LiftGammaGain lgg;

    private void Awake()
    {
        Volume volume = GetComponent<Volume>();
        // .profile = สำเนาตอนเล่น (ไม่เขียนทับไฟล์ profile จริง)
        if (volume.profile == null || !volume.profile.TryGet(out lgg))
            Debug.LogWarning("[BrightnessVolume] Profile ไม่มี Lift Gamma Gain — ช่องความสว่างจะไม่มีผล", this);
    }

    private void OnEnable() => GameEventBus.Subscribe<BrightnessChangedEvent>(OnChanged);
    private void OnDisable() => GameEventBus.Unsubscribe<BrightnessChangedEvent>(OnChanged);

    private void Start() => Apply(PlayerPrefs.GetFloat(PrefKey, DefaultValue));

    private void OnChanged(BrightnessChangedEvent e) => Apply(e.Value);

    private void Apply(float value01)
    {
        if (lgg == null) return;
        float shift = (Mathf.Clamp01(value01) - DefaultValue) * 2f * maxGammaShift;
        lgg.active = true;
        lgg.gamma.overrideState = true;
        lgg.gamma.value = new Vector4(1f, 1f, 1f, shift);   // w = เลื่อนความสว่างโทนกลาง (สีไม่เปลี่ยน)
    }
}
