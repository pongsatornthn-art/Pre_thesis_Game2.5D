using UnityEngine;
using UnityEngine.UI;

public class PTSDCircularMinigameUI : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject minigamePanel;
    [Tooltip("ใส่ Empty GameObject ที่เป็นจุดศูนย์กลางของลูกศร")]
    public RectTransform cursorPivot;
    [Tooltip("ใส่ Empty GameObject ที่เป็นจุดศูนย์กลางของโซนสีเขียว")]
    public RectTransform targetZonePivot;
    [Tooltip("ใส่ Image ของโซนสีเขียว (ต้องตั้ง Image Type เป็น Filled)")]
    public Image targetZoneImage;

    [Header("Controller Reference")]
    public PTSDMinigameController minigameController;

    [Header("Gauge Settings (ตั้งค่าองศาเกจ)")]
    [Tooltip("องศาเริ่มต้น (ค่า 0) เช่น 90 คือเริ่มจากซ้าย, 180 คือเริ่มจากซ้ายล่าง")]
    public float startAngle = 90f;
    [Tooltip("องศาสิ้นสุด (ค่า 100) เช่น -90 คือจบที่ขวา, 0 คือจบที่ขวาล่าง")]
    public float endAngle = -90f;

    private void Start()
    {
        if (minigameController != null)
        {
            minigameController.OnMinigameStart.AddListener(ShowUI);
            minigameController.OnMinigameSuccess.AddListener(HideUI);
            minigameController.OnMinigameFailed.AddListener(HideUI);
            minigameController.OnMinigameCanceled.AddListener(HideUI);
            HideUI();
        }
    }

    private void Update()
    {
        if (minigameController != null && minigameController.isMinigameActive)
        {
            UpdateCursor();
            UpdateTargetZone();
        }
    }

    private void ShowUI() { if (minigamePanel != null) minigamePanel.SetActive(true); }
    private void HideUI() { if (minigamePanel != null) minigamePanel.SetActive(false); }

    private void UpdateCursor()
    {
        if (cursorPivot == null) return;

        float normalizedValue = minigameController.GetCurrentGaugeValue() / 100f;
        float currentAngle = Mathf.Lerp(startAngle, endAngle, normalizedValue);

        cursorPivot.localEulerAngles = new Vector3(0, 0, currentAngle);
    }

    private void UpdateTargetZone()
    {
        if (targetZonePivot == null || targetZoneImage == null) return;

        float minZone = minigameController.targetZoneMin;
        float maxZone = minigameController.targetZoneMax;

        float zoneStartAngle = Mathf.Lerp(startAngle, endAngle, minZone / 100f);
        targetZonePivot.localEulerAngles = new Vector3(0, 0, zoneStartAngle);

        float totalAngleSpread = Mathf.Abs(startAngle - endAngle);
        float zoneWidthPercent = (maxZone - minZone) / 100f;

        targetZoneImage.fillAmount = (totalAngleSpread * zoneWidthPercent) / 360f;
    }
}