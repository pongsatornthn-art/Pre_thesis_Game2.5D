using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ทำของค่อยๆ จางหาย (หรือค่อยๆ โผล่) — เช่น รูปวาดจางหายแล้วเห็นกุญแจข้างหลัง
/// ใช้คู่กับ StoryFlagListener แบบเดียวกับ SimpleMover:
///   onFlagSet → SimpleFader.Play   ·   onAlreadySetAtStart → SimpleFader.JumpToEnd (โหลดเซฟแล้วไม่เล่นซ้ำ)
///
/// รองรับ SpriteRenderer (สไปรท์) · CanvasGroup (UI) · Renderer อื่นที่ material เป็นแบบ Transparent
/// ไม่รู้จักระบบเนื้อเรื่อง — เอาไปใช้กับอะไรก็ได้ที่อยากให้จาง
/// </summary>
public class SimpleFader : MonoBehaviour
{
    public enum Direction { FadeOut, FadeIn }

    [SerializeField] private Direction direction = Direction.FadeOut;
    [SerializeField, Min(0f)] private float duration = 1.5f;

    [Tooltip("จางจบแล้วปิด object นี้ไปเลย (เฉพาะ FadeOut) — ตัวชน/ป้ายกดจะได้หายตามด้วย")]
    [SerializeField] private bool deactivateWhenFadedOut = true;

    [Tooltip("ทำงานตอนจางเสร็จ เช่น เปิดกุญแจที่ซ่อนอยู่ข้างหลัง (GameObject.SetActive ติ๊กถูก)")]
    public UnityEvent onFinished;

    private Coroutine running;

    /// <summary>เล่นการจาง</summary>
    public void Play()
    {
        if (!isActiveAndEnabled) { JumpToEnd(); return; }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FadeRoutine());
    }

    /// <summary>ข้ามไปสภาพปลายทางทันที (ใช้ตอนโหลดเซฟ)</summary>
    public void JumpToEnd()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        SetAlpha(direction == Direction.FadeOut ? 0f : 1f);
        Finish();
    }

    private IEnumerator FadeRoutine()
    {
        float from = direction == Direction.FadeOut ? 1f : 0f;
        float to = 1f - from;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, duration > 0f ? t / duration : 1f));
            yield return null;
        }

        running = null;
        SetAlpha(to);
        Finish();
    }

    private void Finish()
    {
        onFinished?.Invoke();
        if (direction == Direction.FadeOut && deactivateWhenFadedOut) gameObject.SetActive(false);
    }

    private void SetAlpha(float a)
    {
        foreach (CanvasGroup g in GetComponentsInChildren<CanvasGroup>(true)) g.alpha = a;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer sr)
            {
                Color c = sr.color; c.a = a; sr.color = c;
            }
            else if (r.material.HasProperty("_BaseColor"))   // URP Lit/Unlit (material ต้องตั้งเป็น Transparent)
            {
                Color c = r.material.GetColor("_BaseColor"); c.a = a; r.material.SetColor("_BaseColor", c);
            }
        }
    }
}
