using UnityEngine;
using UnityEngine.Events;

public class MiniGameInteractable : MonoBehaviour
{
    [Header("Visual Feedback")]
    public GameObject glowEffect;

    [Header("Dialogue System")]
    [TextArea]
    public string dialogueHint;

    [Header("Puzzle Action")]
    public UnityEvent onInteract;

    void Start()
    {
        if (glowEffect != null) glowEffect.SetActive(false);
    }

    public void OnHoverEnter()
    {
        if (glowEffect != null) glowEffect.SetActive(true);
    }

    public void OnHoverExit()
    {
        if (glowEffect != null) glowEffect.SetActive(false);
    }

    public void OnClick()
    {
        if (!string.IsNullOrEmpty(dialogueHint))
        {
            Debug.Log($"[Dialogue Popup]: {dialogueHint}");
        }

        onInteract?.Invoke();
    }
}