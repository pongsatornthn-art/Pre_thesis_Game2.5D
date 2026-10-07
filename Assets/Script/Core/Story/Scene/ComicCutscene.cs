using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คัทซีนภาพทีละหน้าแบบการ์ตูน (เช่น 10 รูปความทรงจำ) — กดเปิดหน้าถัดไปเอง
///
/// วิธีทำ (ในซีนทั้งหมด ไม่มีโค้ดสร้างของ · STRUCTURE_CHECKLIST B1):
///   1. ใน Canvas สร้าง object แม่ เช่น "Comic_PaintingMemory" แปะ CanvasGroup + UIPanelController + สคริปต์นี้ · ตั้ง Scene Id
///   2. ลูก 10 ตัว = รูปละหน้า (Image) แต่ละหน้าแปะ CanvasGroup · คำบรรยายใต้รูปใช้ TMP + LocalizedText (แปลภาษาได้)
///   3. ลากหน้าใส่ลิสต์ Pages ตามลำดับ · ใส่เสียงประจำหน้า / เวลาเปลี่ยนเองได้ต่อหน้า
///   4. ฉากเนื้อเรื่อง: "คัทซีน/เล่นคัทซีน" ใส่ Scene Id เดียวกัน
/// กด Space / Enter / ปุ่มโต้ตอบ (F) = หน้าถัดไป · ปุ่มข้าม = จบทั้งชุด · ไม่รับปุ่มตอนหยุดเกม · ใช้เวลาจริง (unscaled)
/// </summary>
public class ComicCutscene : CutsceneBase
{
    [Serializable]
    public class Page
    {
        [Tooltip("หน้านี้ (ควรมี CanvasGroup เพื่อค่อยๆ จาง)")]
        public GameObject root;

        [Tooltip("เสียงตอนเปิดหน้านี้ (เว้นว่างได้) — เล่นผ่าน IAudioService")]
        public AudioClip sound;

        [Tooltip("เปลี่ยนหน้าเองหลังกี่วินาที (0 = รอผู้เล่นกดเท่านั้น)")]
        [Min(0f)] public float autoAdvanceSeconds;
    }

    [Header("หน้า (เรียงตามลำดับ)")]
    [SerializeField] private List<Page> pages = new List<Page>();

    [Header("หน้าตา")]
    [Tooltip("ตัวเปิด/ปิดทั้งชุด (fade เหมือน UI อื่น) — เว้นว่าง = เปิด/ปิด object นี้ตรงๆ")]
    [SerializeField] private UIPanelController panel;
    [SerializeField, Min(0f)] private float pageFadeSeconds = 0.35f;

    [Header("ปุ่ม")]
    [Tooltip("ข้ามทั้งชุด (None = ข้ามไม่ได้) · ห้ามใช้ Esc (ชนเมนูหยุดเกม)")]
    [SerializeField] private KeyCode skipKey = KeyCode.Tab;

    [Tooltip("เสียงตอนกดเปิดหน้าถัดไป (เว้นว่าง = เสียงคลิกเมนูกลาง)")]
    [SerializeField] private AudioClip turnPageSound;

    private Coroutine running;
    private bool advanceRequested;
    private bool skipRequested;
    private int shownFrame = -1;

    public override bool IsPlaying => running != null;

    protected override void Awake()
    {
        base.Awake();
        foreach (Page p in pages) if (p?.root != null) p.root.SetActive(false);

        // UIPanelController.HideImmediate ปิด object ทั้งก้อน — ถ้าแผงอยู่ก้อนเดียวกับสคริปต์นี้ ห้ามเรียก
        // (ก้อนที่ถูกปิดเริ่ม coroutine ไม่ได้ → สั่งเล่นแล้วไม่เล่น · เจอจริงตอนทดสอบ 2026-10-07) → ซ่อนด้วย alpha แทน
        if (panel != null && panel.gameObject != gameObject) panel.HideImmediate();
        else HideSelfWithAlpha();
    }

    private void HideSelfWithAlpha()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (running == null || Time.frameCount == shownFrame) return;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        if (skipKey != KeyCode.None && Input.GetKeyDown(skipKey)) skipRequested = true;
        else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || InteractInput.Pressed) advanceRequested = true;
    }

    public override void Play()
    {
        // แผงซ่อนจบรอบก่อนจะปิดก้อนนี้ไว้ → เปิดก่อนเริ่ม coroutine
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[ComicCutscene] '{name}' อยู่ใต้ object ที่ปิดอยู่ — เล่นไม่ได้", this);
            return;
        }

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayRoutine());
    }

    public override void Skip() => skipRequested = true;

    private IEnumerator PlayRoutine()
    {
        skipRequested = false;
        shownFrame = Time.frameCount;   // กด F เริ่มฉาก → เฟรมเดียวกันห้ามนับเป็นเปิดหน้าถัดไป

        if (panel != null) panel.Show();
        else { CanvasGroup g = GetComponent<CanvasGroup>(); if (g != null) { g.alpha = 1f; g.blocksRaycasts = true; } }

        for (int i = 0; i < pages.Count && !skipRequested; i++)
        {
            Page page = pages[i];
            if (page?.root == null) continue;

            PlaySound(page.sound);
            yield return FadePage(page.root, true);

            advanceRequested = false;
            float shown = 0f;
            while (!advanceRequested && !skipRequested)
            {
                if (page.autoAdvanceSeconds > 0f && shown >= page.autoAdvanceSeconds) break;
                shown += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!skipRequested) PlaySound(turnPageSound, menuFallback: true);
            yield return FadePage(page.root, false);
        }

        foreach (Page p in pages) if (p?.root != null) p.root.SetActive(false);
        running = null;

        if (panel != null) panel.Hide();
        else HideSelfWithAlpha();
    }

    private IEnumerator FadePage(GameObject root, bool show)
    {
        if (show) root.SetActive(true);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group != null && pageFadeSeconds > 0f && !skipRequested)
        {
            float from = show ? 0f : 1f, to = show ? 1f : 0f, t = 0f;
            while (t < pageFadeSeconds && !skipRequested)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / pageFadeSeconds);
                yield return null;
            }
            group.alpha = to;
        }

        if (!show) root.SetActive(false);
    }

    private static void PlaySound(AudioClip clip, bool menuFallback = false)
    {
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        if (audio == null) return;

        if (clip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            audio.PlaySFX(clip, pos, 1f, false);
        }
        else if (menuFallback)
        {
            audio.PlayMenuSound(MenuSoundType.Click);
        }
    }
}
