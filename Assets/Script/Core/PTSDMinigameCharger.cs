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

    [Header("Events (สำหรับส่งไปบอกระบบอื่น)")]
    [Tooltip("ใส่ Event สั่งเปิดหน้าต่างมินิเกมของจริง หรือตัดเข้าฉากมินิเกม")]
    public UnityEvent OnChargeComplete;

    private void Start()
    {
        if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
    }

    private void Update()
    {
        if (PTSDManager.Instance == null || !PTSDManager.Instance.isPTSDActive)
        {
            if (chargeUIPanel != null && chargeUIPanel.activeSelf)
            {
                chargeUIPanel.SetActive(false);
                ResetCharge();
            }
            return;
        }

        if (hasTriggeredMinigame) return;

        if (chargeUIPanel != null && !chargeUIPanel.activeSelf)
        {
            chargeUIPanel.SetActive(true);
        }

        HandleCharging();
        UpdateUI();
    }

    private void HandleCharging()
    {
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