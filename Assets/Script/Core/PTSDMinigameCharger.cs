using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class PTSDMinigameCharger : MonoBehaviour
{
    [Header("Charge Settings")]
    [Tooltip("ใช้เวลากดค้างกี่วินาทีหลอดถึงจะเต็ม")]
    public float timeToFill = 2.0f;
    [Tooltip("เวลาปล่อยปุ่ม หลอดจะลดลงเร็วแค่ไหน (ยิ่งเยอะยิ่งลดเร็ว)")]
    public float decayMultiplier = 1.5f;

    private float currentCharge = 0f;
    private bool isCharging = false;
    private bool hasTriggeredMinigame = false;

    // 🌟 เพิ่มตัวแปรเช็คว่าอยู่ใน SafeZone หรือยัง
    [HideInInspector] public bool isPlayerInSafeZone = false;

    [Header("UI References")]
    [Tooltip("ลาก UI Panel ทั้งก้อนมาใส่ (ที่มีหลอดกับข้อความเตือน)")]
    public GameObject chargeUIPanel;
    [Tooltip("ลาก Image หลอดสีเขียว (ตั้งค่าเป็น Filled 360) มาใส่")]
    public Image fillGaugeImage;
    [Tooltip("ลากออบเจกต์ข้อความ Hold [SPACE] (PromptText) มาใส่ที่นี่")]
    public GameObject promptTextObj;

    [Header("UI Warning (ล็อคการซ่อนตัว Type B)")]
    [Tooltip("ลาก Text แจ้งเตือนสีแดง (เช่น 'สับสนเกินไป ต้องแก้ปริศนา') มาใส่")]
    public GameObject warningTextObj;

    [Header("Events (สำหรับส่งไปบอกระบบอื่น)")]
    [Tooltip("ใส่ Event สั่งเปิดหน้าต่างมินิเกมของจริง หรือตัดเข้าฉากมินิเกม")]
    public UnityEvent OnChargeComplete;

    private void Start()
    {
        if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
        if (warningTextObj != null) warningTextObj.SetActive(false);
    }

    private void Update()
    {
        // ถ้าวิทยุกลางพัง หรือไม่ได้อยู่ในสถานะ PTSD ให้ปิด UI ทิ้งให้หมด
        if (PTSDManager.Instance == null || PTSDManager.Instance.currentMode == PTSDMode.None)
        {
            if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
            if (warningTextObj != null) warningTextObj.SetActive(false);
            ResetCharge();
            return;
        }

        // ถ้าเล่นมินิเกมผ่านไปแล้ว ก็ไม่ต้องทำอะไรต่อ
        if (hasTriggeredMinigame) return;

        // 🌟 ถ้ายินอยู่ในโหมด PTSD แต่ "ยังไม่เข้า SafeZone" ก็ปิด UI ซ่อนไว้ก่อน
        if (!isPlayerInSafeZone)
        {
            if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
            currentCharge = 0f; // ไม่ให้แอบชาร์จตอนอยู่นอกโซน
            return;
        }

        // 🌟 ถ้าเข้า SafeZone แล้ว ถึงจะยอมเปิด UI ให้เห็น
        if (chargeUIPanel != null && !chargeUIPanel.activeSelf)
        {
            chargeUIPanel.SetActive(true);
            if (promptTextObj != null) promptTextObj.SetActive(true);
        }

        HandleCharging();
        UpdateUI();
    }

    private void HandleCharging()
    {
        if (PTSDManager.Instance.currentMode == PTSDMode.Narrative_TypeB)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (warningTextObj != null) warningTextObj.SetActive(true);
                if (promptTextObj != null) promptTextObj.SetActive(false);
                Debug.LogWarning("จิตใจของคุณสับสนเกินกว่าจะสงบสติอารมณ์ได้... ต้องแก้ปริศนาเท่านั้น!");
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                if (warningTextObj != null) warningTextObj.SetActive(false);
                if (promptTextObj != null) promptTextObj.SetActive(true);
            }

            currentCharge = 0f;
            return;
        }

        // โหมด Type A ปกติ อนุญาตให้ชาร์จ
        if (Input.GetKey(KeyCode.Space))
        {
            isCharging = true;
            currentCharge += Time.deltaTime;

            if (currentCharge >= timeToFill)
            {
                currentCharge = timeToFill;
                TriggerMinigame();
            }
        }
        else
        {
            isCharging = false;
            currentCharge -= Time.deltaTime * decayMultiplier;
            if (currentCharge < 0f) currentCharge = 0f;
        }
    }

    private void UpdateUI()
    {
        if (fillGaugeImage != null)
        {
            fillGaugeImage.fillAmount = currentCharge / timeToFill;
        }
    }

    private void TriggerMinigame()
    {
        hasTriggeredMinigame = true;
        Debug.Log("ชาร์จเต็มแล้ว! ตัดเข้ามินิเกมของจริง!");

        OnChargeComplete?.Invoke();

        if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
    }

    public void ResetCharge()
    {
        currentCharge = 0f;
        hasTriggeredMinigame = false;
        UpdateUI();
    }
}