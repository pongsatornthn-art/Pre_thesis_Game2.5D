using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("เปิดกำแพงที่ระบบซ่อนกำแพง (RoomWallFade / RoomWallSmoothHide) ปิดไว้ ระหว่างดูผ่านกล้องปริศนา แล้วคืนค่าตอนออก")]
    public bool showHiddenWalls = true;
    [Tooltip("ใส่เพิ่มเองได้: Renderer ที่ต้องเห็นเสมอในกล้องปริศนา (เว้นว่างได้)")]
    public Renderer[] extraWallsToShow;
    [Tooltip("ใส่เพิ่มเองได้: GameObject ที่ปิดไว้ แล้วให้เปิดเฉพาะตอนเล่นปริศนา (เช่นกำแพง/เพดานปลอมปิดช่องโหว่) — เว้นว่างได้")]
    public GameObject[] extraObjectsToShow;
    [Tooltip("พิมพ์รายชื่อกำแพงที่ระบบดูแลลง Console ตอนเข้าปริศนา (ไว้เช็กว่าเจอกำแพงไหนบ้าง)")]
    public bool logWalls = true;

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

    [Header("เงื่อนไขก่อนเปิด (นา 2026-11-24)")]
    [Tooltip("จุดที่ต้องคลิกสังเกตก่อน (รอยลากหน้าชั้น) — ยังไม่คลิก = ดึงเล่มถูกก็ไม่เปิด · เว้นว่าง = ไม่มีเงื่อนไข (แบบเดิม)")]
    public PuzzleClue requiredClue;

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

    // ระหว่างอยู่ในกล้องปริศนา: กันระบบซ่อนกำแพงมาปิดซ้ำ (เช่น Trigger ห้องยิง OnTriggerEnter อีกรอบตอนล็อกตัวผู้เล่น)
    private void LateUpdate()
    {
        if (IsActive && wallsShown) KeepWallsVisible();
    }

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
        if (showHiddenWalls) ShowWalls();
        SetHintsVisible(true);
        onEnter?.Invoke();
    }

    public void Exit()
    {
        if (!IsActive) return;
        IsActive = false;
        solving = false;
        SetHover(null);
        SetClueHover(null);   // [นา 2026-11-24]
        SetHintsVisible(false);
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ExitMode();
        PlayerVisibility.Show(this);
        RestoreWalls();
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
        PuzzleClue clueHit = null;   // [นา 2026-11-24]
        if (cam != null)
        {
            // ยิงทะลุทุกอย่าง แล้วเอาหนังสือที่ใกล้สุด — หนังสือเสียบอยู่ในชั้น กล่องชนของตู้หนังสือจะบังถ้าใช้ Raycast ธรรมดา
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue, bestClue = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                PuzzleBook b = h.collider.GetComponentInParent<PuzzleBook>();
                if (b != null && h.distance < best) { best = h.distance; hit = b; }
                PuzzleClue c = h.collider.GetComponentInParent<PuzzleClue>();
                if (c != null && h.distance < bestClue) { bestClue = h.distance; clueHit = c; }
            }
            if (hit != null && clueHit != null) { if (bestClue < best) hit = null; else clueHit = null; }   // เอาอันที่ใกล้กล้องกว่า
        }
        SetHover(hit);
        SetClueHover(clueHit);

        if (clueHit != null && Input.GetMouseButtonDown(0)) { clueHit.Inspect(audioSource); return; }

        if (hit != null && Input.GetMouseButtonDown(0) && !hit.IsBusy && !hit.IsPulled)
        {
            bool locked = requiredClue != null && !requiredClue.Inspected;   // [นา 2026-11-24] ยังไม่สังเกตรอยลาก = เล่มถูกก็ไม่เปิด
            bool correct = hit.Pull(audioSource, locked);
            if (correct) Solve(hit);
            else
            {
                if (wrongSound != null) audioSource.PlayOneShot(wrongSound);
                onWrongBook?.Invoke();
            }
        }
    }

    private PuzzleClue hoveredClue;
    private void SetClueHover(PuzzleClue c)
    {
        if (hoveredClue == c) return;
        if (hoveredClue != null) hoveredClue.SetHovered(false);
        hoveredClue = c;
        if (hoveredClue != null) hoveredClue.SetHovered(true);
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
            RestoreWalls();                   // กลับกล้องหลัก = ซ่อนกำแพงเหมือนเดิม
            solvedCutscene.Play();
            yield return new WaitForSeconds((float)solvedCutscene.duration);
        }

        Exit();
    }

    // ───────── เซฟ ─────────
    [System.Serializable] private struct SaveData { public bool solved; public bool clueFound; }

    public string CaptureState() => JsonUtility.ToJson(new SaveData { solved = IsSolved, clueFound = requiredClue != null && requiredClue.Inspected });

    public void RestoreState(string stateJson)
    {
        SaveData d = JsonUtility.FromJson<SaveData>(stateJson);
        if (d.clueFound && requiredClue != null) requiredClue.SetInspectedInstant();   // [นา 2026-11-24]
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

    // ───────── กำแพงที่ถูกซ่อน ─────────
    // ระบบกล้องหลักซ่อนกำแพงไว้ให้มองเห็นตัวละคร:
    //   RoomWallFade / RoomWallSmoothHide = ปิด renderer ตอนผู้เล่นอยู่ในห้อง
    //   CameraObstacleFader (บน Main Camera) = ทำกำแพงโปร่ง — พอกล้องหลักถูกปิดตอนเข้าโหมดปริศนา กำแพงจะค้างโปร่งไว้
    // กล้องปริศนาอยู่ในห้อง ต้องเห็นกำแพงครบ → ทำให้ทึบชั่วคราว แล้วคืนค่าเดิมทุกอย่างตอนออก
    private readonly Dictionary<Renderer, bool> wallStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Material, Material> matSnapshots = new Dictionary<Material, Material>();
    private readonly List<GameObject> shownObjects = new List<GameObject>();

    private bool wallsShown;

    // [นา 2026-11-24] ย้ายตัวเปิดกำแพงไปเป็นระบบกลาง HiddenGeometryReveal (มุม FPS ทุกจุดใช้ร่วม)
    // เดิมปริศนาเปิดเอง + FirstPersonModeController ก็เปิด → 2 ระบบจำสถานะทับกัน ออกแล้วกำแพงค้างโชว์ · ตอนนี้ใช้ตัวเดียว
    private void ShowWalls()
    {
        RestoreWalls();
        wallsShown = true;
        HiddenGeometryReveal.Reveal(this, extraWallsToShow);
        if (extraObjectsToShow != null)
            foreach (GameObject g in extraObjectsToShow)
                if (g != null && !g.activeSelf) { g.SetActive(true); shownObjects.Add(g); }
    }

    private void ShowWallsOld_Unused()
    {
        var list = new List<Renderer>();
        foreach (RoomWallFade w in FindObjectsByType<RoomWallFade>(FindObjectsSortMode.None))
            if (w.wallsToHide != null) foreach (MeshRenderer r in w.wallsToHide) list.Add(r);
        foreach (RoomWallSmoothHide w in FindObjectsByType<RoomWallSmoothHide>(FindObjectsSortMode.None))
            if (w.wallsToFade != null) foreach (MeshRenderer r in w.wallsToFade) list.Add(r);
        foreach (CameraObstacleFader f in FindObjectsByType<CameraObstacleFader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            CollectFaderWalls(f, list);
        if (extraWallsToShow != null) list.AddRange(extraWallsToShow);

        foreach (Renderer r in list)
        {
            if (r == null || wallStates.ContainsKey(r)) continue;
            wallStates[r] = r.enabled;
            r.enabled = true;
            foreach (Material m in r.sharedMaterials) MakeOpaque(m);
        }

        if (extraObjectsToShow != null)
            foreach (GameObject g in extraObjectsToShow)
                if (g != null && !g.activeSelf) { g.SetActive(true); shownObjects.Add(g); }

        if (logWalls)
        {
            var sb = new System.Text.StringBuilder("[BookshelfPuzzle] กำแพงที่ดูแลระหว่างปริศนา: ");
            foreach (var kv in wallStates) if (kv.Key != null) sb.Append(kv.Key.name).Append(kv.Value ? "(เปิดอยู่) " : "(ถูกซ่อน→เปิดให้) ");
            sb.Append(" | วัสดุที่โปร่งอยู่→ทำทึบ: ").Append(matSnapshots.Count);
            Debug.Log(sb.ToString(), this);
        }
    }

    // เรียกทุกเฟรม: ถ้ามีสคริปต์ไหนปิด/ทำโปร่งกำแพงซ้ำ → จำไว้ว่า "ตอนออกต้องซ่อน" แล้วเปิดกลับทันที
    private void KeepWallsVisible() => HiddenGeometryReveal.KeepVisible();   // [นา 2026-11-24]

    private void KeepWallsVisibleOld_Unused()
    {
        tmpKeys.Clear();
        tmpKeys.AddRange(wallStates.Keys);
        foreach (Renderer r in tmpKeys)
        {
            if (r == null) continue;
            if (!r.enabled)
            {
                wallStates[r] = false;
                r.enabled = true;
                if (logWalls) Debug.Log($"[BookshelfPuzzle] มีสคริปต์ซ่อน {r.name} ซ้ำ — เปิดกลับให้แล้ว", r);
            }
            foreach (Material m in r.sharedMaterials) MakeOpaque(m);
        }
    }
    private readonly List<Renderer> tmpKeys = new List<Renderer>();

    private void RestoreWalls()
    {
        HiddenGeometryReveal.Restore(this);   // [นา 2026-11-24]
        foreach (GameObject g in shownObjects) if (g != null) g.SetActive(false);
        shownObjects.Clear();
        wallsShown = false;
    }

    private void RestoreWallsOld_Unused()
    {
        foreach (var kv in matSnapshots)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Key.CopyPropertiesFromMaterial(kv.Value);
            kv.Key.shaderKeywords = kv.Value.shaderKeywords;
            kv.Key.renderQueue = kv.Value.renderQueue;
            Destroy(kv.Value);
        }
        foreach (var kv in wallStates) if (kv.Key != null) kv.Key.enabled = kv.Value;
        foreach (GameObject g in shownObjects) if (g != null) g.SetActive(false);
        matSnapshots.Clear();
        wallStates.Clear();
        shownObjects.Clear();
        wallsShown = false;
    }

    private void MakeOpaque(Material m)
    {
        if (m == null) return;
        bool faded = (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < 0.99f)
                  || (m.HasProperty("_Color") && m.color.a < 0.99f)
                  || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f && m.renderQueue >= 3000);
        if (!faded) return;
        if (!matSnapshots.ContainsKey(m)) matSnapshots[m] = new Material(m); // เก็บสภาพเดิม (รวมโหมดโปร่ง) ไว้คืนตอนออก

        if (m.HasProperty("_BaseColor")) { Color c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); }
        if (m.HasProperty("_Color")) { Color c = m.color; c.a = 1f; m.color = c; }
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 2000;
    }

    // CameraObstacleFader เก็บกำแพงที่กำลังโปร่งไว้ในตัวแปร private — อ่านผ่าน reflection (ไม่ต้องแก้สคริปต์เดิม)
    private static void CollectFaderWalls(CameraObstacleFader fader, List<Renderer> list)
    {
        var field = typeof(CameraObstacleFader).GetField("activeWalls",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (!(field?.GetValue(fader) is System.Collections.IDictionary dict)) return;
        foreach (object fw in dict.Values)
        {
            var rf = fw?.GetType().GetField("renderer");
            if (rf?.GetValue(fw) is Renderer r) list.Add(r);
        }
    }

}
