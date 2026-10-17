using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// บรรยากาศแสง "ต่อโลก" — โลกปกติ: ไฟทุกดวงติดสว่างปกติ · โลก PTSD: ไฟที่ยังติดกลายเป็นสีแดง ที่เหลือดับ + กระพริบ
/// วางในซีน 1 ตัว (ที่ Map/Map_Test/Lights) · ตั้งค่าทั้งหมดใน Inspector ไม่ต้องแก้โค้ด
///
/// ฟัง WorldVisualsSwappedEvent (ตอนฉากสลับจริง กลางจอมืด) → ค่อยๆ เปลี่ยน:
///   แสงรอบตัว (ambient) · แสงหลัก (แดด/จันทร์) · ไฟทุกดวงใต้ Lamps Root (ความแรง + สี + ติด/ดับ ตาม WorldLamp)
/// โลก PTSD มีกระพริบ 4 แบบ: ดวงที่ตั้ง Flicker (ตลอด) · สุ่มดวงแดงกระพริบเป็นพักๆ · ทุกดวงกระพริบพร้อมกันตอนเพิ่งเข้า (ช็อก)
///   · ไฟใกล้ "ตัวรบกวน" (LampDisturber — แปะที่สตอล์กเกอร์) กระพริบรัว ยิ่งใกล้ยิ่งถี่
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

        [Tooltip("สีที่ย้อมไฟทุกดวงที่ยังติด")]
        public Color lampTint = Color.white;
        [Tooltip("ย้อมสีแค่ไหน (0 = สีเดิมของไฟ · 1 = สี Lamp Tint เต็มๆ)")]
        [Range(0f, 1f)] public float lampTintAmount = 0f;
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
        ambient = new Color(0.16f, 0.12f, 0.14f),
        mainLightColor = new Color(0.55f, 0.65f, 0.9f),
        mainLightIntensity = 0.25f,
        lampMultiplier = 1.2f,
        lampTint = new Color(1f, 0.1f, 0.06f),
        lampTintAmount = 1f
    };

    [Header("ของในซีน")]
    [Tooltip("แสงหลัก (Directional Light)")]
    [SerializeField] private Light mainLight;
    [Tooltip("ตัวแม่ของไฟในแมพ — ไฟทุกดวงข้างใต้ถูกคุม (แปะ WorldLamp ที่ดวงไหน = เลือกได้ว่าโลก PTSD เป็นยังไง)")]
    [SerializeField] private Transform lampsRoot;

    [Header("การเปลี่ยน")]
    [Tooltip("วินาทีที่ใช้ค่อยๆ เปลี่ยน (0 = เปลี่ยนทันที)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

    [Header("กระพริบ (โลก PTSD เท่านั้น)")]
    [Tooltip("สุ่มดวงแดงมากระพริบทุกกี่วินาที (ต่ำสุด–สูงสุด)")]
    [SerializeField] private Vector2 randomFlickerEvery = new Vector2(2f, 6f);
    [Tooltip("ดวงที่โดนสุ่มกระพริบนานกี่วินาที (ต่ำสุด–สูงสุด)")]
    [SerializeField] private Vector2 randomFlickerLength = new Vector2(0.2f, 1f);
    [Tooltip("เพิ่งเข้าโลก PTSD → ไฟทุกดวงกระพริบพร้อมกันกี่วินาที (0 = ไม่ทำ)")]
    [SerializeField, Min(0f)] private float shockSeconds = 1.2f;
    [Tooltip("ตอนกระพริบ 'ดับ' เหลือความสว่างเท่าไหร่ (0 = ดับสนิท)")]
    [SerializeField, Range(0f, 1f)] private float flickerDimLevel = 0.05f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color GlowOffColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    private class Lamp
    {
        public Light light;
        public WorldLamp tag;
        public float baseIntensity;
        public Color baseColor;
        public Color glowBaseColor;

        public float level, fromLevel;          // ความสว่างตามโลก (ยังไม่รวมกระพริบ)
        public Color color, fromColor;
        public float glowLit, fromGlowLit;       // 0 = โคมดับ · 1 = โคมติด

        public bool flickOn = true;
        public float flickTimer;
        public float burstUntil;

        public WorldLamp.PtsdState State => tag != null ? tag.inPtsd : WorldLamp.PtsdState.Red;
    }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private MaterialPropertyBlock block;
    private WorldMode current = WorldMode.Real;
    private Coroutine running;
    private float nextRandomFlickerAt;
    private float shockUntil;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (lampsRoot == null) lampsRoot = transform;
        foreach (Light l in lampsRoot.GetComponentsInChildren<Light>(true))
        {
            if (l == mainLight) continue;
            var lamp = new Lamp { light = l, tag = l.GetComponent<WorldLamp>(), baseIntensity = l.intensity, baseColor = l.color };
            Renderer glow = lamp.tag != null ? lamp.tag.glowVisual : null;
            lamp.glowBaseColor = glow != null && glow.sharedMaterial != null && glow.sharedMaterial.HasProperty(BaseColorId)
                ? glow.sharedMaterial.GetColor(BaseColorId) : Color.white;
            lamps.Add(lamp);
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
        bool enteringPtsd = mode != WorldMode.Real && current == WorldMode.Real;
        current = mode;
        if (enteringPtsd && !instant && shockSeconds > 0f) shockUntil = Time.time + fadeSeconds + shockSeconds;
        nextRandomFlickerAt = Time.time + fadeSeconds + shockSeconds + RandomIn(randomFlickerEvery);

        if (running != null) StopCoroutine(running);
        BeginFade();
        if (instant || fadeSeconds <= 0f || !isActiveAndEnabled) Set(mode, 1f);
        else running = StartCoroutine(Fade(mode));
    }

    private IEnumerator Fade(WorldMode mode)
    {
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            Set(mode, t / fadeSeconds);
            yield return null;
        }
        Set(mode, 1f);
        running = null;
    }

    // จำค่าตอนเริ่มเปลี่ยน
    private Color fromAmbient, fromMainColor;
    private float fromMainIntensity;

    private void BeginFade()
    {
        fromAmbient = RenderSettings.ambientLight;
        if (mainLight != null) { fromMainIntensity = mainLight.intensity; fromMainColor = mainLight.color; }
        foreach (Lamp lamp in lamps) { lamp.fromLevel = lamp.level; lamp.fromColor = lamp.color; lamp.fromGlowLit = lamp.glowLit; }
    }

    private void Set(WorldMode mode, float k)
    {
        Mood m = mode == WorldMode.Real ? real : ptsd;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(fromAmbient, m.ambient, k);

        if (mainLight != null)
        {
            mainLight.intensity = Mathf.Lerp(fromMainIntensity, m.mainLightIntensity, k);
            mainLight.color = Color.Lerp(fromMainColor, m.mainLightColor, k);
        }

        foreach (Lamp lamp in lamps)
        {
            bool on = mode == WorldMode.Real || lamp.State != WorldLamp.PtsdState.Off;
            Color targetColor = Color.Lerp(lamp.baseColor, m.lampTint, m.lampTintAmount);
            lamp.level = Mathf.Lerp(lamp.fromLevel, on ? lamp.baseIntensity * m.lampMultiplier : 0f, k);
            lamp.color = Color.Lerp(lamp.fromColor, targetColor, k);
            lamp.glowLit = Mathf.Lerp(lamp.fromGlowLit, on ? 1f : 0f, k);
        }
        Render();
    }

    private void Update()
    {
        if (lamps.Count == 0) return;
        if (current != WorldMode.Real) PickRandomFlicker();
        TickFlicker(Time.deltaTime);
        Render();
    }

    private void PickRandomFlicker()
    {
        if (Time.time < nextRandomFlickerAt) return;
        nextRandomFlickerAt = Time.time + RandomIn(randomFlickerEvery);

        // สุ่มเฉพาะดวงแดงที่ติดอยู่ และไม่ได้กำลังกระพริบ
        int count = 0;
        Lamp pick = null;
        foreach (Lamp lamp in lamps)
        {
            if (lamp.State != WorldLamp.PtsdState.Red || lamp.level <= 0.001f || Time.time < lamp.burstUntil) continue;
            count++;
            if (UnityEngine.Random.Range(0, count) == 0) pick = lamp;
        }
        if (pick != null) pick.burstUntil = Time.time + RandomIn(randomFlickerLength);
    }

    private void TickFlicker(float dt)
    {
        bool inPtsd = current != WorldMode.Real;
        bool shock = inPtsd && Time.time < shockUntil;
        foreach (Lamp lamp in lamps)
        {
            bool steady = lamp.State == WorldLamp.PtsdState.Flicker;
            bool burst = shock || Time.time < lamp.burstUntil;
            float near = inPtsd ? DisturbanceAt(lamp) : 0f;   // 0 = ไม่มีตัวรบกวนใกล้ · 1 = อยู่ใต้ไฟพอดี
            if (!inPtsd || (!steady && !burst && near <= 0f)) { lamp.flickOn = true; lamp.flickTimer = 0f; continue; }

            lamp.flickTimer -= dt;
            if (lamp.flickTimer > 0f) continue;
            lamp.flickOn = !lamp.flickOn;
            // ตัวรบกวน (บอส) ใกล้ = รัว ยิ่งใกล้ยิ่งดับนาน · กระพริบรัว (สุ่ม/ช็อก) = สั้นๆ ถี่ๆ · ดวง Flicker ตลอด = ติดนานกว่า ดูเป็นหลอดเสีย
            if (near > 0f) lamp.flickTimer = lamp.flickOn ? UnityEngine.Random.Range(0.03f, 0.05f + 0.4f * (1f - near)) : UnityEngine.Random.Range(0.03f, 0.08f + 0.2f * near);
            else if (burst) lamp.flickTimer = lamp.flickOn ? UnityEngine.Random.Range(0.03f, 0.12f) : UnityEngine.Random.Range(0.03f, 0.1f);
            else lamp.flickTimer = lamp.flickOn ? UnityEngine.Random.Range(0.1f, 1.2f) : UnityEngine.Random.Range(0.04f, 0.25f);
        }
    }

    // ตัวรบกวนไฟ (LampDisturber) ที่ใกล้ไฟดวงนี้สุด → 0..1 (วัดแนวราบ ไม่สนความสูง)
    private static float DisturbanceAt(Lamp lamp)
    {
        if (lamp.light == null || LampDisturber.Active.Count == 0) return 0f;
        Vector3 p = lamp.light.transform.position;
        float best = 0f;
        foreach (LampDisturber d in LampDisturber.Active)
        {
            float r = d.Radius;
            if (r <= 0f) continue;
            Vector3 q = d.transform.position;
            float dist = Mathf.Sqrt((p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z));
            if (dist < r) best = Mathf.Max(best, 1f - dist / r);
        }
        return best;
    }

    private void Render()
    {
        foreach (Lamp lamp in lamps)
        {
            if (lamp.light == null) continue;
            float flick = lamp.flickOn ? 1f : flickerDimLevel;
            float value = lamp.level * flick;
            lamp.light.intensity = value;
            lamp.light.color = lamp.color;
            lamp.light.enabled = value > 0.001f;

            Renderer glow = lamp.tag != null ? lamp.tag.glowVisual : null;
            if (glow == null) continue;
            // สีโคม = สีไฟ (ย้อมแดงตามไฟ) · ดับ/กระพริบดับ = เทาเข้ม
            Color lit = lamp.glowBaseColor * new Color(lamp.color.r / Mathf.Max(lamp.baseColor.r, 0.01f),
                                                       lamp.color.g / Mathf.Max(lamp.baseColor.g, 0.01f),
                                                       lamp.color.b / Mathf.Max(lamp.baseColor.b, 0.01f), 1f);
            Color c = Color.Lerp(GlowOffColor, lit, lamp.glowLit * flick);
            glow.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            glow.SetPropertyBlock(block);
        }
    }

    private static float RandomIn(Vector2 range) => UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

#if UNITY_EDITOR
    [ContextMenu("ดูตัวอย่าง: โลกปกติ")]
    private void PreviewReal() => PreviewInEditor(WorldMode.Real);

    [ContextMenu("ดูตัวอย่าง: โลก PTSD")]
    private void PreviewPtsd() => PreviewInEditor(WorldMode.PtsdNarrative);

    // ใน Editor: ปรับแค่แสงรอบตัว + แสงหลัก ให้ดูบรรยากาศ (ไม่แตะความแรง/สีไฟแต่ละดวง จะได้ไม่เพี้ยนค่าตั้งต้น)
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
