using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวแสดงผลบทสนทนาแบบกล่องล่างจอ (ScreenBox Dialogue Presenter)
/// รองรับการแสดงชื่อผู้พูด, รูปใบหน้า (Portrait), ระบบพิมพ์ตัวอักษรทีละตัว (Typewriter)
/// และรองรับการกดปุ่ม (Space / E / Enter / คลิกซ้าย) เพื่อเร่งข้อความหรือข้ามไปยังบรรทัดถัดไป
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenBoxDialoguePresenter : MonoBehaviour, IDialoguePresenter
{
    [Header("UI References")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject speakerContainer;

    [Header("Typewriter Settings")]
    [Tooltip("ความเร็วในการพิมพ์ตัวอักษร (ตัวอักษรต่อวินาที)")]
    [SerializeField] private float charsPerSecond = 35f;

    private CanvasGroup canvasGroup;
    private Coroutine currentRoutine;
    private bool skipRequested = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    private void Update()
    {
        // รับ Input สำหรับการกดข้ามหรือเร่งข้อความ
        if (canvasGroup != null && canvasGroup.alpha > 0f)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                skipRequested = true;
            }
        }
    }

    public IEnumerator Show(DialogueLine line)
    {
        if (line == null) yield break;

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ShowRoutine(line));
        yield return currentRoutine;
        currentRoutine = null;
    }

    private IEnumerator ShowRoutine(DialogueLine line)
    {
        skipRequested = false;
        canvasGroup.alpha = 1f;

        ILocalizationService loc = ServiceLocator.Get<ILocalizationService>();

        // 1. จัดการชื่อผู้พูด (ถ้าว่าง = ไม่แสดง)
        bool hasSpeaker = !string.IsNullOrEmpty(line.speakerKey);
        if (speakerContainer != null) speakerContainer.SetActive(hasSpeaker);
        if (speakerText != null)
        {
            speakerText.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
            {
                speakerText.text = loc != null ? loc.GetText(line.speakerKey) : line.speakerKey;
                if (loc != null) speakerText.font = loc.GetFont(FontCategory.Header);
            }
        }

        // 2. จัดการรูปใบหน้า (ถ้าไม่มี = ซ่อนรูป)
        if (portraitImage != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.enabled = line.portrait != null;
        }

        // 3. จัดการข้อความหลักและเอฟเฟกต์ Typewriter
        string fullText = loc != null ? loc.GetText(line.textKey) : line.textKey;
        if (bodyText != null)
        {
            bodyText.text = fullText;
            if (loc != null) bodyText.font = loc.GetFont(FontCategory.Dialog);
            bodyText.maxVisibleCharacters = 0;
        }

        // บังคับอัปเดตข้อมูล TextMeshPro เพื่อคำนวณจำนวนตัวอักษรทั้งหมดที่มองเห็น
        if (bodyText != null) bodyText.ForceMeshUpdate();
        int totalChars = bodyText != null ? bodyText.textInfo.characterCount : fullText.Length;

        float charDelay = charsPerSecond > 0f ? (1f / charsPerSecond) : 0.03f;
        int visibleCount = 0;

        // ลูปพิมพ์ทีละตัวอักษร — หากกดข้ามครั้งแรก จะแสดงครบทั้งบรรทัดทันที
        while (visibleCount < totalChars)
        {
            if (skipRequested)
            {
                skipRequested = false;
                if (bodyText != null) bodyText.maxVisibleCharacters = totalChars;
                break;
            }

            visibleCount++;
            if (bodyText != null) bodyText.maxVisibleCharacters = visibleCount;
            yield return new WaitForSecondsRealtime(charDelay);
        }

        // 4. รอตามระยะเวลาค้าง (Hold) หรือจนกว่าผู้เล่นจะกดข้ามครั้งที่สอง
        float holdElapsed = 0f;
        while (holdElapsed < line.holdSeconds)
        {
            if (skipRequested)
            {
                skipRequested = false;
                break;
            }
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void HideImmediate()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        skipRequested = false;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
}
