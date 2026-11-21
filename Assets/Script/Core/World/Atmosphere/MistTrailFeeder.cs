using UnityEngine;

/// <summary>
/// [2026-11-21] ส่ง "รอยเท้า" ผู้เล่นให้หมอก (shader PTSD/VolumeMist) → หมอกยุบเฉพาะจุดที่เดินผ่าน แล้วค่อยๆ ไหลกลับ
/// แปะที่ PTSD_Atmosphere (ตัวแม่ของแผ่นหมอก) ตัวเดียวพอ — ไม่สร้างของใดๆ ตอนเล่น แค่ส่งตัวเลขเข้า shader
///
/// เก็บตำแหน่งย้อนหลังสูงสุด 12 จุด (ทุกครั้งที่เดินไปได้ระยะหนึ่ง) · จุดเก่าค่อยๆ อ่อนแรงลง = หมอกค่อยๆ กลับมาปิดรอย
/// </summary>
public class MistTrailFeeder : MonoBehaviour
{
    private const int MaxPoints = 12;   // ต้องตรงกับ MIST_TRAIL_MAX ใน shader

    [Tooltip("เดินไปได้กี่เมตร ถึงวางรอยใหม่")]
    [SerializeField, Min(0.05f)] private float stepDistance = 0.35f;

    [Tooltip("รอยเก่าค่อยๆ หายไปในกี่วินาที (หมอกไหลกลับมาปิด)")]
    [SerializeField, Min(0.1f)] private float trailLife = 2.5f;

    private static readonly int TrailId = Shader.PropertyToID("_MistTrail");
    private static readonly int CountId = Shader.PropertyToID("_MistTrailCount");

    private readonly Vector4[] points = new Vector4[MaxPoints];   // xyz = ตำแหน่ง · w = เวลาที่วาง
    private readonly Vector4[] upload = new Vector4[MaxPoints];
    private int head;
    private Vector3 lastDrop = new Vector3(float.MaxValue, 0f, 0f);
    private Transform player;

    private void OnDisable()
    {
        Shader.SetGlobalFloat(CountId, 0f);
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) { Shader.SetGlobalFloat(CountId, 0f); return; }
            player = p.transform;
        }

        Vector3 pos = player.position;
        float now = Time.time;

        Vector3 flat = pos - lastDrop; flat.y = 0f;
        if (flat.sqrMagnitude >= stepDistance * stepDistance)
        {
            points[head] = new Vector4(pos.x, pos.y, pos.z, now);
            head = (head + 1) % MaxPoints;
            lastDrop = pos;
        }

        // ช่องแรก = ตัวผู้เล่นตอนนี้ (แรงเต็ม) · ที่เหลือ = รอยเก่าที่อ่อนลงตามเวลา
        int n = 0;
        upload[n++] = new Vector4(pos.x, pos.y, pos.z, 1f);
        for (int i = 0; i < MaxPoints && n < MaxPoints; i++)
        {
            Vector4 pt = points[i];
            if (pt.w <= 0f) continue;
            float k = 1f - (now - pt.w) / trailLife;
            if (k <= 0f) continue;
            upload[n++] = new Vector4(pt.x, pt.y, pt.z, k * k * (3f - 2f * k));   // ค่อยๆ อ่อน แบบนุ่ม
        }
        for (int i = n; i < MaxPoints; i++) upload[i] = Vector4.zero;

        Shader.SetGlobalVectorArray(TrailId, upload);
        Shader.SetGlobalFloat(CountId, n);
    }
}
