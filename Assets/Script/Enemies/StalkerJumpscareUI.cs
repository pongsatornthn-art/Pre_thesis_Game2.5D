using UnityEngine;
using UnityEngine.UI;

public class StalkerJumpscareUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject jumpscareCanvas;
    [Tooltip("ใส่ UI Slider ที่เป็นหลอดแนวตั้ง")]
    public Slider struggleBar;

    [Header("Screen Shake (ภาพฉาก Jumpscare ที่จะให้สั่น)")]
    [Tooltip("ลาก RectTransform ของภาพฉากโดนบีบคอมาใส่ตรงนี้")]
    public RectTransform shakeTarget;
    [Tooltip("แรงสั่นพื้นฐานตลอดเวลา (แนะนำ 1.5 - 2)")]
    public float baseShakeIntensity = 2f;
    [Tooltip("แรงสั่นเพิ่มพิเศษตอนใกล้แพ้")]
    public float lowHpExtraShake = 4f;
    [Tooltip("แรงกระตุกตอนกดคลิกสู้ (แนะนำ 6 - 8)")]
    public float pressShakeBurst = 6f;
    [Tooltip("ความเร็วในการลดแรงกระตุกหลังกดปุ่ม")]
    public float burstDecaySpeed = 60f;

    [Header("Click Settings (ปรับความแรงในการคลิก)")]
    [Tooltip("ตัวคูณเพิ่มแรงกดคลิก (ยิ่งเยอะ ยิ่งกดแล้วหลอดพุ่งไว)")]
    public float clickPowerMultiplier = 2.5f;

    [Header("Settings")]
    public float startingValue = 50f;
    public float maxValue = 100f;

    private StalkerAI currentBoss;
    private bool isMinigameActive = false;
    private float currentValue;
    private CursorLockMode savedLock = CursorLockMode.Confined;
    private bool savedVisible = true;

    private Vector2 originalAnchoredPos;
    private float currentPressBurst = 0f;

    void Awake()
    {
        if (shakeTarget != null)
        {
            originalAnchoredPos = shakeTarget.anchoredPosition;
        }
    }

    void Start()
    {
        if (jumpscareCanvas != null) jumpscareCanvas.SetActive(false);
    }

    public void StartMinigame(StalkerAI boss)
    {
        currentBoss = boss;
        currentValue = startingValue;
        currentPressBurst = 0f;

        if (shakeTarget != null)
        {
            shakeTarget.anchoredPosition = originalAnchoredPos;
        }

        struggleBar.maxValue = maxValue;
        struggleBar.value = currentValue;

        jumpscareCanvas.SetActive(true);
        isMinigameActive = true;

        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!isMinigameActive || currentBoss == null) return;

        // 1. หลอดลดลงเรื่อยๆ ตามค่าของบอส
        currentValue -= currentBoss.baseHpDrainRate * Time.deltaTime;

        // 2. คลิกเพื่อเพิ่มหลอด (คูณเพิ่มด้วย clickPowerMultiplier ให้หลอดขึ้นสะใจขึ้น)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(currentBoss.requiredInputButton))
        {
            currentValue += currentBoss.drainReductionPerPress * clickPowerMultiplier;
            currentPressBurst = pressShakeBurst;
        }

        // ล็อกค่าไม่ให้ทะลุขอบ
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
        struggleBar.value = currentValue;

        // 3. อัปเดตเอฟเฟกต์จอสั่นเบาๆ ไม่ให้เวียนหัว
        UpdateScreenShake();

        // 4. เช็กแพ้ชนะ
        if (currentValue >= maxValue)
        {
            EndMinigame(true); // ชนะ (ผลักผีออก)
        }
        else if (currentValue <= 0f)
        {
            EndMinigame(false); // แพ้ (เลือดลด/โดนทำดาเมจ)
        }
    }

    private void UpdateScreenShake()
    {
        if (shakeTarget == null) return;

        if (currentPressBurst > 0f)
        {
            currentPressBurst = Mathf.MoveTowards(currentPressBurst, 0f, burstDecaySpeed * Time.deltaTime);
        }

        float percent = maxValue > 0f ? (currentValue / maxValue) : 0f;
        float dangerRatio = 1f - percent;

        float totalShake = baseShakeIntensity + (lowHpExtraShake * dangerRatio) + currentPressBurst;

        Vector2 randomOffset = Random.insideUnitCircle * totalShake;
        shakeTarget.anchoredPosition = originalAnchoredPos + randomOffset;
    }

    private void EndMinigame(bool isWin)
    {
        isMinigameActive = false;

        if (shakeTarget != null)
        {
            shakeTarget.anchoredPosition = originalAnchoredPos;
        }

        jumpscareCanvas.SetActive(false);

        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;

        currentBoss.OnJumpscareEnd(isWin);
    }
}