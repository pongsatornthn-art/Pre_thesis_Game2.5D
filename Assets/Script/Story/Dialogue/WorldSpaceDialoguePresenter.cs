using System.Collections;
using System.Collections.Generic;
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
[RequireComponent(typeof(Billboard))]   // แปะให้ตอนวางใน Editor (เห็นใน Inspector) แทนการแปะด้วยโค้ดตอนเล่น
public class WorldSpaceDialoguePresenter : MonoBehaviour, IDialoguePresenter
{
    [Tooltip("ชื่อที่บทพูดใช้เรียกกล่องนี้ (ช่อง Presenter Id ใน DialogueData)")]
    [SerializeField] private string presenterId = DialoguePresenterIds.World;

    [Header("UI References")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Canvas worldCanvas;

    [Tooltip("Shader ที่วาดทับทุกอย่าง (ไม่สนกำแพง) — ลาก 'TMP_SDF Overlay' (Assets/TextMesh Pro/Shaders) มาใส่\nSorting Order อย่างเดียวกันกำแพง 3D ไม่ได้ เพราะกำแพงยังบังด้วยความลึก")]
    [SerializeField] private Shader overlayShader;

    [Header("ความยาวข้อความ (งานข้อ 5)")]
    [Tooltip("บรรทัดสูงสุดต่อ 1 หน้า — ยาวกว่านี้ตัดขึ้นหน้าถัดไปอัตโนมัติ")]
    [SerializeField, Min(1)] private int maxLines = 2;
    [Tooltip("แต่ละหน้าค้างอย่างน้อยกี่วินาที (กรณีข้อความถูกตัดหลายหน้า)")]
    [SerializeField, Min(0.3f)] private float minPageSeconds = 1.5f;

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

        // [แก้โดย Claude 2026-11-11 งานข้อ 5] ข้อความยาวเกิน Max Lines → ตัดเป็นหน้าละไม่เกิน 2 บรรทัด ขึ้นต่อกันเอง
        // เวลาค้างแบ่งตามความยาวแต่ละหน้า (รวมเท่าเดิม · หน้าละอย่างน้อย Min Page Seconds)
        List<string> pages = new List<string> { text };
        if (dialogueText != null)
        {
            if (loc != null) dialogueText.font = loc.GetFont(FontCategory.Dialog);
            ApplyOverlay();   // เปลี่ยนฟอนต์แล้ววัสดุถูกเปลี่ยนตาม → ตั้งใหม่ทุกครั้ง
            pages = SplitIntoPages(text);
            dialogueText.text = pages[0];
        }

        int totalChars = 0;
        foreach (string p in pages) totalChars += Mathf.Max(1, p.Length);

        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0 && dialogueText != null)
            {
                // เปลี่ยนหน้า: จางออกเร็วๆ → เปลี่ยนข้อความ → จางเข้า
                yield return Fade(1f, 0f, fadeDuration * 0.5f);
                dialogueText.text = pages[i];
                yield return Fade(0f, 1f, fadeDuration * 0.5f);
            }
            else
            {
                // 1. Fade In (ใช้ unscaledDeltaTime เพื่อรองรับกรณี Pause/TimeScale = 0)
                yield return Fade(0f, 1f, fadeDuration);
            }

            // 2. Hold ตามระยะเวลาที่กำหนด (ข้อความจะหายเองเสมอแม้ผู้เล่นเดินหนี)
            if (line.holdSeconds > 0f)
            {
                float share = pages.Count == 1 ? line.holdSeconds
                    : Mathf.Max(minPageSeconds, line.holdSeconds * Mathf.Max(1, pages[i].Length) / totalChars);
                yield return new WaitForSecondsRealtime(share);
            }
        }

        float elapsed;

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

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    /// <summary>
    /// ตัดข้อความเป็นหน้า หน้าละไม่เกิน maxLines บรรทัด
    /// ภาษาไทยไม่มีเว้นวรรคระหว่างคำ → ถ้าปล่อย TextMeshPro ตัดเอง จะตัดกลางคำ ("ทา|งได้")
    /// เลยจัดบรรทัดเอง: ตัดเฉพาะตรง "เว้นวรรค" ที่เขียนไว้ในบท (วัดความกว้างจริงด้วยฟอนต์ตอนนี้) แล้วใส่ขึ้นบรรทัดใหม่ให้เอง
    /// ก้อนไหนยาวเกินบรรทัดเดียว (ไม่มีเว้นวรรคเลย) ค่อยยอมตัดกลางก้อน
    /// → เขียนบทให้เว้นวรรคตรงจุดหายใจ จะตัดสวย
    /// </summary>
    private List<string> SplitIntoPages(string text)
    {
        List<string> pages = new List<string>();
        if (string.IsNullOrEmpty(text) || maxLines <= 0 || dialogueText == null) { pages.Add(text ?? ""); return pages; }

        float maxWidth = dialogueText.rectTransform.rect.width;
        if (maxWidth <= 1f) { pages.Add(text); return pages; }

        List<string> lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                if (word.Length == 0) continue;
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (Width(candidate) <= maxWidth) { line = candidate; continue; }

                if (line.Length > 0) lines.Add(line);
                line = word;

                // ก้อนเดียวยาวเกินบรรทัด → ตัดกลางก้อนตามความกว้าง (กรณีเลี่ยงไม่ได้)
                while (Width(line) > maxWidth && line.Length > 1)
                {
                    int cut = line.Length - 1;
                    while (cut > 1 && Width(line.Substring(0, cut)) > maxWidth) cut--;
                    lines.Add(line.Substring(0, cut));
                    line = line.Substring(cut);
                }
            }
            if (line.Length > 0) lines.Add(line);
        }

        for (int i = 0; i < lines.Count; i += maxLines)
            pages.Add(string.Join("\n", lines.GetRange(i, Mathf.Min(maxLines, lines.Count - i))));
        if (pages.Count == 0) pages.Add(text);
        return pages;
    }

    private float Width(string s) => dialogueText.GetPreferredValues(s, float.PositiveInfinity, float.PositiveInfinity).x;

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

    // ข้อความโดนกำแพงบัง (แก้ 2026-11-08): ให้วัสดุตัวหนังสือวาดแบบไม่เช็คความลึก (ZTest Always)
    // ใช้ fontMaterial = สำเนาเฉพาะกล่องนี้ ไม่แก้ไฟล์วัสดุของฟอนต์ที่ UI อื่นใช้ร่วม
    private void ApplyOverlay()
    {
        if (dialogueText == null) return;
        Shader s = overlayShader != null ? overlayShader : Shader.Find("TextMeshPro/Distance Field Overlay");
        if (s == null) return;
        Material m = dialogueText.fontMaterial;
        if (m.shader != s) m.shader = s;
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
