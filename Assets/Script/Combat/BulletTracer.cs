using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BulletTracer : MonoBehaviour
{
    [Header("Tracer Settings")]
    public float fadeDuration = 0.1f; // ระยะเวลาที่เส้นจะโชว์ก่อนจางหายไป (ตาม GDD)

    private LineRenderer lineRenderer;

    public void Setup(Vector3 startPoint, Vector3 endPoint)
    {
        lineRenderer = GetComponent<LineRenderer>();

        // กำหนดจุดเริ่มและจุดจบของเส้นกระสุน
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // เริ่มกระบวนการจางหาย
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        Color startColor = lineRenderer.startColor;
        Color endColor = lineRenderer.endColor;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            // คำนวณความโปร่งใส (Alpha) จาก 1 ไป 0
            float percent = 1f - (timer / fadeDuration);

            Color newStart = new Color(startColor.r, startColor.g, startColor.b, startColor.a * percent);
            Color newEnd = new Color(endColor.r, endColor.g, endColor.b, endColor.a * percent);

            lineRenderer.startColor = newStart;
            lineRenderer.endColor = newEnd;

            yield return null; // รอให้จบเฟรมแล้วทำต่อ
        }

        // พอกระสุนจางจนใสแล้ว ก็ลบทิ้งเลยเพื่อคืนหน่วยความจำ
        Destroy(gameObject);
    }
}