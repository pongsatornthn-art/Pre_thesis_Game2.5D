using UnityEngine;

/// <summary>
/// [2026-11-21] ความหนาหมอก "รายห้อง" — แปะที่ตัวแม่ของพรีแฟบ PTSD_VolumeMist_Strip (ทุกแผ่นลูกใช้ค่าเดียวกัน)
/// ทุกห้องใช้วัสดุ M_PTSD_VolumeMist ตัวเดียวกัน แต่ห้องนี้ปรับความหนา/ปริมาณ/ขอบ ของตัวเองได้ โดยไม่กระทบห้องอื่น
/// (ส่งค่าผ่าน MaterialPropertyBlock — ไม่สร้างวัสดุใหม่ ไม่สร้างของในฉาก)
/// </summary>
[ExecuteAlways]
public class MistArea : MonoBehaviour
{
    [Tooltip("ความหนาของหมอกห้องนี้ (0 = ไม่มี · 0.5 = บาง · 1 = หนา)")]
    [SerializeField, Range(0f, 2f)] private float density = 0.6f;

    [Tooltip("ปริมาณ: ยิ่งมาก หมอกยิ่งเป็นหย่อมๆ มีช่องว่าง (0.35 = เกือบเต็มห้อง · 0.55 = เป็นหย่อม)")]
    [SerializeField, Range(0f, 1f)] private float coverage = 0.45f;

    [Tooltip("ขอบแผ่นจาง (เมตร) — ห้อง = น้อย (หมอกถึงผนัง) · ทางเดินต่อห้อง = มาก (กลืนกัน)")]
    [SerializeField, Min(0.05f)] private float edgeFade = 0.8f;

    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
    private MaterialPropertyBlock block;

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void Apply()
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(block);
            block.SetFloat(DensityId, density);
            block.SetFloat(CoverageId, coverage);
            block.SetFloat(EdgeFadeId, edgeFade);
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>ใช้ตอนวางหมอกจากเครื่องมือ (Editor) — ตั้งค่าทีเดียว</summary>
    public void Configure(float newDensity, float newCoverage, float newEdgeFade)
    {
        density = newDensity; coverage = newCoverage; edgeFade = newEdgeFade;
        Apply();
    }
}
