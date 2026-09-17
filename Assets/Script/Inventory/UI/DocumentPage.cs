using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า "เอกสาร" ในสมุด — อ่านจาก IDocumentLog
/// เอกสารที่เก็บได้จะอยู่ถาวร อ่านซ้ำได้ · อันที่ยังไม่เคยอ่านมีจุดแดง
/// </summary>
public class DocumentPage : JournalPage
{
    [Header("สารบัญฝั่งซ้าย")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private JournalEntryButton entryPrefab;

    [Header("เนื้อหาฝั่งขวา")]
    [SerializeField] private GameObject readerPanel;
    [SerializeField] private TMP_Text readerTitle;
    [SerializeField] private TMP_Text readerBody;
    [SerializeField] private Image readerImage;

    [Header("ข้อความตอนยังไม่มีเอกสาร")]
    [SerializeField] private GameObject emptyMessage;

    private readonly List<JournalEntryButton> spawned = new List<JournalEntryButton>();
    private IDocumentLog log;
    private ILocalizationService locService;
    private DocumentData selected;

    private void OnEnable()
    {
        log = ServiceLocator.Get<IDocumentLog>();
        if (log != null) log.OnChanged += Refresh;

        locService = ServiceLocator.Get<ILocalizationService>();
        if (locService != null) locService.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (log != null) log.OnChanged -= Refresh;
        if (locService != null) locService.OnLanguageChanged -= Refresh;
    }

    public override void Refresh()
    {
        if (log == null) log = ServiceLocator.Get<IDocumentLog>();

        ClearList();

        IReadOnlyList<DocumentData> docs = log?.All;
        bool hasAny = docs != null && docs.Count > 0;

        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);

        if (!hasAny)
        {
            ShowDocument(null);
            return;
        }

        for (int i = 0; i < docs.Count; i++)
        {
            DocumentData doc = docs[i];
            if (doc == null) continue;
            SpawnEntry(doc);
        }

        ShowDocument(ListContains(docs, selected) ? selected : docs[0]);
    }

    private void SpawnEntry(DocumentData doc)
    {
        if (entryPrefab == null || listContainer == null) return;

        bool unread = log != null && !log.HasRead(doc.documentId);

        JournalEntryButton entry = Instantiate(entryPrefab, listContainer);
        entry.Setup(doc.icon, doc.Title, unread, () => ShowDocument(doc));
        spawned.Add(entry);
    }

    private void ShowDocument(DocumentData doc)
    {
        selected = doc;

        if (readerPanel != null) readerPanel.SetActive(doc != null);
        if (doc == null) return;

        if (locService == null) locService = ServiceLocator.Get<ILocalizationService>();

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
            readerImage.enabled = doc.pageImage != null;
        }

        // เปิดอ่านแล้ว = เอาจุดแดงออก
        if (log != null && !log.HasRead(doc.documentId))
        {
            log.MarkRead(doc.documentId);
            // MarkRead จะยิง OnChanged -> Refresh() เอง เลยไม่ต้องอัปเดตจุดแดงเองตรงนี้
            return;
        }

        UpdateSelectionHighlight(doc);
    }

    private void UpdateSelectionHighlight(DocumentData doc)
    {
        IReadOnlyList<DocumentData> docs = log?.All;
        if (docs == null) return;

        for (int i = 0; i < spawned.Count && i < docs.Count; i++)
        {
            if (spawned[i] != null) spawned[i].SetSelected(docs[i] == doc);
        }
    }

    private void ClearList()
    {
        foreach (JournalEntryButton e in spawned)
        {
            if (e != null) Destroy(e.gameObject);
        }
        spawned.Clear();
    }

    private static bool ListContains(IReadOnlyList<DocumentData> list, DocumentData target)
    {
        if (list == null || target == null) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == target) return true;
        }
        return false;
    }
}
