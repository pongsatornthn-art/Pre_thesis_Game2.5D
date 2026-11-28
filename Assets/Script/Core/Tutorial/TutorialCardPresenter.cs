using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// [Claude 2026-11-28] หน้าตาการ์ดคำสอน — กลางจอ · หยุดเกม · คลิปวนขยับ + ไอคอนปุ่มขยับ · หลายหน้า
/// สเปก: docs/TUTORIAL_SPEC.md · ตัวตัดสินใจ = TutorialService
///
/// วางในซีน (prefab [TUTORIAL] — Canvas ของตัวเอง ไม่อยู่ใต้ UI_Manager เพราะมุมโต๊ะ/มุม FPS ซ่อน UI_Manager):
///   [TUTORIAL]  Canvas (Sort Order สูงกว่าสมุด) + TutorialService + สคริปต์นี้  ← ต้องเปิดอยู่เสมอ
///     └ CardPanel  CanvasGroup + UIPanelController  (พื้นมืดเต็มจอ + การ์ดกระดาษ)
///         ├ VideoFrame
///         │   ├ StillImage (Image)       ภาพนิ่งสำรอง
///         │   ├ VideoImage (RawImage)    ← ใส่ Render Texture เดียวกับ Video Player
///         │   └ Overlays/…  (TutorialOverlay + รูปปุ่มแปะ KeyIconAnimator)
///         ├ Title / Heading / Body / PageCounter (TextMeshPro)
///         └ PrevButton / NextButton / CloseButton (+ UIButtonFX, ป้ายใช้ LocalizedText)
///   Video Player: Render Mode = Render Texture · ไม่ต้องใส่คลิป (การ์ดใส่ให้)
///
/// ปุ่ม: Enter = ถัดไป/เข้าใจแล้ว · ← → หรือ A D = เปลี่ยนหน้า · Esc = ปิด · คลิกปุ่มบนการ์ด
/// ⚠️ ไม่ใช้ E / Space / คลิกซ้ายลอย ๆ ปิด — E เปิดประตู, Space พุ่งตัว, คลิกซ้ายยิงปืน จะทำงานซ้อนตอนการ์ดปิด
/// </summary>
public class TutorialCardPresenter : MonoBehaviour, ITutorialPresenter
{
    /// <summary>การ์ดเปิดอยู่ (หรือเพิ่งปิดเฟรมนี้) — ระบบอื่นใช้กันปุ่มทะลุ (ดู GameplayInputGate)</summary>
    public static bool AnyOpen { get; private set; }

    [Header("แผง")]
    [SerializeField] private UIPanelController panel;

    [Header("ข้อความ")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("หัวข้อย่อยของหน้า (ไม่บังคับ)")]
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("เลขหน้า เช่น 1 / 3 (ซ่อนเองถ้ามีหน้าเดียว)")]
    [SerializeField] private TMP_Text pageCounterText;
    [SerializeField] private string bulletPrefix = "•  ";

    [Header("ภาพขยับ")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("Render Texture ที่ Video Player วาดลง (ตัวเดียวกับใน RawImage)")]
    [SerializeField] private RenderTexture videoTexture;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private Image stillImage;
    [Tooltip("ชุดไอคอนปุ่มขยับ — เว้นว่าง = หาลูกทั้งหมดเอง")]
    [SerializeField] private TutorialOverlay[] overlays = new TutorialOverlay[0];

    [Header("ปุ่ม")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [Tooltip("ปุ่ม 'เข้าใจแล้ว' — โชว์เฉพาะหน้าสุดท้าย")]
    [SerializeField] private Button closeButton;

    [Header("จังหวะ / เสียง")]
    [Tooltip("ต้องรอกี่วินาทีก่อนกดปิดได้ (กันกดข้ามโดยไม่ตั้งใจ)")]
    [SerializeField, Min(0f)] private float inputDelay = 0.5f;
    [Tooltip("เสียงตอนการ์ดเด้ง (เว้นว่าง = เสียงคลิกเมนู)")]
    [SerializeField] private AudioClip openSound;

    public bool IsOpen { get; private set; }

    private TutorialData current;
    private int pageIndex;
    private bool closeRequested;
    private float acceptInputAt;
    private ILocalizationService loc;

    private float savedTimeScale = 1f;
    private CursorLockMode savedLock;
    private bool savedVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyOpen = false;

    private void Awake()
    {
        if (overlays == null || overlays.Length == 0) overlays = GetComponentsInChildren<TutorialOverlay>(true);

        if (prevButton != null) prevButton.onClick.AddListener(() => ChangePage(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => ChangePage(+1));
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;   // เกมหยุด (timeScale 0) คลิปยังเล่น
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            if (videoTexture != null) videoPlayer.targetTexture = videoTexture;
            else Debug.LogWarning("[TutorialCard] ยังไม่ได้ใส่ Video Texture — คลิปจะไม่ขึ้น (โชว์ภาพนิ่งแทน)", this);
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.started += OnVideoStarted;
        }
        if (videoImage != null && videoTexture != null) videoImage.texture = videoTexture;

        if (panel == null) Debug.LogWarning("[TutorialCard] ยังไม่ได้ลาก Panel (UIPanelController)", this);
        else panel.HideImmediate();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.started -= OnVideoStarted;
        }
        if (IsOpen) RestoreGameState();
    }

    private void OnEnable()
    {
        loc = ServiceLocator.GetOptional<ILocalizationService>();
        if (loc != null) loc.OnLanguageChanged += Redraw;
    }

    private void OnDisable()
    {
        if (loc != null) loc.OnLanguageChanged -= Redraw;

        // กันพัง: การ์ดโดนปิด object กลางคัน (เปลี่ยนซีน/ระบบอื่นปิด UI) → coroutine ตาย
        // ถ้าไม่คืนค่าตรงนี้ เกมจะค้าง timeScale 0 + ปุ่มโดนบล็อกตลอดไป
        if (!IsOpen) return;
        IsOpen = false;
        closeRequested = true;
        if (videoPlayer != null) videoPlayer.Stop();
        if (panel != null) panel.HideImmediate();
        RestoreGameState();
        current = null;
    }

    // ───────── เปิด / ปิด ─────────

    public IEnumerator Show(TutorialData tutorial)
    {
        if (tutorial == null || IsOpen || panel == null) yield break;
        if (tutorial.PageCount == 0)
        {
            Debug.LogWarning($"[TutorialCard] '{tutorial.name}' ไม่มีหน้าในการ์ด — ข้าม", tutorial);
            yield break;
        }

        current = tutorial;
        pageIndex = 0;
        closeRequested = false;
        IsOpen = true;
        AnyOpen = true;
        acceptInputAt = Time.unscaledTime + inputDelay;

        // หยุดเกม + เปิดเมาส์ (จำค่าเดิมไว้คืน — เปิดจากสมุดเวลาก็หยุดอยู่แล้ว)
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DrawPage();
        panel.Show();
        PlayOpenSound();

        while (!closeRequested && this != null) yield return null;

        if (videoPlayer != null) videoPlayer.Stop();
        panel.Hide();
        Audio()?.PlayMenuSound(MenuSoundType.Back);

        // คืนเวลาเกมเฟรมถัดไป — คลิก/ปุ่มที่ใช้ปิดการ์ด จะได้ไม่ไปยิงปืน/ฟันต่อในเฟรมเดียวกัน
        IsOpen = false;
        yield return null;
        RestoreGameState();
        current = null;
    }

    public void Close()
    {
        if (IsOpen) closeRequested = true;
    }

    private void RestoreGameState()
    {
        AnyOpen = false;
        // ระหว่างการ์ดเปิด มีระบบอื่นเปลี่ยนเวลาไปแล้ว (เช่น ปิดสมุดที่เปิดการ์ดดูซ้ำ → สมุดคืนเวลา 1 + ล็อกเมาส์เอง)
        // → ปล่อยตามระบบนั้น ไม่งั้นจะคืนค่า "ตอนสมุดเปิด" ทับ = เกมค้างหยุด
        if (Time.timeScale != 0f) return;
        Time.timeScale = savedTimeScale;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    // ───────── ปุ่ม ─────────

    private void Update()
    {
        if (!IsOpen || closeRequested) return;

        // กันพลาด: เตรียมคลิปเสร็จแต่ event prepareCompleted ไม่มา (เคยเจอตอนเกมหยุด) → สั่งเล่นเอง
        if (videoPlayer != null && videoPlayer.clip != null && videoPlayer.isPrepared && !videoPlayer.isPlaying)
            videoPlayer.Play();

        if (Time.unscaledTime < acceptInputAt) return;

        bool last = pageIndex >= current.PageCount - 1;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (last) Close();
            else ChangePage(+1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ChangePage(+1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) ChangePage(-1);
        else if (Input.GetKeyDown(KeyCode.Escape) && !JournalController.IsAnyOpen)
        {
            // เปิดจากสมุด → สมุดเรียก HandleBack ปิดให้เอง (TutorialPage) ไม่งั้นจะปิดสมุดซ้อน
            EscapeGuard.MarkHandled();   // ไม่ให้เมนูหยุดเกมเด้งตาม
            Close();
        }
    }

    private void ChangePage(int delta)
    {
        if (!IsOpen || current == null || Time.unscaledTime < acceptInputAt) return;
        int next = Mathf.Clamp(pageIndex + delta, 0, current.PageCount - 1);
        if (next == pageIndex) return;
        pageIndex = next;
        DrawPage();
        Audio()?.PlayMenuSound(MenuSoundType.Click);
    }

    // ───────── วาด ─────────

    private void Redraw()
    {
        if (IsOpen) DrawText();
    }

    private void DrawPage()
    {
        if (current == null) return;
        DrawText();
        DrawMedia(current.pages[pageIndex]);
        DrawOverlays(current.pages[pageIndex]);

        int count = current.PageCount;
        bool last = pageIndex >= count - 1;
        if (prevButton != null) prevButton.gameObject.SetActive(count > 1 && pageIndex > 0);
        if (nextButton != null) nextButton.gameObject.SetActive(!last);
        if (closeButton != null) closeButton.gameObject.SetActive(last);
        if (pageCounterText != null)
        {
            pageCounterText.gameObject.SetActive(count > 1);
            pageCounterText.text = $"{pageIndex + 1} / {count}";
        }
    }

    private void DrawText()
    {
        if (current == null) return;
        if (loc == null) loc = ServiceLocator.GetOptional<ILocalizationService>();
        TutorialPageData page = current.pages[pageIndex];

        if (titleText != null)
        {
            titleText.text = current.Title;
            if (loc != null) titleText.font = loc.GetFont(FontCategory.Header);
        }

        if (headingText != null)
        {
            bool hasHeading = page != null && !string.IsNullOrEmpty(page.headingKey);
            headingText.gameObject.SetActive(hasHeading);
            if (hasHeading)
            {
                headingText.text = TutorialData.Loc(page.headingKey);
                if (loc != null) headingText.font = loc.GetFont(FontCategory.Header);
            }
        }

        if (bodyText != null)
        {
            var sb = new StringBuilder();
            if (page != null && page.bodyKeys != null)
            {
                foreach (string key in page.bodyKeys)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(bulletPrefix).Append(TutorialData.Loc(key));
                }
            }
            bodyText.text = sb.ToString();
            if (loc != null) bodyText.font = loc.GetFont(FontCategory.Default);
        }
    }

    private void DrawMedia(TutorialPageData page)
    {
        Sprite still = page != null ? page.image : null;
        VideoClip clip = page != null ? page.clip : null;
        bool canPlay = clip != null && videoPlayer != null && videoTexture != null;

        // ภาพนิ่งโชว์ก่อนเสมอ (ระหว่างคลิปโหลด) · RawImage โผล่เมื่อคลิปเริ่มเล่นจริง (OnVideoStarted) กันเฟรมดำ/เฟรมคลิปเก่า
        if (stillImage != null)
        {
            stillImage.sprite = still;
            stillImage.gameObject.SetActive(still != null);
        }
        if (videoImage != null) videoImage.gameObject.SetActive(false);

        if (videoPlayer == null) return;
        videoPlayer.Stop();
        if (!canPlay)
        {
            videoPlayer.clip = null;   // ไม่งั้นตัวกันพลาดใน Update จะเล่นคลิปหน้าก่อนต่อ
            return;
        }

        videoPlayer.clip = clip;
        videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (IsOpen && !closeRequested) vp.Play();
    }

    private void OnVideoStarted(VideoPlayer vp)
    {
        if (videoImage != null && IsOpen) videoImage.gameObject.SetActive(true);
    }

    private void DrawOverlays(TutorialPageData page)
    {
        foreach (TutorialOverlay o in overlays)
        {
            if (o == null) continue;
            bool on = page != null && page.overlayIds != null && page.overlayIds.Contains(o.OverlayId);
            o.gameObject.SetActive(on);
        }

#if UNITY_EDITOR
        if (page?.overlayIds == null) return;
        foreach (string id in page.overlayIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (System.Array.Find(overlays, o => o != null && o.OverlayId == id) == null)
                Debug.LogWarning($"[TutorialCard] '{current.name}' ขอไอคอนชุด '{id}' แต่ในการ์ดไม่มี TutorialOverlay ชื่อนี้", this);
        }
#endif
    }

    private void PlayOpenSound()
    {
        IAudioService audio = Audio();
        if (audio == null) return;
        if (openSound != null)
        {
            Camera cam = Camera.main;
            audio.PlaySFX(openSound, cam != null ? cam.transform.position : Vector3.zero, 1f, false);
        }
        else audio.PlayMenuSound(MenuSoundType.Click);
    }

    private static IAudioService Audio() => ServiceLocator.GetOptional<IAudioService>();
}
