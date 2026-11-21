using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-11-21] ไฟส่องติดตัวผู้เล่น — ค่าต่างกันตามโลก (งาน 1.4)
/// แปะที่ไฟดวงที่ติดตัวผู้เล่น (ซีนนี้ = Player/GFX/Player_Light)
///   โลกปกติ = สว่างกว่า ขาวอุ่น · โลก PTSD = หรี่ลง อุ่นออกส้ม (ให้ไฟแดงของบ้านยังเด่น แต่ผู้เล่นยังเห็นรอบตัว)
/// เปลี่ยนตอนฉากสลับจริง (WorldVisualsSwappedEvent — กลางจอมืด) แบบค่อยๆ เฟด เหมือน WorldLighting
/// </summary>
[RequireComponent(typeof(Light))]
public class PlayerLightByWorld : MonoBehaviour
{
    [System.Serializable]
    public struct Look
    {
        [Min(0f)] public float intensity;
        [Min(0f)] public float range;
        public Color color;
    }

    [Header("โลกปกติ")]
    [SerializeField] private Look real = new Look { intensity = 1.2f, range = 4f, color = new Color(1f, 0.9f, 0.72f) };

    [Header("โลก PTSD")]
    [SerializeField] private Look ptsd = new Look { intensity = 1.0f, range = 3.5f, color = new Color(1f, 0.78f, 0.5f) };

    [Tooltip("วินาทีที่ใช้ค่อยๆ เปลี่ยน")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.6f;

    private Light lightComp;
    private Coroutine running;

    private void Awake() => lightComp = GetComponent<Light>();

    private void OnEnable() => GameEventBus.Subscribe<WorldVisualsSwappedEvent>(OnSwapped);
    private void OnDisable() => GameEventBus.Unsubscribe<WorldVisualsSwappedEvent>(OnSwapped);

    private void Start()
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        Set(world != null && world.VisibleWorld != WorldMode.Real ? ptsd : real);
    }

    private void OnSwapped(WorldVisualsSwappedEvent e)
    {
        Look to = e.Visible == WorldMode.Real ? real : ptsd;
        if (running != null) StopCoroutine(running);
        if (fadeSeconds <= 0f || !isActiveAndEnabled) Set(to);
        else running = StartCoroutine(Fade(to));
    }

    private IEnumerator Fade(Look to)
    {
        Look from = new Look { intensity = lightComp.intensity, range = lightComp.range, color = lightComp.color };
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeSeconds;
            lightComp.intensity = Mathf.Lerp(from.intensity, to.intensity, k);
            lightComp.range = Mathf.Lerp(from.range, to.range, k);
            lightComp.color = Color.Lerp(from.color, to.color, k);
            yield return null;
        }
        Set(to);
        running = null;
    }

    private void Set(Look l)
    {
        lightComp.intensity = l.intensity;
        lightComp.range = l.range;
        lightComp.color = l.color;
    }
}
