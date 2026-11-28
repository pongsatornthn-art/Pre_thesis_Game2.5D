using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-28] หน้า "Tutorials" ในสมุด — รายชื่อคำสอนที่เคยเด้งแล้ว · กดชื่อ = เปิดการ์ดดูซ้ำ
/// หน้าตาเหมือนหน้าเอกสาร: ลิ้นชักแถว (ScrollRect) ตรงกลาง · แถวใช้ prefab JournalEntryButton ตัวเดิม
/// ลากใส่ช่อง Pages ของ JournalController ได้เลย (ไม่ต้องแก้ JournalController)
/// Esc ระหว่างการ์ดเปิด = ปิดแค่การ์ด (ไม่ปิดสมุด) · สลับแท็บ/ปิดสมุด = ปิดการ์ดด้วย
/// </summary>
public class TutorialPage : JournalPage
{
    [Header("ลิ้นชัก — แถวรายการตรงกลาง")]
    [Tooltip("Content ของ ScrollRect (ควรมี Vertical Layout Group + Content Size Fitter)")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private JournalEntryButton entryPrefab;

    [Header("ข้อความตอนยังไม่มีคำสอน")]
    [SerializeField] private GameObject emptyMessage;

    private readonly List<JournalEntryButton> spawned = new List<JournalEntryButton>();
    private ITutorialService service;
    private ILocalizationService locService;

    private void OnEnable()
    {
        service = ServiceLocator.GetOptional<ITutorialService>();
        if (service != null) service.OnChanged += Refresh;

        locService = ServiceLocator.GetOptional<ILocalizationService>();
        if (locService != null) locService.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (service != null) service.OnChanged -= Refresh;
        if (locService != null) locService.OnLanguageChanged -= Refresh;
    }

    public override void Hide()
    {
        base.Hide();
        service?.CloseCurrent();
    }

    public override void HideImmediate()
    {
        base.HideImmediate();
        service?.CloseCurrent();
    }

    /// <summary>Esc: การ์ดเปิดอยู่ → ปิดแค่การ์ด</summary>
    public override bool HandleBack()
    {
        if (service == null || !service.IsShowing) return false;
        service.CloseCurrent();
        return true;
    }

    public override void Refresh()
    {
        if (service == null)
        {
            service = ServiceLocator.GetOptional<ITutorialService>();
            if (service != null && isActiveAndEnabled) service.OnChanged += Refresh;
        }

        foreach (JournalEntryButton e in spawned) if (e != null) Destroy(e.gameObject);
        spawned.Clear();

        IReadOnlyList<TutorialData> seen = service?.SeenForJournal;
        bool hasAny = seen != null && seen.Count > 0;
        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);
        if (!hasAny || entryPrefab == null || listContainer == null) return;

        foreach (TutorialData t in seen)
        {
            TutorialData tut = t;   // กัน closure จับตัวแปรวน
            JournalEntryButton entry = Instantiate(entryPrefab, listContainer);
            entry.Setup(tut.listIcon, tut.Title, false, () => service?.Replay(tut));
            spawned.Add(entry);
        }
    }
}
