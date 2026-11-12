using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// หน้าจอเลือกช่องเซฟ/โหลด (แบบ Resident Evil / เกม AAA) — ใช้สมุดเล่มเดียวกับหน้ากระเป๋า
/// โหมดโหลด (เมนูหลัก / เมนูหยุดเกม): หัวข้อเปลี่ยนเป็น "โหลดเกม" · ช่องว่างจางและกดไม่ได้ · คลิกช่องที่มีเซฟ = โหลด
/// การ์ด 4 ช่อง: รูปฉากตอนเซฟ + วันที่ + เวลา · ชี้การ์ด = รายละเอียดโชว์บนกระดาษด้านขวา
/// คลิกช่องว่าง = เซฟเลย · คลิกช่องที่มีเซฟ = ถามก่อนว่าเขียนทับไหม · ESC = ยกเลิก (ปิดหน้ายืนยันก่อนถ้าเปิดอยู่)
///
/// ลงทะเบียนเป็น ISaveSlotPicker → จุดเซฟทุกจุด (SavePoint) ใช้หน้าจอนี้อัตโนมัติ
/// วางสคริปต์นี้บน Canvas ที่ "เปิดอยู่ตลอด" ส่วนหน้าจอจริงคือ Panel ลูก (UIPanelController)
/// หยุดเวลาเกมตอนเปิด (ผีไม่เดินมากัดตอนเลือกช่อง)
/// [Claude 2026-11-11 งานข้อ 8]
/// </summary>
[DefaultExecutionOrder(-50)]   // อ่าน ESC ก่อนโต๊ะ/เมนูหยุดเกม — ESC ปิดหน้านี้ ไม่หลุดไปออกจากโต๊ะด้วย
public class SaveSlotPickerUI : MonoBehaviour, ISaveSlotPicker, ISaveSlotLoader
{
    private enum Mode { Save, Load }
    [Header("หน้าจอ")]
    [SerializeField] private UIPanelController panel;
    [SerializeField] private SaveSlotCardUI[] cards;

    [Header("รายละเอียดช่องที่ชี้อยู่ (กระดาษด้านขวา)")]
    [SerializeField] private UnityEngine.UI.RawImage previewImage;
    [SerializeField] private GameObject previewEmpty;
    [SerializeField] private TMP_Text previewTitle;
    [SerializeField] private TMP_Text previewDate;
    [SerializeField] private TMP_Text previewPlayTime;
    [SerializeField] private TMP_Text previewLocation;

    [Header("ข้อความที่เปลี่ยนตามโหมด (เซฟ/โหลด)")]
    [SerializeField] private LocalizedText titleLabel;
    [SerializeField] private LocalizedText hintLabel;
    [SerializeField] private LocalizedText confirmLabel;

    [Header("ถามก่อนเขียนทับ / ก่อนโหลด")]
    [SerializeField] private UIPanelController confirmPanel;
    [SerializeField] private UnityEngine.UI.Button confirmYes;
    [SerializeField] private UnityEngine.UI.Button confirmNo;

    [Header("ตัวเลือก")]
    [SerializeField] private bool pauseWhileOpen = true;
    [Tooltip("เซฟเสร็จแล้วค้างหน้าจอให้เห็นการ์ดใหม่กี่วินาทีก่อนปิด")]
    [SerializeField, Min(0f)] private float closeDelayAfterSave = 0.8f;

    /// <summary>หน้าจอเปิดอยู่ไหม — กล้องโต๊ะ/โต๊ะเช็คเพื่อไม่แย่งเมาส์และปุ่ม ESC</summary>
    public static bool IsOpen { get; private set; }

    private Mode mode;
    private bool confirmBeforeLoad;
    private System.Action onLoadClosed;
    private SaveRequest request;
    private bool busy;
    private int pendingSlot = -1;
    private float savedTimeScale = 1f;
    private CursorLockMode savedLock;
    private bool savedVisible;
    private readonly List<Texture2D> loadedThumbs = new List<Texture2D>();

    private void Awake()
    {
        ServiceLocator.Register<ISaveSlotPicker>(this);
        ServiceLocator.Register<ISaveSlotLoader>(this);

        foreach (SaveSlotCardUI card in cards)
        {
            if (card == null) continue;
            card.Hovered += OnCardHovered;
            card.Clicked += OnCardClicked;
        }
        if (confirmYes != null) confirmYes.onClick.AddListener(ConfirmOverwrite);
        if (confirmNo != null) confirmNo.onClick.AddListener(CancelOverwrite);

        if (panel != null) panel.HideImmediate();
        if (confirmPanel != null) confirmPanel.HideImmediate();
        IsOpen = false;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ServiceLocator.GetOptional<ISaveSlotPicker>(), this)) ServiceLocator.Unregister<ISaveSlotPicker>();
        if (ReferenceEquals(ServiceLocator.GetOptional<ISaveSlotLoader>(), this)) ServiceLocator.Unregister<ISaveSlotLoader>();
        if (IsOpen) RestoreGameState();
        IsOpen = false;
        ReleaseThumbs();
    }

    private void Update()
    {
        if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;

        EscapeGuard.MarkHandled();
        if (busy) return;
        if (pendingSlot >= 0) CancelOverwrite();
        else Close(false);
    }

    // ───────── ISaveSlotPicker ─────────

    public void OpenSavePicker(SaveRequest req)
    {
        if (IsOpen) return;
        mode = Mode.Save;
        request = req;
        OpenInternal();
    }

    public void OpenLoadPicker(bool confirmFirst, System.Action onClosed)
    {
        if (IsOpen) return;
        mode = Mode.Load;
        request = default;
        confirmBeforeLoad = confirmFirst;
        onLoadClosed = onClosed;
        OpenInternal();
    }

    private void OpenInternal()
    {
        bool load = mode == Mode.Load;
        if (titleLabel != null) titleLabel.SetKey(load ? "LOAD_TITLE" : "SAVE_TITLE");
        if (hintLabel != null) hintLabel.SetKey(load ? "LOAD_HINT" : "SAVE_HINT");
        if (confirmLabel != null) confirmLabel.SetKey(load ? "LOAD_CONFIRM" : "SAVE_OVERWRITE");

        busy = false;
        pendingSlot = -1;
        IsOpen = true;

        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseWhileOpen)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        RefreshCards();
        if (confirmPanel != null) confirmPanel.HideImmediate();
        if (panel != null) panel.Show();
        Audio()?.PlayMenuSound(MenuSoundType.Click);
    }

    // ───────── การ์ด ─────────

    private void RefreshCards()
    {
        ReleaseThumbs();
        ServiceLocator.TryGet(out SaveManager save);
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();

        int count = save != null ? Mathf.Min(cards.Length, save.ManualSlotCount) : 0;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].gameObject.SetActive(i < count);
            if (i >= count) continue;

            SaveSlotInfo info = save.GetSlotInfo(i);
            Texture2D thumb = info.Exists ? SaveThumbnail.Load(i) : null;
            if (thumb != null) loadedThumbs.Add(thumb);
            cards[i].Bind(info, thumb, loc);
            cards[i].SetInteractable(mode == Mode.Save || info.Exists);
        }

        // โหลด: ชี้การ์ดแรกที่มีเซฟก่อน
        SaveSlotCardUI first = null;
        for (int i = 0; i < count; i++)
            if (cards[i] != null && (mode == Mode.Save || cards[i].Info.Exists)) { first = cards[i]; break; }
        if (first == null && count > 0) first = cards[0];
        if (first != null) OnCardHovered(first);
    }

    private void OnCardHovered(SaveSlotCardUI card)
    {
        foreach (SaveSlotCardUI c in cards) if (c != null) c.SetHighlighted(c == card);
        ShowPreview(card);
    }

    private void ShowPreview(SaveSlotCardUI card)
    {
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
        SaveSlotInfo info = card.Info;

        if (previewImage != null)
        {
            previewImage.texture = card.Thumbnail;
            previewImage.enabled = card.Thumbnail != null;
        }
        if (previewEmpty != null) previewEmpty.SetActive(!info.Exists);

        string title = Text(loc, "SAVE_SLOT", "Slot") + " " + (card.Slot + 1);
        SetText(previewTitle, title, loc, FontCategory.Header);
        SetText(previewDate, info.Exists ? info.SavedAtLocal.ToString("dd/MM/yyyy  HH:mm") : "", loc, FontCategory.Default);
        SetText(previewPlayTime, info.Exists ? Text(loc, "SAVE_PLAYTIME", "Play time") + "  " + info.PlayTimeText : "", loc, FontCategory.Default);
        SetText(previewLocation, info.Exists && !string.IsNullOrEmpty(info.LocationKey) ? Text(loc, info.LocationKey, info.LocationKey) : "", loc, FontCategory.Default);
    }

    private void OnCardClicked(SaveSlotCardUI card)
    {
        if (busy || pendingSlot >= 0) return;

        if (mode == Mode.Load)
        {
            if (!card.Info.Exists) return;
            if (confirmBeforeLoad && confirmPanel != null)
            {
                pendingSlot = card.Slot;
                confirmPanel.Show();
                Audio()?.PlayMenuSound(MenuSoundType.Click);
                return;
            }
            LoadSlot(card.Slot);
            return;
        }

        if (card.Info.Exists)
        {
            pendingSlot = card.Slot;
            if (confirmPanel != null) confirmPanel.Show();
            else ConfirmOverwrite();
            Audio()?.PlayMenuSound(MenuSoundType.Click);
            return;
        }
        StartCoroutine(SaveRoutine(card.Slot));
    }

    private void ConfirmOverwrite()
    {
        if (pendingSlot < 0 || busy) return;
        int slot = pendingSlot;
        pendingSlot = -1;
        if (confirmPanel != null) confirmPanel.Hide();
        if (mode == Mode.Load) LoadSlot(slot);
        else StartCoroutine(SaveRoutine(slot));
    }

    private void CancelOverwrite()
    {
        pendingSlot = -1;
        if (confirmPanel != null) confirmPanel.Hide();
        Audio()?.PlayMenuSound(MenuSoundType.Back);
    }

    private IEnumerator SaveRoutine(int slot)
    {
        busy = true;
        bool ok = ServiceLocator.TryGet(out SaveManager save)
                  && save.TrySaveToSlot(slot, SaveReason.Manual, request.SpawnPoint, request.LocationKey);

        if (!ok)
        {
            Audio()?.PlayMenuSound(MenuSoundType.Error);
            busy = false;
            yield break;
        }

        Audio()?.PlayMenuSound(MenuSoundType.Click);
        RefreshCards();                                   // โชว์การ์ดใหม่ (รูป + เวลาใหม่) ให้เห็นก่อนปิด
        if (slot >= 0 && slot < cards.Length && cards[slot] != null) OnCardHovered(cards[slot]);

        yield return new WaitForSecondsRealtime(closeDelayAfterSave);
        busy = false;
        Close(true);
    }

    private void LoadSlot(int slot)
    {
        if (!ServiceLocator.TryGet(out SaveManager save)) return;
        busy = true;
        Audio()?.PlayMenuSound(MenuSoundType.Click);
        // LoadSlot เปิดซีนใหม่ (ตั้งเวลาเกมกลับปกติให้เอง) — หน้าจอนี้หายไปพร้อมซีนเก่า
        if (!save.LoadSlot(slot))
        {
            busy = false;
            Audio()?.PlayMenuSound(MenuSoundType.Error);
        }
    }

    private void Close(bool saved)
    {
        if (!IsOpen) return;
        IsOpen = false;
        pendingSlot = -1;

        if (confirmPanel != null) confirmPanel.HideImmediate();
        if (panel != null) panel.Hide();
        if (!saved) Audio()?.PlayMenuSound(MenuSoundType.Back);

        RestoreGameState();

        if (mode == Mode.Load)
        {
            System.Action closed = onLoadClosed;
            onLoadClosed = null;
            closed?.Invoke();
            return;
        }

        System.Action<bool> done = request.OnFinished;
        request = default;
        done?.Invoke(saved);
    }

    private void RestoreGameState()
    {
        if (pauseWhileOpen) Time.timeScale = savedTimeScale;
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    private void ReleaseThumbs()
    {
        foreach (Texture2D t in loadedThumbs) if (t != null) Destroy(t);
        loadedThumbs.Clear();
    }

    private static IAudioService Audio() => ServiceLocator.GetOptional<IAudioService>();

    private static string Text(ILocalizationService loc, string key, string fallback)
    {
        if (loc == null) return fallback;
        string s = loc.GetText(key);
        return string.IsNullOrEmpty(s) || s == key ? fallback : s;
    }

    private static void SetText(TMP_Text t, string value, ILocalizationService loc, FontCategory cat)
    {
        if (t == null) return;
        if (loc != null)
        {
            TMP_FontAsset f = loc.GetFont(cat);
            if (f != null) t.font = f;
        }
        t.text = value;
    }
}
