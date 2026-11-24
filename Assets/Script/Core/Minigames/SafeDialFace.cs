using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [Claude 2026-11-24] หน้าปัดตู้เซฟ: วางตัวเลข 0–9 (และขีด) รอบวงให้ห่างกันเท่ากันเป๊ะ (ทุก 36°)
/// ใส่บน "Dial Image" (รูปหน้าปัดเปล่าไม่มีตัวเลข) → ตัวเลขเป็นลูกของมัน หมุนไปพร้อมกัน
/// ทำไมไม่ใช้ตัวเลขในรูป: รูปที่ AI วาด ตัวเลขเรียงไม่ครบ/ไม่เท่ากัน → หมุนยังไงก็ไม่ตรงลูกศร
/// ปรับค่าใน Inspector แล้วเห็นผลทันที (ไม่ต้องกด Play)
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeDialFace : MonoBehaviour
{
    [Header("ตัวเลข")]
    [Range(2, 40)] public int count = 10;
    [Tooltip("ตัวเลขเรียงตามเข็มนาฬิกา (0 บนสุด → 1 ทางขวา) — แบบตู้เซฟทั่วไป")]
    public bool clockwise = true;
    [Tooltip("ระยะจากกลางถึงตัวเลข (สัดส่วนของรัศมีรูป) — รูปสนิมที่ให้ไป ≈ 0.72")]
    [Range(0.1f, 1f)] public float numberRadius = 0.72f;
    [Tooltip("ขนาดตัวเลข (สัดส่วนของความกว้างรูป)")]
    [Range(0.03f, 0.4f)] public float numberSize = 0.11f;
    public TMP_FontAsset font;
    public Color numberColor = new Color(0.12f, 0.1f, 0.08f, 0.95f);
    [Tooltip("เปิด = ตัวเลขหันหัวออกนอกวงแบบหน้าปัดจริง · ปิด = ตั้งตรงตลอด")]
    public bool rotateNumbers = true;
    public FontStyles fontStyle = FontStyles.Bold;

    [Header("ขีดระหว่างตัวเลข")]
    public bool ticks = true;
    [Tooltip("ขีดย่อยต่อช่อง (0 = มีแค่ขีดที่ตัวเลข)")]
    [Range(0, 9)] public int subTicks = 1;
    [Range(0.1f, 1f)] public float tickRadius = 0.60f;
    [Range(0.01f, 0.3f)] public float tickLength = 0.045f;
    [Range(0.002f, 0.05f)] public float tickWidth = 0.012f;
    public Color tickColor = new Color(0.12f, 0.1f, 0.08f, 0.85f);

    private const string ContainerName = "_DialNumbers";

    /// <summary>มุมต่อ 1 ช่อง (องศา)</summary>
    public float Step => 360f / Mathf.Max(1, count);

    private void OnEnable()
    {
        // ตอนเล่นจริง: มีตัวเลขที่สร้างไว้แล้วในซีน → แค่จัดตำแหน่ง ไม่สร้างใหม่
        if (Application.isPlaying && transform.Find(ContainerName) != null) Layout();
        else Rebuild();
    }
    private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Layout(); }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ห้ามสร้าง/ลบ GameObject ใน OnValidate → ทำทีหลัง 1 จังหวะ
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
    }
#endif

    [ContextMenu("สร้างตัวเลขใหม่")]
    public void Rebuild()
    {
        Transform old = transform.Find(ContainerName);
        if (old != null)
        {
            old.name = "_old"; // Destroy ตอนเล่นจะลบท้ายเฟรม → เปลี่ยนชื่อกัน Find เจอตัวเก่า
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }

        var container = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = container.offsetMax = Vector2.zero;
        container.SetAsLastSibling();

        int total = ticks ? count * (subTicks + 1) : 0;
        for (int i = 0; i < total; i++)
        {
            var t = new GameObject("Tick_" + i, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            t.SetParent(container, false);
            Image img = t.GetComponent<Image>();
            img.color = tickColor;
            img.raycastTarget = false;
        }
        for (int n = 0; n < count; n++)
        {
            var go = new GameObject("Num_" + n, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(container, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = n.ToString();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
        }
        Layout();
    }

    /// <summary>จัดตำแหน่ง/ขนาดตามขนาดรูปตอนนี้</summary>
    public void Layout()
    {
        Transform c = transform.Find(ContainerName);
        if (c == null) return;
        Rect r = ((RectTransform)transform).rect;
        float size = Mathf.Min(r.width, r.height);
        float half = size * 0.5f;
        float dir = clockwise ? 1f : -1f;

        int tickTotal = ticks ? count * (subTicks + 1) : 0;
        for (int i = 0; i < tickTotal; i++)
        {
            Transform t = c.Find("Tick_" + i);
            if (t == null) continue;
            var rt = (RectTransform)t;
            bool major = i % (subTicks + 1) == 0;
            float ang = dir * i * Step / (subTicks + 1);           // องศาตามเข็มจากด้านบน
            float len = size * tickLength * (major ? 1f : 0.6f);
            float rad = half * tickRadius - len * 0.5f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size * tickWidth * (major ? 1f : 0.7f), len);
            rt.anchoredPosition = Polar(ang, rad);
            rt.localEulerAngles = new Vector3(0, 0, -ang);
            rt.GetComponent<Image>().color = tickColor;
        }

        for (int n = 0; n < count; n++)
        {
            Transform t = c.Find("Num_" + n);
            if (t == null) continue;
            var rt = (RectTransform)t;
            float ang = dir * n * Step;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size * numberSize * 1.6f, size * numberSize * 1.3f);
            rt.anchoredPosition = Polar(ang, half * numberRadius);
            rt.localEulerAngles = new Vector3(0, 0, rotateNumbers ? -ang : 0f);
            TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = size * numberSize;
            tmp.color = numberColor;
            tmp.fontStyle = fontStyle;
            if (font != null && tmp.font != font) tmp.font = font;
        }
    }

    /// <summary>มุม (องศา ตามเข็มนาฬิกา เริ่มจากด้านบน) → ตำแหน่ง</summary>
    private static Vector2 Polar(float angDeg, float radius)
    {
        float a = angDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
    }

    /// <summary>มุมหมุนของหน้าปัด (Z) ที่ทำให้เลข n ไปอยู่ใต้ลูกศรด้านบนพอดี</summary>
    public float AngleForNumber(int n) => (clockwise ? 1f : -1f) * n * Step;
}
