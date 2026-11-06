using UnityEngine;
using UnityEngine.Events;

public class MiniGameInteractable : MonoBehaviour
{
    [Header("Visual Feedback")]
    public GameObject glowEffect;

    [Header("Dialogue System")]
    [TextArea]
    public string dialogueHint;

    [Header("Audio")]
    [Tooltip("ใส่ Component Audio Source ของวัตถุชิ้นนี้ (ถ้าเว้นว่างจะหาเอง)")]
    public AudioSource interactAudioSource;
    [Tooltip("ไฟล์เสียงรายการวิทยุ / เสียงเพลง")]
    public AudioClip interactSound;
    public bool loopSound = true;

    [Header("Puzzle Action")]
    public UnityEvent onInteract;

    void Awake()
    {
        if (interactAudioSource == null)
            interactAudioSource = GetComponent<AudioSource>();

        if (interactAudioSource != null)
        {
            interactAudioSource.playOnAwake = false;
            interactAudioSource.spatialBlend = 0f; // 2D กันเสียงเบา/หายเพราะระยะกล้อง
        }
    }

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

    // เล่นเสียงทันที ไม่เล่นซ้อนถ้ากำลังเล่นอยู่
    public void PlaySound()
    {
        if (interactAudioSource == null || interactSound == null) return;

        if (!interactAudioSource.enabled) interactAudioSource.enabled = true;

        if (interactAudioSource.isPlaying && interactAudioSource.clip == interactSound) return;

        interactAudioSource.clip = interactSound;
        interactAudioSource.loop = loopSound;
        interactAudioSource.Play();
    }

    public void OnClick()
    {
        if (!string.IsNullOrEmpty(dialogueHint))
        {
            Debug.Log($"[Dialogue Popup]: {dialogueHint}");
        }

        PlaySound();
        onInteract?.Invoke();
    }
}