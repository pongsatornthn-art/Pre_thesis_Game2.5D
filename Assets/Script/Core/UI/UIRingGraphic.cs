using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-22] วงกลม UI ที่วาดเอง (ไม่ต้องมีไฟล์รูป) — เส้นหนาเท่าเดิมเสมอไม่ว่าวงจะใหญ่หรือเล็ก
/// ใช้ทำเป้าแบบวงกลม (CrosshairController สร้างให้อัตโนมัติ) · filled = วงทึบ (ใช้ทำจุดกลาง)
/// ขนาดวง = ขนาดของ RectTransform (เส้นผ่านศูนย์กลาง = ด้านที่สั้นกว่า)
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIRingGraphic : MaskableGraphic
{
    [Tooltip("ความหนาเส้น (พิกเซล UI)")]
    [SerializeField] private float thickness = 2f;
    [Tooltip("จำนวนเหลี่ยม (ยิ่งเยอะยิ่งกลมเนียน)")]
    [Range(12, 256)]
    [SerializeField] private int segments = 96;
    [Tooltip("ทึบทั้งวง (ใช้ทำจุดกลาง)")]
    [SerializeField] private bool filled = false;
    [Tooltip("ขอบนุ่ม กันเส้นเป็นขั้นบันได (พิกเซล)")]
    [SerializeField] private float feather = 1f;

    public float Thickness { get => thickness; set { if (!Mathf.Approximately(thickness, value)) { thickness = value; SetVerticesDirty(); } } }
    public bool Filled { get => filled; set { if (filled != value) { filled = value; SetVerticesDirty(); } } }
    public int Segments { get => segments; set { int v = Mathf.Clamp(value, 12, 256); if (segments != v) { segments = v; SetVerticesDirty(); } } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float outer = Mathf.Min(r.width, r.height) * 0.5f;
        if (outer <= 0f) return;

        float f = Mathf.Max(0f, feather);
        float inner = filled ? 0f : Mathf.Max(0f, outer - Mathf.Max(0.5f, thickness));

        // 4 วงซ้อน: ขอบในจาง → เนื้อเส้นใน → เนื้อเส้นนอก → ขอบนอกจาง
        float[] radii = { Mathf.Max(0f, inner - f), inner, outer, outer + f };
        Color32 solid = color;
        Color32 clear = color; clear.a = 0;
        Color32[] cols = { filled ? solid : clear, solid, solid, clear };
        int rings = radii.Length;

        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            for (int k = 0; k < rings; k++)
                vh.AddVert(c + dir * radii[k], cols[k], Vector4.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int a0 = i * rings, b0 = (i + 1) * rings;
            for (int k = 0; k < rings - 1; k++)
            {
                vh.AddTriangle(a0 + k, a0 + k + 1, b0 + k + 1);
                vh.AddTriangle(a0 + k, b0 + k + 1, b0 + k);
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
