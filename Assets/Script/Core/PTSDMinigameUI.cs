using UnityEngine;
using UnityEngine.UI;

public class PTSDMinigameUI : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject minigamePanel;
    public RectTransform cursorRect;
    public RectTransform targetZoneRect;
    public RectTransform backgroundRect;

    [Header("Controller Reference")]
    public PTSDMinigameController minigameController;

    private float gaugeWidth;

    private void Start()
    {
        if (backgroundRect != null)
        {
            gaugeWidth = backgroundRect.rect.width;
        }

        if (minigamePanel != null)
        {
            minigamePanel.SetActive(false);
        }

        if (minigameController != null)
        {
            minigameController.OnMinigameStart.AddListener(ShowUI);
            minigameController.OnMinigameSuccess.AddListener(HideUI);
            minigameController.OnMinigameFailed.AddListener(HideUI);
            minigameController.OnMinigameCanceled.AddListener(HideUI);
        }
    }

    private void Update()
    {
        if (minigameController != null && minigameController.isMinigameActive)
        {
            UpdateCursorPosition();
            UpdateTargetZone();
        }
    }

    private void ShowUI()
    {
        if (minigamePanel != null) minigamePanel.SetActive(true);
    }

    private void HideUI()
    {
        if (minigamePanel != null) minigamePanel.SetActive(false);
    }

    private void UpdateCursorPosition()
    {
        if (cursorRect == null || backgroundRect == null) return;

        float normalizedValue = minigameController.GetCurrentGaugeValue() / 100f;

        float targetX = (normalizedValue * gaugeWidth) - (gaugeWidth / 2f);

        cursorRect.anchoredPosition = new Vector2(targetX, cursorRect.anchoredPosition.y);
    }

    private void UpdateTargetZone()
    {
        if (targetZoneRect == null || backgroundRect == null) return;

        float minZone = minigameController.targetZoneMin;
        float maxZone = minigameController.targetZoneMax;

        float zoneWidthPercent = (maxZone - minZone) / 100f;
        targetZoneRect.sizeDelta = new Vector2(gaugeWidth * zoneWidthPercent, targetZoneRect.sizeDelta.y);

        float centerPercent = (minZone + ((maxZone - minZone) / 2f)) / 100f;
        float targetX = (centerPercent * gaugeWidth) - (gaugeWidth / 2f);

        targetZoneRect.anchoredPosition = new Vector2(targetX, targetZoneRect.anchoredPosition.y);
    }
}