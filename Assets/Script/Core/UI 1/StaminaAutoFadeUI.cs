using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [ข้อ 7] หลอด Stamina แบบอัตโนมัติ — โผล่เมื่อค่าลดลง/กำลังฟื้น และค่อยๆ จางหายเมื่อเต็ม
/// แปะที่ "ตัวแม่ของหลอด Stamina" ใน Canvas (จะเพิ่ม CanvasGroup ให้อัตโนมัติ)
/// ต้องใช้ PlayerMovement เวอร์ชันที่มี OnStaminaChanged / CurrentStamina แล้ว
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class StaminaAutoFadeUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("เว้นว่างได้ จะหาในซีนให้เอง")]
    public PlayerMovement playerMovement;
    [Tooltip("เว้นว่างได้ ถ้าเป็น Slider ตัวเดียวกับที่ใส่ใน PlayerMovement อยู่แล้ว")]
    public Slider staminaSlider;

    [Header("Fade Settings")]
    [Tooltip("รอกี่วินาทีหลัง Stamina เต็ม ก่อนเริ่มจางหาย")]
    public float hideDelay = 1.0f;
    [Tooltip("ความเร็ว Fade เข้า-ออก (หน่วย alpha ต่อวินาที)")]
    public float fadeSpeed = 5.0f;
    [Tooltip("เริ่มเกมมาให้ซ่อนหลอดไว้ก่อน")]
    public bool hideOnStart = true;

    private const float FullThreshold = 0.05f;

    private CanvasGroup canvasGroup;
    private float fullTimer;
    private float targetAlpha;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();
    }

    private void OnEnable()
    {
        if (playerMovement != null)
            playerMovement.OnStaminaChanged += HandleStaminaChanged;
    }

    private void OnDisable()
    {
        if (playerMovement != null)
            playerMovement.OnStaminaChanged -= HandleStaminaChanged;
    }

    private void Start()
    {
        if (playerMovement == null)
        {
            Debug.LogWarning("[StaminaAutoFadeUI] ไม่พบ PlayerMovement ในซีน", this);
            return;
        }

        if (staminaSlider != null)
        {
            staminaSlider.maxValue = playerMovement.maxStamina;
            staminaSlider.value = playerMovement.CurrentStamina;
        }

        targetAlpha = hideOnStart ? 0f : 1f;
        canvasGroup.alpha = targetAlpha;
    }

    private void HandleStaminaChanged(float current, float max)
    {
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = max;
            staminaSlider.value = current;
        }

        // ยังไม่เต็ม (กำลังลด หรือ กำลังฟื้น) → โชว์หลอด
        if (current < max - FullThreshold)
        {
            targetAlpha = 1f;
            fullTimer = 0f;
        }
    }

    private void Update()
    {
        if (playerMovement == null) return;

        // เต็มแล้ว → นับเวลาก่อนสั่งจางหาย
        if (playerMovement.CurrentStamina >= playerMovement.maxStamina - FullThreshold)
        {
            fullTimer += Time.unscaledDeltaTime;
            if (fullTimer >= hideDelay) targetAlpha = 0f;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
    }
}
