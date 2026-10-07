using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า "เอกสาร" (N) ในสมุด — อ่านจาก IDocumentLog · เอกสารเก็บถาวร อ่านซ้ำได้ · ยังไม่อ่าน = จุดแดง
///
/// หน้าตา (เจ้าของออกแบบ 2026-10-07):
///   หน้ากระดาษเปล่า · ซ้าย/ขวาว่าง · ตรงกลาง = "ลิ้นชัก" แถวยาวเลื่อนขึ้นลงได้ (ScrollRect)
///   คลิกแถว → เปิด "หน้าอ่านเต็ม" ทับสมุด (ข้อความยาวไม่ยัดในช่องเล็ก — อ่านสบาย)
///   ปิดหน้าอ่าน: ปุ่มปิด / คลิกขวา / Esc (Esc ปิดแค่หน้าอ่าน ไม่ปิดสมุด)
/// แถวสร้างจาก prefab (JournalEntryButton) — จำนวนเอกสารไม่แน่นอน (ข้อยกเว้น B1)
/// </summary>
public class DocumentPage : JournalPage
{
    [Header("ลิ้นชัก — แถวรายการตรงกลาง")]
    [Tooltip("Content ของ ScrollRect (ควรมี Vertical Layout Group + Content Size Fitter)")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private JournalEntryButton entryPrefab;

    [Header("หน้าอ่านเต็ม (ทับสมุด)")]
    [SerializeField] private UIPanelController readerPanel;
    [SerializeField] private TMP_Text readerTitle;
    [SerializeField] private TMP_Text readerBody;
    [SerializeField] private Image readerImage;
    [SerializeField] private Button readerCloseButton;
    [Tooltip("ScrollRect ของเนื้อหา (ไม่บังคับ) — เปิดเอกสารใหม่แล้วเลื่อนกลับขึ้นบนสุด")]
    [SerializeField] private ScrollRect readerScroll;

    [Header("ข้อความตอนยังไม่มีเอกสาร")]
    [SerializeField] private GameObject emptyMessage;

    private readonly List<JournalEntryButton> spawned = new List<JournalEntryButton>();
    private readonly List<DocumentData> spawnedDocs = new List<DocumentData>();
    private IDocumentLog log;
    private ILocalizationService locService;
    private DocumentData reading;

    private bool IsReading => readerPanel != null && readerPanel.IsVisible;

    private void Awake()
    {
        if (readerCloseButton != null) readerCloseButton.onClick.AddListener(CloseReader);
        if (readerPanel != null) readerPanel.HideImmediate();
    }

    private void OnEnable()
    {
        log = ServiceLocator.GetOptional<IDocumentLog>();
        if (log != null) log.OnChanged += Refresh;

        locService = ServiceLocator.GetOptional<ILocalizationService>();
        if (locService != null) locService.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (log != null) log.OnChanged -= Refresh;
        if (locService != null) locService.OnLanguageChanged -= OnLanguageChanged;
    }

    private void Update()
    {
        if (IsReading && Input.GetMouseButtonDown(1)) CloseReader();
    }

    public override void Hide()
    {
        base.Hide();
        if (readerPanel != null) readerPanel.HideImmediate();   // สลับแท็บ/ปิดสมุด = ปิดหน้าอ่านด้วย
        reading = null;
    }

    public override void HideImmediate()
    {
        base.HideImmediate();
        if (readerPanel != null) readerPanel.HideImmediate();
        reading = null;
    }

    /// <summary>Esc: หน้าอ่านเปิดอยู่ → ปิดแค่หน้าอ่าน (ไม่ปิดสมุด)</summary>
    public override bool HandleBack()
    {
        if (!IsReading) return false;
        CloseReader();
        return true;
    }

    public override void Refresh()
    {
        if (log == null) log = ServiceLocator.GetOptional<IDocumentLog>();

        ClearList();

        IReadOnlyList<DocumentData> docs = log?.All;
        bool hasAny = docs != null && docs.Count > 0;
        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);
        if (!hasAny) return;

        foreach (DocumentData doc in docs)
        {
            if (doc == null || entryPrefab == null || listContainer == null) continue;

            bool unread = log != null && !log.HasRead(doc.documentId);
            JournalEntryButton entry = Instantiate(entryPrefab, listContainer);
            entry.Setup(doc.icon, doc.Title, unread, () => OpenReader(doc));
            entry.SetSelected(doc == reading);
            spawned.Add(entry);
            spawnedDocs.Add(doc);
        }
    }

    private void OpenReader(DocumentData doc)
    {
        if (doc == null) return;
        reading = doc;
        FillReader(doc);
        if (readerPanel != null) readerPanel.Show();
        if (readerScroll != null) readerScroll.verticalNormalizedPosition = 1f;

        for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) spawned[i].SetSelected(spawnedDocs[i] == doc);

        // เปิดอ่านแล้ว = เอาจุดแดงออก (MarkRead ยิง OnChanged → Refresh วาดแถวใหม่เอง)
        if (log != null && !log.HasRead(doc.documentId)) log.MarkRead(doc.documentId);
    }

    private void FillReader(DocumentData doc)
    {
        if (locService == null) locService = ServiceLocator.GetOptional<ILocalizationService>();

        if (readerTitle != null)
        {
            readerTitle.text = doc.Title;
            if (locService != null) readerTitle.font = locService.GetFont(FontCategory.Header);
        }
        if (readerBody != null)
        {
            readerBody.text = doc.Body;
            if (locService != null) readerBody.font = locService.GetFont(FontCategory.Default);
        }
        if (readerImage != null)
        {
            readerImage.sprite = doc.pageImage;
            readerImage.gameObject.SetActive(doc.pageImage != null);
        }
    }

    private void CloseReader()
    {
        if (readerPanel != null) readerPanel.Hide();
        ServiceLocator.GetOptional<IAudioService>()?.PlayMenuSound(MenuSoundType.Back);
    }

    private void OnLanguageChanged()
    {
        Refresh();
        if (IsReading && reading != null) FillReader(reading);
    }

    private void ClearList()
    {
        foreach (JournalEntryButton e in spawned) if (e != null) Destroy(e.gameObject);
        spawned.Clear();
        spawnedDocs.Clear();
    }
}
