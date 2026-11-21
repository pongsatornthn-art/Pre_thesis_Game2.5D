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

    [HideInInspector] public bool isPlayerInSafeZone = false;

    [Header("UI Panel หลัก")]
    [Tooltip("ลาก UI Panel ทั้งก้อนมาใส่ (ที่เป็นตัวแม่ของชิ้นส่วนทั้งหมด)")]
    public GameObject chargeUIPanel;

    [Header("ชิ้นส่วน UI ทั้ง 4 ชิ้น")]
    [Tooltip("1. กรอบวงแหวนทองเหลืองด้านนอก (ring_frame)")]
    public Image ringFrameImage;
    [Tooltip("2. วงแหวนหลอดโหลดสีแดง (red_ring_ful — ต้องตั้ง Image Type = Filled / Radial 360)")]
    public Image fillGaugeImage;
    [Tooltip("3. รูปคำว่า Hold (text_hold)")]
    public Image holdTextImage;
    [Tooltip("4. รูปปุ่ม SPACE (key_space)")]
    public Image spaceKeyImage;

    [Header("UI Warning (ล็อคการซ่อนตัว Type B)")]
    [Tooltip("ลาก Text แจ้งเตือนสีแดง (เช่น 'สับสนเกินไป ต้องแก้ปริศนา') มาใส่")]
    public GameObject warningTextObj;

    [Header("Key Press Feedback (ลูกเล่นตอนกดปุ่ม)")]
    [Tooltip("ขนาดปุ่ม SPACE ตอนถูกกดค้าง (0.9 = ยุบลง 10%)")]
    [Range(0.7f, 1f)] public float pressedKeyScale = 0.9f;
    [Tooltip("สีปุ่ม SPACE ตอนถูกกดค้าง")]
    public Color pressedKeyTint = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Events (สำหรับส่งไปบอกระบบอื่น)")]
    [Tooltip("ใส่ Event สั่งเปิดหน้าต่างมินิเกมของจริง หรือตัดเข้าฉากมินิเกม")]
    public UnityEvent OnChargeComplete;

    private Vector3 defaultKeyScale = Vector3.one;

    private void Awake()
    {
        if (spaceKeyImage != null)
        {
            defaultKeyScale = spaceKeyImage.rectTransform.localScale;
        }
    }

    private void Start()
    {
        if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
        if (warningTextObj != null) warningTextObj.SetActive(false);
        UpdateUI();
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

        // ถ้าอยู่ในโหมด PTSD แต่ "ยังไม่เข้า SafeZone" ให้ปิด UI ซ่อนไว้ก่อน
        if (!isPlayerInSafeZone)
        {
            if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
            currentCharge = 0f;
            isCharging = false;
            UpdateUI();
            return;
        }

        // ถ้าเข้า SafeZone แล้ว ถึงจะยอมเปิด UI ให้เห็น
        if (chargeUIPanel != null && !chargeUIPanel.activeSelf)
        {
            chargeUIPanel.SetActive(true);
            SetPromptVisible(true);
            if (warningTextObj != null) warningTextObj.SetActive(false);
        }

        HandleCharging();
        UpdateUI();
    }

    private void HandleCharging()
    {
        if (PTSDManager.Instance.currentMode == PTSDMode.Narrative_TypeB)
        {
            isCharging = false;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (warningTextObj != null) warningTextObj.SetActive(true);
                SetPromptVisible(false);
                Debug.LogWarning("จิตใจของคุณสับสนเกินกว่าจะสงบสติอารมณ์ได้... ต้องแก้ปริศนาเท่านั้น!");
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                if (warningTextObj != null) warningTextObj.SetActive(false);
                SetPromptVisible(true);
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
        // อัปเดตวงแหวนโหลดสีแดง
        if (fillGaugeImage != null)
        {
            fillGaugeImage.fillAmount = timeToFill > 0f ? (currentCharge / timeToFill) : 0f;
        }

        // อัปเดตเอฟเฟกต์ปุ่ม SPACE ยุบตัวตอนกดค้าง
        if (spaceKeyImage != null)
        {
            spaceKeyImage.rectTransform.localScale = isCharging ? (defaultKeyScale * pressedKeyScale) : defaultKeyScale;
            spaceKeyImage.color = isCharging ? pressedKeyTint : Color.white;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (holdTextImage != null) holdTextImage.gameObject.SetActive(visible);
        if (spaceKeyImage != null) spaceKeyImage.gameObject.SetActive(visible);
    }

    private void TriggerMinigame()
    {
        hasTriggeredMinigame = true;
        isCharging = false;
        UpdateUI();

        Debug.Log("ชาร์จเต็มแล้ว! ตัดเข้ามินิเกมของจริง!");
        OnChargeComplete?.Invoke();

        if (chargeUIPanel != null) chargeUIPanel.SetActive(false);
    }

    public void ResetCharge()
    {
        currentCharge = 0f;
        isCharging = false;
        hasTriggeredMinigame = false;
        isPlayerInSafeZone = false;
        SetPromptVisible(true);
        UpdateUI();
    }
}