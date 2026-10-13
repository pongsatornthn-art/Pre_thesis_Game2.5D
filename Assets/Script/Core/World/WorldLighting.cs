using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// บรรยากาศแสง "ต่อโลก" — โลกปกติสว่าง (ไฟเปิดหมด) · โลก PTSD มืด (ไฟดับเหลือไม่กี่ดวง + แสงจันทร์)
/// วางในซีน 1 ตัว (เช่น ที่ Map/Map_Test/Lights) · ตั้งค่าทั้งหมดใน Inspector ไม่ต้องแก้โค้ด
///
/// ฟัง WorldVisualsSwappedEvent (ตอนฉากสลับจริง กลางจอมืด) → ค่อยๆ เปลี่ยน:
///   แสงรอบตัว (ambient) · แสงหลัก (แดด/จันทร์) · ไฟทุกดวงใต้ Lamps Root (ความแรงคูณ + เปิด/ปิดตาม WorldLamp)
/// คุยกับโลกผ่าน IWorldModeService เท่านั้น (ไม่เรียก PTSDManager ตรง)
/// </summary>
public class WorldLighting : MonoBehaviour
{
    [Serializable]
    public class Mood
    {
        [Tooltip("แสงรอบตัว (สีล้วน) — ยิ่งสว่าง ยิ่งเห็นทั่วทั้งฉาก")]
        public Color ambient = new Color(0.2f, 0.21f, 0.28f);

        [Tooltip("สีแสงหลัก (Directional Light)")]
        public Color mainLightColor = new Color(0.55f, 0.65f, 0.9f);
        [Min(0f)] public float mainLightIntensity = 0.35f;

        [Tooltip("คูณความสว่างไฟในห้องทุกดวง (1 = ตามที่ตั้งใน Light)")]
        [Min(0f)] public float lampMultiplier = 1f;
    }

    [Header("บรรยากาศแต่ละโลก")]
    public Mood real = new Mood
    {
        ambient = new Color(0.55f, 0.53f, 0.5f),
        mainLightColor = new Color(1f, 0.95f, 0.85f),
        mainLightIntensity = 0.6f,
        lampMultiplier = 1.4f
    };
    public Mood ptsd = new Mood
    {
        ambient = new Color(0.26f, 0.27f, 0.34f),
        mainLightColor = new Color(0.55f, 0.65f, 0.9f),
        mainLightIntensity = 0.4f,
        lampMultiplier = 1f
    };

    [Header("ของในซีน")]
    [Tooltip("แสงหลัก (Directional Light)")]
    [SerializeField] private Light mainLight;
    [Tooltip("ตัวแม่ของไฟในแมพ — ไฟทุกดวงข้างใต้ถูกคุม (แปะ WorldLamp ที่ดวงไหน = เลือกได้ว่าติดโลกไหน)")]
    [SerializeField] private Transform lampsRoot;

    [Header("การเปลี่ยน")]
    [Tooltip("วินาทีที่ใช้ค่อยๆ เปลี่ยน (0 = เปลี่ยนทันที)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

    private struct LampInfo { public Light light; public float baseIntensity; public WorldLamp tag; }
    private readonly List<LampInfo> lamps = new List<LampInfo>();
    private Coroutine running;

    private void Awake()
    {
        if (lampsRoot == null) lampsRoot = transform;
        foreach (Light l in lampsRoot.GetComponentsInChildren<Light>(true))
        {
            if (l == mainLight) continue;
            lamps.Add(new LampInfo { light = l, baseIntensity = l.intensity, tag = l.GetComponent<WorldLamp>() });
        }
    }

    private void OnEnable() => GameEventBus.Subscribe<WorldVisualsSwappedEvent>(OnSwapped);
    private void OnDisable() => GameEventBus.Unsubscribe<WorldVisualsSwappedEvent>(OnSwapped);

    private void Start()
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        Apply(world != null ? world.VisibleWorld : WorldMode.Real, instant: true);
    }

    private void OnSwapped(WorldVisualsSwappedEvent e) => Apply(e.Visible, instant: false);

    /// <summary>เปลี่ยนบรรยากาศเป็นของโลกนี้</summary>
    public void Apply(WorldMode mode, bool instant)
    {
        if (running != null) StopCoroutine(running);
        if (instant || fadeSeconds <= 0f || !isActiveAndEnabled) Set(mode, 1f, Snapshot());
        else running = StartCoroutine(Fade(mode));
    }

    private IEnumerator Fade(WorldMode mode)
    {
        float[] from = Snapshot();
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            Set(mode, t / fadeSeconds, from);
            yield return null;
        }
        Set(mode, 1f, from);
        running = null;
    }

    // สถานะตอนเริ่มเปลี่ยน: [ambient r,g,b, main intensity, main r,g,b, lamp0..N]
    private float[] Snapshot()
    {
        var s = new float[7 + lamps.Count];
        Color a = RenderSettings.ambientLight;
        s[0] = a.r; s[1] = a.g; s[2] = a.b;
        if (mainLight != null) { s[3] = mainLight.intensity; s[4] = mainLight.color.r; s[5] = mainLight.color.g; s[6] = mainLight.color.b; }
        for (int i = 0; i < lamps.Count; i++) s[7 + i] = lamps[i].light != null && lamps[i].light.enabled ? lamps[i].light.intensity : 0f;
        return s;
    }

    private void Set(WorldMode mode, float k, float[] from)
    {
        Mood m = mode == WorldMode.Real ? real : ptsd;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(new Color(from[0], from[1], from[2]), m.ambient, k);

        if (mainLight != null)
        {
            mainLight.intensity = Mathf.Lerp(from[3], m.mainLightIntensity, k);
            mainLight.color = Color.Lerp(new Color(from[4], from[5], from[6]), m.mainLightColor, k);
        }

        for (int i = 0; i < lamps.Count; i++)
        {
            LampInfo info = lamps[i];
            if (info.light == null) continue;
            bool on = info.tag == null || info.tag.IsLitIn(mode);
            float target = on ? info.baseIntensity * m.lampMultiplier : 0f;
            float value = Mathf.Lerp(from[7 + i], target, k);
            info.light.intensity = value;
            info.light.enabled = value > 0.001f;
            if (info.tag != null && info.tag.glowVisual != null) info.tag.glowVisual.enabled = on || k < 0.5f;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("ดูตัวอย่าง: โลกปกติ")]
    private void PreviewReal() => PreviewInEditor(WorldMode.Real);

    [ContextMenu("ดูตัวอย่าง: โลก PTSD")]
    private void PreviewPtsd() => PreviewInEditor(WorldMode.PtsdNarrative);

    // ใน Editor: ปรับแค่แสงรอบตัว + แสงหลัก ให้ดูบรรยากาศ (ไม่แตะความแรงไฟแต่ละดวง จะได้ไม่เพี้ยนค่าตั้งต้น)
    private void PreviewInEditor(WorldMode mode)
    {
        Mood m = mode == WorldMode.Real ? real : ptsd;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = m.ambient;
        if (mainLight != null) { mainLight.intensity = m.mainLightIntensity; mainLight.color = m.mainLightColor; }
        UnityEditor.SceneView.RepaintAll();
    }
#endif
}
