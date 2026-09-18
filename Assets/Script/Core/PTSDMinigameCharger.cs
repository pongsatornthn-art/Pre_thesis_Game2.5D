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
        if (warningTextObj != null) warningTextObj.SetActive(false); // ซ่อนคำเตือนสีแดงตอนเริ่มเกม
    }

    private void Update()
    {
        // ถ้าวิทยุกลางพัง หรือไม่ได้อยู่ในสถานะ PTSD (ทุกรูปแบบ) ให้ปิด UI ทิ้งให้หมด
        if (PTSDManager.Instance == null || PTSDManager.Instance.currentMode == PTSDMode.None)
        {
            if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
            if (warningTextObj != null) warningTextObj.SetActive(false);
            ResetCharge();
            return;
        }

        // ถ้าเล่นมินิเกมผ่านไปแล้ว ก็ไม่ต้องทำอะไรต่อ
        if (hasTriggeredMinigame) return;

        // ถ้าเข้าเงื่อนไข PTSD (ไม่ว่า Type A หรือ B) ก็เปิด UI หลอดชาร์จขึ้นมาเตรียมรอ
        if (chargeUIPanel != null && !chargeUIPanel.activeSelf)
        {
            chargeUIPanel.SetActive(true);
            if (promptTextObj != null) promptTextObj.SetActive(true);
        }

        // 🌟 แยกการทำงาน: เช็คการชาร์จหลอด
        HandleCharging();
        // 🌟 อัปเดตภาพหลอดชาร์จให้ยาวขึ้น/หดลง
        UpdateUI();
    }

    private void HandleCharging()
    {
        // 🌟 1. SYSTEM LOCK: ดักเช็คก่อนเลยว่าติดสถานะ Type B หรือเปล่า?
        if (PTSDManager.Instance.currentMode == PTSDMode.Narrative_TypeB)
        {
            // ถ้ากด Spacebar แต่ติด Type B อยู่
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // โชว์ข้อความเตือนสีแดงให้ผู้เล่นรู้ว่า "ห้ามซ่อน!"
                if (warningTextObj != null) warningTextObj.SetActive(true);
                if (promptTextObj != null) promptTextObj.SetActive(false); // ซ่อนข้อความให้กดค้างไปเลย

                Debug.LogWarning("จิตใจของคุณสับสนเกินกว่าจะสงบสติอารมณ์ได้... ต้องแก้ปริศนาเท่านั้น!");
            }

            // ถ้าปล่อยปุ่ม Spacebar ก็ซ่อนข้อความสีแดงกลับไป
            if (Input.GetKeyUp(KeyCode.Space))
            {
                if (warningTextObj != null) warningTextObj.SetActive(false);
                if (promptTextObj != null) promptTextObj.SetActive(true);
            }

            // 🌟 2. เตะออกทันที ไม่ให้หลอดชาร์จเพิ่มขึ้นแม้แต่น้อย
            currentCharge = 0f;
            return;
        }

        // ==========================================
        // 🌟 3. ถ้าเป็น Type A ปกติ ก็อนุญาตให้ชาร์จหลอดได้ตามเดิม
        // ==========================================
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