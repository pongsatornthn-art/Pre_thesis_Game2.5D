using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// กล่องดำทึบครอบ 1 ห้อง — ห้องที่ผู้เล่นไม่ได้อยู่ต้อง "ดำสนิทเท่ากันหมด" (ของ/แสง/กำแพงด้านในไม่หลุดออกมา)
/// วางในซีน (ใต้ Map_Test/RoomCovers) ช่อง Zone = Trigger ของโซนห้องของปอ · RoomVisibility เป็นคนสั่งมืด/สว่าง
/// ใน Editor ปิด Mesh Renderer ไว้ (ไม่บังตอนแก้ฉาก) — ตอนเล่น RoomVisibility เปิดให้เอง
/// ค่อยๆ เฟดดำ/จางได้ (ตอนเล่นปรับวัสดุเป็นโปร่งใสแบบเขียน depth ในหน่วยความจำ — ไม่แตะไฟล์วัสดุ)
/// ไม่มีตัวชน · ไม่ทำเงา · ไม่รับแสง
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class RoomCover : MonoBehaviour
{
    [Tooltip("Trigger ของโซนห้อง (object Test_zone_* ของปอ)")]
    public Collider zone;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private bool prepared;
    private float alpha;
    private float target;

    public MeshRenderer Renderer => meshRenderer != null ? meshRenderer : (meshRenderer = GetComponent<MeshRenderer>());

    /// <summary>ความมืดตอนนี้ (0 = มองทะลุ, 1 = ดำสนิท)</summary>
    public float Alpha => alpha;

    /// <summary>มืด/สว่างทันที (ตอนเริ่มเกม / ปิดระบบ)</summary>
    public void SetDark(bool dark)
    {
        alpha = target = dark ? 1f : 0f;
        ApplyAlpha();
    }

    /// <summary>ตั้งเป้าแล้วค่อยๆ ไป — เรียก Tick ทุกเฟรม</summary>
    public void FadeTo(bool dark) => target = dark ? 1f : 0f;

    public void Tick(float step)
    {
        if (Mathf.Approximately(alpha, target)) return;
        alpha = Mathf.MoveTowards(alpha, target, step);
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        if (!prepared) PrepareMaterial();
        Renderer.enabled = alpha > 0.001f;
        block ??= new MaterialPropertyBlock();
        block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
        Renderer.SetPropertyBlock(block);
    }

    // URP Unlit → โปร่งใส แต่ยังเขียน depth + วาดก่อนของโปร่งใสอื่น
    // (ตอนดำสนิท ผี/พาร์ติเคิลในห้องที่อยู่หลังกล่องจะไม่ทะลุออกมา)
    private void PrepareMaterial()
    {
        Material m = Renderer.material;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 1f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.GeometryLast + 1;
        prepared = true;
    }
}
