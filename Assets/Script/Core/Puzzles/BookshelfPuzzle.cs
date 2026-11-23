using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// [2026-11-22] ปริศนาห้องหนังสือ (ห้องสุดท้าย) — ดึงหนังสือถูกเล่ม → ประตูลับเปิด
///
/// เล่นยังไง: เดินเข้าจุด กด E → เข้ามุมกล้องปริศนา (ตัวผู้เล่นหายจากจอ)
///   A / D (หรือ ← →) = สลับมุมกล้อง 4 มุม (วนได้) · ชี้เมาส์ที่หนังสือ = เลื่อนออกมานิดนึง · คลิก = ดึง
///   ถูกเล่ม → หนังสือค้าง + ประตูลับเปิด + ออกจากปริศนาเอง · ผิดเล่ม → หนังสือเลื่อนกลับ
///   ESC = ออก
///
/// ตั้งค่า: แปะที่ GameObject ที่มี Collider (Is Trigger) ตรงจุดที่ให้กด E (เช่น หน้าโต๊ะ)
///   + SaveableEntity (ใส่ให้เอง) → เซฟจำว่าแก้แล้ว · โหลดกลับมาประตูลับยังเปิดอยู่
///   Views = กล้อง 4 ตัว (ปิดไว้ในฉาก) · Passage = ประตูลับ (SecretPassage) · Books เว้นว่าง = หาลูกของ Books Root / ทั้งฉาก
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class BookshelfPuzzle : MonoBehaviour, ISaveable
{
    [Header("กล้อง (เรียงตามลำดับที่กด D)")]
    [Tooltip("กล้องมุมต่างๆ (GameObject ที่มี Camera) — ปิดไว้ในฉาก ระบบเปิด/ปิดให้เอง")]
    public GameObject[] views = new GameObject[4];
    [Tooltip("เข้ามาแล้วเริ่มที่มุมไหน (0 = มุมแรก)")]
    public int startView = 0;
    [Tooltip("ซ่อนตัวผู้เล่นระหว่างเล่นปริศนา")]
    public bool hidePlayer = true;

    [Header("หนังสือ / ประตูลับ")]
    [Tooltip("เว้นว่าง = หา PuzzleBook ใต้ Books Root (ถ้าไม่ใส่ Root = ทั้งฉาก)")]
    public PuzzleBook[] books;
    public Transform booksRoot;
    [Tooltip("ประตูลับแบบสคริปต์ (เว้นว่างได้ ถ้าใช้คัตซีนเปิดประตูแทน)")]
    public SecretPassage passage;

    [Header("ต่อกับระบบเก่า (คัตซีนเปิดทาง)")]
    [Tooltip("คัตซีนที่เล่นหลังแก้ได้ เช่น MiniGame_Cutscene_Exit (กล้องไปที่ประตู Door_Tast_Cutscene + Cube (2))\n" +
             "ลำดับ: หนังสือค้าง → รอ Exit Delay → กลับกล้องหลัก (ผู้เล่นยังล็อก) → เล่นคัตซีนจนจบ → เดินได้")]
    public PlayableDirector solvedCutscene;
    [Tooltip("ธงเนื้อเรื่องตอนแก้ได้ (แบบเดียวกับ MiniGameManager เดิม) — เว้นว่างได้")]
    public StoryFlagId onCompleteFlag;
    [Tooltip("โหลดเซฟที่แก้แล้ว → ข้ามคัตซีนไปท่าจบทันที (ประตู/ของที่คัตซีนขยับ อยู่ท่าเปิด)")]
    public bool restoreCutsceneEndOnLoad = true;
    [Tooltip("แก้ได้แล้ว: รอกี่วินาทีก่อนออกจากมุมกล้อง / เข้าคัตซีน (ให้เห็นหนังสือค้างก่อน)")]
    public float exitDelayAfterSolve = 1.5f;
    [Tooltip("แก้ได้แล้ว: สลับไปมุมกล้องนี้ให้เห็นประตูลับเปิด (-1 = อยู่มุมเดิม)")]
    public int showPassageView = -1;

    [Header("ปุ่ม")]
    public KeyCode enterKey = KeyCode.E;

    [Header("ป้ายบอกปุ่ม (สร้างให้เอง)")]
    public bool showHints = true;
    public TMP_FontAsset hintFont;
    public string hintLeft = "<  A";
    public string hintRight = "D  >";
    public string hintExit = "ESC";

    [Header("เสียง (เว้นว่างได้)")]
    public AudioClip switchViewSound;
    public AudioClip wrongSound;
    public AudioClip solvedSound;

    [Header("Events")]
    public UnityEvent onEnter;
    public UnityEvent onExit;
    public UnityEvent onWrongBook;
    public UnityEvent onSolved;

    public bool IsSolved { get; private set; }
    public bool IsActive { get; private set; }

    private int viewIndex;
    private bool playerNear;
    private PuzzleBook hovered;
    private AudioSource audioSource;
    private bool solving;
    private GameObject hintRoot;
    private TextMeshProUGUI counterText;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; }
        if (books == null || books.Length == 0)
            books = booksRoot != null ? booksRoot.GetComponentsInChildren<PuzzleBook>(true)
                                      : FindObjectsByType<PuzzleBook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject v in views) if (v != null) v.SetActive(false);
        if (showHints) BuildHints();
    }

    // ───────── เข้า/ออก ─────────
    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) playerNear = true; }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) playerNear = false; }

    private void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        if (!IsActive)
        {
            if (!IsSolved && playerNear && Input.GetKeyDown(enterKey) && !InteractPromptMarker.OtherPromptShown(gameObject)
                && !GameplayInputGate.Blocked) Enter();
            return;
        }

        if (solving) return; // แก้เสร็จแล้ว กำลังเล่นลำดับจบ — ไม่รับปุ่ม

        if (Input.GetKeyDown(KeyCode.Escape)) { EscapeGuard.MarkHandled(); Exit(); return; }
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) SwitchView(-1);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) SwitchView(1);

        UpdateHoverAndClick();
    }

    public void Enter()
    {
        if (IsActive || IsSolved) return;
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp == null || fp.IsActive || ValidViewCount() == 0)
        {
            if (ValidViewCount() == 0) Debug.LogWarning("[BookshelfPuzzle] ยังไม่ได้ใส่กล้องในช่อง Views", this);
            return;
        }

        IsActive = true;
        solving = false;
        viewIndex = NextValid(Mathf.Clamp(startView, 0, views.Length - 1), 0);
        fp.EnterMode(views[viewIndex], unlockCursor: true, lookController: null, standPoint: null, returnToOriginalPosition: true);
        if (hidePlayer) PlayerVisibility.Hide(this);
        SetHintsVisible(true);
        onEnter?.Invoke();
    }

    public void Exit()
    {
        if (!IsActive) return;
        IsActive = false;
        solving = false;
        SetHover(null);
        SetHintsVisible(false);
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ExitMode();
        PlayerVisibility.Show(this);
        onExit?.Invoke();
    }

    private void OnDisable()
    {
        if (IsActive) Exit();
    }

    // ───────── สลับมุม ─────────
    private void SwitchView(int dir)
    {
        if (ValidViewCount() <= 1) return;
        int next = NextValid(viewIndex, dir);
        if (next == viewIndex) return;
        ShowView(next);
        if (switchViewSound != null) audioSource.PlayOneShot(switchViewSound);
    }

    private void ShowView(int index)
    {
        viewIndex = index;
        SetHover(null);
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ShowFirstPersonCamera(views[viewIndex], unlockCursor: true);
        UpdateCounter();
    }

    /// <summary>หามุมกล้องถัดไปที่ใส่ไว้ (ข้ามช่องว่าง · วนรอบ) — dir 0 = ตัวมันเองถ้าใช้ได้</summary>
    private int NextValid(int from, int dir)
    {
        int n = views.Length;
        if (n == 0) return 0;
        int step = dir == 0 ? 1 : dir;
        int i = dir == 0 ? from : ((from + step) % n + n) % n;
        for (int k = 0; k < n; k++)
        {
            if (views[i] != null) return i;
            i = ((i + step) % n + n) % n;
        }
        return from;
    }

    private int ValidViewCount()
    {
        int c = 0;
        if (views != null) foreach (GameObject v in views) if (v != null) c++;
        return c;
    }

    // ───────── หนังสือ ─────────
    private void UpdateHoverAndClick()
    {
        Camera cam = views[viewIndex] != null ? views[viewIndex].GetComponentInChildren<Camera>() : null;
        PuzzleBook hit = null;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit h, 50f, ~0, QueryTriggerInteraction.Collide))
                hit = h.collider.GetComponentInParent<PuzzleBook>();
        }
        SetHover(hit);

        if (hit != null && Input.GetMouseButtonDown(0) && !hit.IsBusy && !hit.IsPulled)
        {
            bool correct = hit.Pull(audioSource);
            if (correct) Solve(hit);
            else
            {
                if (wrongSound != null) audioSource.PlayOneShot(wrongSound);
                onWrongBook?.Invoke();
            }
        }
    }

    private void SetHover(PuzzleBook b)
    {
        if (hovered == b) return;
        if (hovered != null) hovered.SetHovered(false);
        hovered = b;
        if (hovered != null) hovered.SetHovered(true);
    }

    private void Solve(PuzzleBook book)
    {
        IsSolved = true;
        SetHover(null);
        if (solvedSound != null) audioSource.PlayOneShot(solvedSound);
        if (passage != null) passage.Open();
        else if (solvedCutscene == null) Debug.LogWarning("[BookshelfPuzzle] ยังไม่ได้ใส่ประตูลับ (Passage) หรือคัตซีน (Solved Cutscene)", this);

        if (onCompleteFlag != null)
        {
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null) flags.Set(onCompleteFlag);
            else Debug.LogWarning("[BookshelfPuzzle] ไม่พบระบบ IStoryFlags ในฉาก", this);
        }
        onSolved?.Invoke();

        if (showPassageView >= 0 && showPassageView < views.Length && views[showPassageView] != null) ShowView(showPassageView);
        SetHintsVisible(false);
        solving = true;
        StartCoroutine(SolvedSequence());
    }

    /// <summary>รอให้เห็นหนังสือค้าง → (มีคัตซีน) กลับกล้องหลักแล้วเล่นคัตซีน โดยผู้เล่นยังล็อก → ออก</summary>
    private IEnumerator SolvedSequence()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, exitDelayAfterSolve));

        if (solvedCutscene != null)
        {
            FirstPersonModeController fp = FirstPersonModeController.Instance;
            if (fp != null)
            {
                fp.SetLookLocked(true);
                fp.SetCursorUnlocked(false);
                fp.ShowMainCamera();          // กลับกล้องหลักให้คัตซีนคุม (ผู้เล่นยังเดินไม่ได้)
            }
            PlayerVisibility.Show(this);      // ในคัตซีนต้องเห็นตัวละคร
            solvedCutscene.Play();
            yield return new WaitForSeconds((float)solvedCutscene.duration);
        }

        Exit();
    }

    // ───────── เซฟ ─────────
    [System.Serializable] private struct SaveData { public bool solved; }

    public string CaptureState() => JsonUtility.ToJson(new SaveData { solved = IsSolved });

    public void RestoreState(string stateJson)
    {
        SaveData d = JsonUtility.FromJson<SaveData>(stateJson);
        if (!d.solved) return;
        IsSolved = true;
        if (passage != null) passage.OpenInstant();
        if (solvedCutscene != null && restoreCutsceneEndOnLoad)
        {
            // ข้ามไปเฟรมสุดท้ายของคัตซีน → ประตู/ของที่คัตซีนขยับ อยู่ท่าเปิดค้าง (ไม่เล่นกล้อง)
            solvedCutscene.time = solvedCutscene.duration;
            solvedCutscene.Evaluate();
        }
        if (books != null) foreach (PuzzleBook b in books) if (b != null && b.isCorrect) b.SetPulledInstant();
    }

    // ───────── ป้ายปุ่ม ─────────
    private void BuildHints()
    {
        hintRoot = new GameObject("BookshelfPuzzle_Hints", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintRoot.transform.SetParent(transform, false);
        Canvas c = hintRoot.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 50;
        CanvasScaler cs = hintRoot.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);

        Label(hintLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), TextAlignmentOptions.Left, 44);
        Label(hintRight, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), TextAlignmentOptions.Right, 44);
        Label(hintExit, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), TextAlignmentOptions.BottomLeft, 26);
        counterText = Label("", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), TextAlignmentOptions.Bottom, 26);
        hintRoot.SetActive(false);
    }

    private TextMeshProUGUI Label(string text, Vector2 anchor, Vector2 pivot, Vector2 pos, TextAlignmentOptions align, float size)
    {
        GameObject go = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(hintRoot.transform, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400f, 80f);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (hintFont != null) t.font = hintFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = new Color(1f, 1f, 1f, 0.75f);
        t.raycastTarget = false;
        return t;
    }

    private void SetHintsVisible(bool on)
    {
        if (hintRoot == null) return;
        hintRoot.SetActive(on);
        if (on) UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText == null) return;
        int pos = 0, total = 0;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            total++;
            if (i == viewIndex) pos = total;
        }
        counterText.text = $"{pos} / {total}";
    }
}