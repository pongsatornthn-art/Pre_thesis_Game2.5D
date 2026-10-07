using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า "คลังความทรงจำ" ในสมุด (ปุ่มลัด L) — แบบ Codex เกมผี
///   ซ้าย = รายการช่อง · กลาง = รูปใหญ่ · ขวา = ชื่อ + เรื่องย่อ
///   ยังไม่ปลด = รูปเดียวกันย้อมดำ (เงา) + "???" · ปลดแล้ว = รูปจริง + ข้อความ · ปลดแล้วปลดถาวร
///   ช่องที่ปลดใหม่ยังไม่ได้เปิดดู = จุดแดง (เปิดดูแล้วหาย)
///
/// หน้าตาทั้งหมดอยู่ในซีน (แผง/รูป/ตัวหนังสือ/prefab แถว) — สคริปต์นี้แค่ใส่ข้อมูล (STRUCTURE_CHECKLIST B1)
/// แถวรายการสร้างจาก prefab เพราะจำนวนช่องไม่ตายตัว (ข้อยกเว้นที่ตกลงไว้) · ใช้ JournalEntryButton เดียวกับหน้าอื่น
/// </summary>
public class MemoryArchivePage : JournalPage
{
    [Header("รายการฝั่งซ้าย")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private JournalEntryButton entryPrefab;

    [Header("รูปตรงกลาง")]
    [SerializeField] private Image bigImage;

    [Header("ข้อความฝั่งขวา")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("ตอนยังไม่ปลด")]
    [Tooltip("สีย้อมรูปตอนยังไม่ปลด (ดำ = เงา)")]
    [SerializeField] private Color lockedTint = Color.black;
    [Tooltip("key ข้อความแทนชื่อ/เรื่องย่อตอนยังไม่ปลด")]
    [SerializeField] private string lockedTextKey = "MEMORY_LOCKED";

    [Header("ตอนคลังว่าง")]
    [SerializeField] private GameObject emptyMessage;

    private readonly List<JournalEntryButton> spawned = new List<JournalEntryButton>();
    private IMemoryArchive archive;
    private ILocalizationService loc;
    private MemoryEntryData selected;

    private void Reset()
    {
        // ค่าเริ่มต้นของหน้านี้ (PageId = "memory", ปุ่มลัด L)
        FieldInfo idField = typeof(JournalPage).GetField("pageId", BindingFlags.NonPublic | BindingFlags.Instance);
        idField?.SetValue(this, "memory");
        FieldInfo keyField = typeof(JournalPage).GetField("shortcutKey", BindingFlags.NonPublic | BindingFlags.Instance);
        keyField?.SetValue(this, KeyCode.L);
    }

    private void OnEnable()
    {
        archive = ServiceLocator.GetOptional<IMemoryArchive>();
        if (archive != null) archive.OnChanged += Refresh;

        loc = ServiceLocator.GetOptional<ILocalizationService>();
        if (loc != null) loc.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (archive != null) archive.OnChanged -= Refresh;
        if (loc != null) loc.OnLanguageChanged -= Refresh;
    }

    public override void Refresh()
    {
        if (archive == null) archive = ServiceLocator.GetOptional<IMemoryArchive>();
        if (loc == null) loc = ServiceLocator.GetOptional<ILocalizationService>();

        ClearList();

        IReadOnlyList<MemoryEntryData> entries = archive?.Entries;
        bool hasAny = entries != null && entries.Count > 0;
        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);
        if (!hasAny) { ShowDetail(null); return; }

        foreach (MemoryEntryData e in entries)
        {
            if (e == null || entryPrefab == null || listContainer == null) continue;

            bool open = archive.IsUnlocked(e);
            MemoryEntryData captured = e;
            JournalEntryButton row = Instantiate(entryPrefab, listContainer);
            row.Setup(e.image, open ? Text(e.titleKey) : Text(lockedTextKey), archive.IsNew(e), () => ShowDetail(captured));
            row.SetIconTint(open ? Color.white : lockedTint);
            spawned.Add(row);
        }

        // ช่องที่เลือกไว้ยังอยู่ → คงไว้ · ไม่งั้นเลือกช่องแรก
        ShowDetail(selected != null && Contains(entries, selected) ? selected : entries[0]);
    }

    private void ShowDetail(MemoryEntryData entry)
    {
        selected = entry;
        bool open = entry != null && archive != null && archive.IsUnlocked(entry);

        if (bigImage != null)
        {
            bigImage.sprite = entry != null ? entry.image : null;
            bigImage.enabled = bigImage.sprite != null;
            bigImage.color = open ? Color.white : lockedTint;
        }

        SetText(titleText, entry == null ? "" : open ? Text(entry.titleKey) : Text(lockedTextKey), FontCategory.Header);
        SetText(descriptionText, entry == null ? "" : open ? Text(entry.descriptionKey) : Text(lockedTextKey), FontCategory.Default);

        // ไฮไลท์ + เปิดดูแล้วเอาจุดแดงออก
        IReadOnlyList<MemoryEntryData> entries = archive?.Entries;
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null || entries == null || i >= entries.Count) continue;
            bool isThis = entries[i] == entry;
            spawned[i].SetSelected(isThis);
            if (isThis && open)
            {
                spawned[i].SetUnread(false);
            }
        }
        if (open) archive.MarkSeen(entry);   // OnChanged → Refresh ไม่วนซ้ำ เพราะ MarkSeen ยิงเฉพาะครั้งแรก
    }

    private void SetText(TMP_Text target, string value, FontCategory font)
    {
        if (target == null) return;
        target.text = value;
        if (loc != null) target.font = loc.GetFont(font);
    }

    private string Text(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        return loc != null ? loc.GetText(key) : key;
    }

    private static bool Contains(IReadOnlyList<MemoryEntryData> list, MemoryEntryData target)
    {
        for (int i = 0; i < list.Count; i++) if (list[i] == target) return true;
        return false;
    }

    private void ClearList()
    {
        foreach (JournalEntryButton e in spawned) if (e != null) Destroy(e.gameObject);
        spawned.Clear();
    }
}
