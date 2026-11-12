using UnityEngine;

/// <summary>
/// ไฟเทียน — ความสว่างไหวเบาๆ แบบเปลวเทียน (Perlin noise)
/// แปะบน GameObject ที่มี Light (พรีแฟบ Furn_Candelabra) · ไม่ผูกกับ WorldLighting
/// (เทียนไม่ได้อยู่ใต้กลุ่ม Lights → โลก PTSD ไม่ย้อมแดง/ไม่ดับตามบอส)
/// [Claude 2026-11-12]
/// </summary>
[RequireComponent(typeof(Light))]
public class CandleFlicker : MonoBehaviour
{
    [Tooltip("ไหวมากแค่ไหน (สัดส่วนของความสว่างตั้งต้น)")]
    [SerializeField, Range(0f, 0.6f)] private float amount = 0.25f;
    [Tooltip("เร็วแค่ไหน")]
    [SerializeField, Min(0.1f)] private float speed = 6f;

    private Light candleLight;
    private float baseIntensity;
    private float seed;

    private void Awake()
    {
        candleLight = GetComponent<Light>();
        baseIntensity = candleLight.intensity;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;   // -0.5 .. 0.5
        candleLight.intensity = baseIntensity * (1f + n * 2f * amount);
    }
}
