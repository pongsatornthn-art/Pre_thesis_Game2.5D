using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [ข้อ 11] ไฟโซน — ไฟในกล่องนี้เปลี่ยนสีตามชนิดโซน (เฉพาะโลก PTSD ตามค่าเริ่ม)
///   Safe           = ไฟขาวนวล (ผู้เล่นเห็นแต่ไกลว่าห้องนี้ปลอดภัย = ไฟนำทาง)
///   Danger         = ไฟแดงเข้ม + หายใจช้าๆ
///   FollowSafeZone = ตามสถานะ SafeZone ในตัวเดียวกัน: Safe → ขาวนวล · Unsafe → แดง · Dormant → ไม่ย้อม (แดงตามโลก PTSD ปกติ)
///
/// วิธีใช้: Create Empty → Add Component นี้ (ได้ Box Collider มาด้วย) → ปรับกล่องให้ครอบห้อง/ทางเดิน
///   ไฟทุกดวงที่อยู่ในกล่องถูกเลือกเองตอนเริ่มเกม · เพิ่มเองได้ที่ Extra Lights
///   ใช้กับระบบ SafeZone: แปะตัวนี้ที่ GameObject เดียวกับ SafeZone แล้วเลือก Follow Safe Zone (ใช้กล่องเดียวกัน)
/// ทำงานร่วมกับ WorldLighting ผ่าน LampOverrides (ไม่ตีกัน กระพริบตอนบอสใกล้ยังทำงานตามเดิม)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ZoneLighting : MonoBehaviour
{
    public enum ZoneType { Safe, Danger, FollowSafeZone }

    [Header("โซน")]
    public ZoneType type = ZoneType.Safe;
    [Tooltip("ทำงานเฉพาะในโลก PTSD (โลกปกติไฟเป็นปกติ)")]
    public bool onlyInPtsd = true;
    [Tooltip("โหมด Follow Safe Zone — เว้นว่าง = หาในตัวเอง")]
    public SafeZone safeZone;

    [Header("โซนปลอดภัย")]
    public Color safeColor = new Color(1f, 0.92f, 0.78f);
    [Tooltip("ความแรง = ค่าตั้งต้นของไฟ × เท่านี้ (ไฟในห้องปลอดภัยติดเสมอ แม้ตั้ง WorldLamp = Off ไว้)")]
    [Min(0f)] public float safeIntensity = 1.2f;

    [Header("โซนอันตราย")]
    public Color dangerColor = new Color(1f, 0.06f, 0.04f);
    [Min(0f)] public float dangerIntensity = 1f;
    [Tooltip("ความเร็วไฟหายใจ (0 = นิ่ง)")]
    [Min(0f)] public float dangerPulseSpeed = 1.5f;
    [Tooltip("หายใจลึกแค่ไหน (0 = นิ่ง · 1 = มืดสุดตอนหายใจออก)")]
    [Range(0f, 1f)] public float dangerPulseDepth = 0.35f;

    [Header("ไฟที่โดนผล")]
    [Tooltip("เลือกไฟทุกดวงที่อยู่ในกล่องเองตอนเริ่มเกม")]
    public bool autoFindLights = true;
    [Tooltip("ไฟที่อยู่นอกกล่องแต่อยากให้เปลี่ยนด้วย")]
    public Light[] extraLights;
    [Tooltip("ไฟที่อยู่ในกล่องแต่ไม่อยากให้เปลี่ยน (เช่น MapLight_CloseUpOnly)")]
    public Light[] excludeLights;

    [Header("การเปลี่ยน")]
    [Tooltip("วินาทีที่ค่อยๆ เปลี่ยนสี")]
    [Min(0.01f)] public float fadeSeconds = 1f;

    private enum Want { None, Safe, Danger }

    private class Target
    {
        public Light light;
        public bool managed;
        public Color baseColor;
        public float baseIntensity;
    }

    private readonly List<Target> targets = new List<Target>();
    private float amount;
    private Want shown = Want.None;
    private BoxCollider box;

    public IReadOnlyList<Light> AffectedLights
    {
        get { var list = new List<Light>(); foreach (Target t in targets) list.Add(t.light); return list; }
    }

    private void Reset()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        if (b != null) b.isTrigger = true;
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (safeZone == null) safeZone = GetComponent<SafeZone>();
    }

    private void Start()
    {
        CollectLights();
    }

    private void OnDisable()
    {
        foreach (Target t in targets) Restore(t);
        amount = 0f;
        shown = Want.None;
    }

    private void CollectLights()
    {
        targets.Clear();
        var seen = new HashSet<Light>();
        if (excludeLights != null) foreach (Light l in excludeLights) if (l != null) seen.Add(l);

        if (autoFindLights && box != null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional || !Inside(l.transform.position)) continue;
                if (seen.Add(l)) targets.Add(MakeTarget(l));
            }
        }
        if (extraLights != null)
            foreach (Light l in extraLights)
                if (l != null && seen.Add(l)) targets.Add(MakeTarget(l));

        if (targets.Count == 0)
            Debug.LogWarning($"[ZoneLighting] '{name}' ไม่มีไฟในกล่องเลย — ขยายกล่องให้ครอบโคมไฟ หรือลากไฟใส่ Extra Lights", this);
    }

    private static Target MakeTarget(Light l) => new Target
    {
        light = l,
        managed = LampOverrides.IsManaged(l),
        baseColor = l.color,
        baseIntensity = l.intensity
    };

    private bool Inside(Vector3 worldPos)
    {
        Vector3 p = transform.InverseTransformPoint(worldPos) - box.center;
        Vector3 h = box.size * 0.5f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
    }

    private void Update()
    {
        Want want = Evaluate();
        if (want != Want.None && want != shown && amount <= 0.001f) shown = want;   // โซนเพิ่งเริ่ม → ใช้สีใหม่เลย

        if (want == shown && want != Want.None) amount = Mathf.MoveTowards(amount, 1f, Time.deltaTime / fadeSeconds);
        else
        {
            amount = Mathf.MoveTowards(amount, 0f, Time.deltaTime / fadeSeconds);   // จางสีเดิมออกก่อน แล้วค่อยเปลี่ยนสี
            if (amount <= 0.001f) shown = want;
        }

        if (shown == Want.None || amount <= 0.001f)
        {
            foreach (Target t in targets) Restore(t);
            return;
        }

        Color c = shown == Want.Safe ? safeColor : dangerColor;
        float mul = shown == Want.Safe ? safeIntensity : dangerIntensity;
        if (shown == Want.Danger && dangerPulseSpeed > 0f && dangerPulseDepth > 0f)
            mul *= 1f - dangerPulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * dangerPulseSpeed * Mathf.PI));

        foreach (Target t in targets) Apply(t, c, mul);
    }

    private Want Evaluate()
    {
        if (onlyInPtsd && !IsPtsdVisible()) return Want.None;
        switch (type)
        {
            case ZoneType.Safe: return Want.Safe;
            case ZoneType.Danger: return Want.Danger;
            case ZoneType.FollowSafeZone:
                if (safeZone == null) return Want.None;
                if (safeZone.State == SafeZoneState.Safe) return Want.Safe;
                if (safeZone.State == SafeZoneState.Unsafe) return Want.Danger;
                return Want.None;
        }
        return Want.None;
    }

    private static bool IsPtsdVisible()
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        if (world != null) return world.VisibleWorld != WorldMode.Real;
        return PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
    }

    private void Apply(Target t, Color c, float mul)
    {
        if (t.light == null) return;
        if (t.managed)
        {
            LampOverrides.Set(t.light, c, amount, mul);
        }
        else
        {
            // ไฟที่ WorldLighting ไม่ได้คุม → เปลี่ยนตรงๆ
            t.light.color = Color.Lerp(t.baseColor, c, amount);
            t.light.intensity = Mathf.Lerp(t.baseIntensity, t.baseIntensity * mul, amount);
        }
    }

    private static void Restore(Target t)
    {
        if (t.light == null) return;
        if (t.managed) LampOverrides.Clear(t.light);
        else
        {
            t.light.color = t.baseColor;
            t.light.intensity = t.baseIntensity;
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        if (b == null) return;
        Color c = type == ZoneType.Safe ? new Color(1f, 0.92f, 0.6f) : type == ZoneType.Danger ? new Color(1f, 0.15f, 0.1f) : new Color(0.6f, 0.6f, 1f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(c.r, c.g, c.b, 0.12f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.8f);
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void OnDrawGizmosSelected()
    {
        foreach (Target t in targets)
        {
            if (t.light == null) continue;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, t.light.transform.position);
        }
    }
}
