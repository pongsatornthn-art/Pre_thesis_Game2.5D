using UnityEngine;
using TMPro;
using UnityEngine.UI;

public interface IMinigameService
{
    void ShowMinigameUI(KeyCode requiredKey);
    void UpdateMinigameUI(float timeRemaining, float currentHp, float maxHp);
    void HideMinigameUI();
}

public class StalkerMinigameUI : MonoBehaviour, IMinigameService
{
    [Header("UI References")]
    public GameObject minigamePanel;
    public TMP_Text promptText;
    public TMP_Text timerText;
    public Slider hpSlider;

    private void Awake()
    {
        ServiceLocator.Register<IMinigameService>(this);
        if (minigamePanel != null) minigamePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IMinigameService>();
    }

    public void ShowMinigameUI(KeyCode requiredKey)
    {
        if (minigamePanel != null) minigamePanel.SetActive(true);
        if (promptText != null) promptText.text = $"MASH [{requiredKey}] TO ESCAPE!";
    }

    public void UpdateMinigameUI(float timeRemaining, float currentHp, float maxHp)
    {
        if (timerText != null) timerText.text = $"Time: {timeRemaining:F1}s";
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value = currentHp;
        }
    }

    public void HideMinigameUI()
    {
        if (minigamePanel != null) minigamePanel.SetActive(false);
    }
}
