using UnityEngine;
using UnityEngine.UI;

public class StalkerJumpscareUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject jumpscareCanvas;
    [Tooltip("ใส่ UI Slider ที่เป็นหลอดแนวตั้ง")]
    public Slider struggleBar;

    [Header("Hands Animation (ภาพมือผี)")]
    public RectTransform leftHand;
    public RectTransform rightHand;
    [Tooltip("ตำแหน่งแกน Y ของมือตอนหลอดเต็ม (ดันผีออกไปได้สำเร็จ)")]
    public float handsYMax = 100f;
    [Tooltip("ตำแหน่งแกน Y ของมือตอนหลอดใกล้หมด (ผีล้วงเข้ามาใกล้จอสุดๆ)")]
    public float handsYMin = -300f;

    [Header("Settings")]
    public float startingValue = 50f;
    public float maxValue = 100f;

    private StalkerAI currentBoss;
    private bool isMinigameActive = false;
    private float currentValue;

    void Start()
    {
        if (jumpscareCanvas != null) jumpscareCanvas.SetActive(false);
    }

    public void StartMinigame(StalkerAI boss)
    {
        currentBoss = boss;
        currentValue = startingValue;

        struggleBar.maxValue = maxValue;
        struggleBar.value = currentValue;

        jumpscareCanvas.SetActive(true);
        isMinigameActive = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!isMinigameActive || currentBoss == null) return;

        // 1. หลอดลดลงเรื่อยๆ
        currentValue -= currentBoss.baseHpDrainRate * Time.deltaTime;

        // 2. คลิกเพื่อเพิ่มหลอด
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(currentBoss.requiredInputButton))
        {
            currentValue += currentBoss.drainReductionPerPress;
        }

        // ล็อกค่าไม่ให้ทะลุ
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
        struggleBar.value = currentValue;

        // 3. 💥 อัปเดตตำแหน่งมือผีให้ขยับตามหลอด
        UpdateHandsPosition();

        // 4. เช็กแพ้ชนะ
        if (currentValue >= maxValue)
        {
            EndMinigame(true); // ชนะ
        }
        else if (currentValue <= 0f)
        {
            EndMinigame(false); // แพ้
        }
    }

    // ฟังก์ชันคำนวณการเลื่อนมือ
    private void UpdateHandsPosition()
    {
        if (leftHand == null || rightHand == null) return;

        // หาเปอร์เซ็นต์ของหลอด (จะได้ค่า 0.0 ถึง 1.0)
        float percent = currentValue / maxValue;

        // แปลงเปอร์เซ็นต์ให้เป็นตำแหน่งแกน Y ระหว่าง Min กับ Max
        // ถ้า percent = 1 (หลอดเต็ม) มือจะอยู่ที่ handsYMax
        // ถ้า percent = 0 (หลอดหมด) มือจะตกลงมาที่ handsYMin
        float targetY = Mathf.Lerp(handsYMin, handsYMax, percent);

        // เซ็ตค่าพิกัดให้มือขยับขึ้นลง (แกน X อยู่ที่เดิม แกน Y เปลี่ยนไป)
        leftHand.anchoredPosition = new Vector2(leftHand.anchoredPosition.x, targetY);
        rightHand.anchoredPosition = new Vector2(rightHand.anchoredPosition.x, targetY);
    }

    private void EndMinigame(bool isWin)
    {
        isMinigameActive = false;
        jumpscareCanvas.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentBoss.OnJumpscareEnd(isWin);
    }
}