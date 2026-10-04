using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// ตัวแสดงผลบทสนทนาแบบลอยในโลก (WorldSpace Dialogue Presenter)
/// ข้อความลอยข้างตัวละคร ไม่มีกรอบ จางเข้า-ออก หันเข้าหากล้องด้วย Billboard ที่มีอยู่เดิม
/// วาดทับวัตถุและกำแพงด้วย Sorting Order สูง และปรับขนาดกล่องตามความยาวข้อความอัตโนมัติ
///
/// บรรทัดที่ระบุ speakerId → ข้อความย้ายไปลอยข้างตัวละครนั้น (ต้องแปะ DialogueSpeaker ไว้) และตามตัวไปถ้าเดิน
/// บรรทัดที่ไม่ระบุ → ลอยตรงที่วางกล่องนี้ไว้ในซีนเหมือนเดิม
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WorldSpaceDialoguePresenter : MonoBehaviour, IDialoguePresenter
{
    [Tooltip("ชื่อที่บทพูดใช้เรียกกล่องนี้ (ช่อง Presenter Id ใน DialogueData)")]
    [SerializeField] private string presenterId = DialoguePresenterIds.World;

    [Header("UI References")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Canvas worldCanvas;

    [Header("การจางเข้า-ออก")]
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private Coroutine currentRoutine;

    private DialogueSpeaker currentSpeaker;
    private Vector3 homeLocalPosition;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        homeLocalPosition = transform.localPosition;

        // ข้อกำหนดที่ 1: กำแพงบัง — บังคับให้ Canvas วาดทับวัตถุ 3D เสมอด้วย Sorting Order สูง
        if (worldCanvas == null) worldCanvas = GetComponent<Canvas>();
        if (worldCanvas != null)
        {
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = 500;
        }

        // ข้อกำหนดที่ 2: หันเข้าหากล้อง — ใช้ Billboard.cs ที่มีอยู่แล้วในโปรเจกต์ ห้ามเขียนใหม่
        if (GetComponent<Billboard>() == null)
        {
            gameObject.AddComponent<Billboard>();
        }

        HideImmediate();
        DialoguePresenterRegistry.Register(presenterId, this);
    }

    private void OnDestroy()
    {
        DialoguePresenterRegistry.Unregister(presenterId, this);
    }

    // LateUpdate = หลังตัวละครขยับเสร็จในเฟรมนั้น ข้อความจะไม่สั่นตามหลังตัว
    private void LateUpdate()
    {
        if (currentSpeaker != null)
        {
            transform.position = currentSpeaker.BubblePosition;
        }
    }

    public IEnumerator Show(DialogueLine line)
    {
        if (line == null) yield break;

        AttachToSpeaker(line.speakerId);

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ShowRoutine(line));
        yield return currentRoutine;
        currentRoutine = null;
    }

    private IEnumerator ShowRoutine(DialogueLine line)
    {
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
        string text = loc != null ? loc.GetText(line.textKey) : line.textKey;

        if (dialogueText != null)
        {
            dialogueText.text = text;
            if (loc != null) dialogueText.font = loc.GetFont(FontCategory.Dialog);
        }

        // 1. Fade In (ใช้ unscaledDeltaTime เพื่อรองรับกรณี Pause/TimeScale = 0)
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 2. Hold ตามระยะเวลาที่กำหนด (ข้อความจะหายเองเสมอแม้ผู้เล่นเดินหนี)
        if (line.holdSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(line.holdSeconds);
        }

        // 3. Fade Out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    public void HideImmediate()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        AttachToSpeaker(null);
    }

    private void AttachToSpeaker(string speakerId)
    {
        currentSpeaker = DialogueSpeaker.Find(speakerId);

        if (!string.IsNullOrEmpty(speakerId) && currentSpeaker == null)
        {
            Debug.LogWarning($"[WorldSpaceDialogue] ไม่พบตัวละคร speakerId '{speakerId}' (ลืมแปะ DialogueSpeaker หรืออยู่ในโลกที่ถูกซ่อน?) — ขึ้นตรงที่เดิมแทน");
        }

        if (currentSpeaker != null)
        {
            transform.position = currentSpeaker.BubblePosition;
        }
        else
        {
            // กลับไปที่วางไว้เดิม (เช่น ลูกของ Player) เหมือนพฤติกรรมก่อนมีระบบ speaker
            transform.localPosition = homeLocalPosition;
        }
    }
}
