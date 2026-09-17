using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า "ของสำคัญ" ในสมุด — อ่านจากคลังแยก (IKeyItemHolder) ไม่ใช่กระเป๋าปกติ
/// ของในหน้านี้ทิ้งไม่ได้ ลากไม่ได้ กดดูรายละเอียดได้อย่างเดียว
/// </summary>
public class KeyItemPage : JournalPage
{
    [Header("รายการฝั่งซ้าย")]
    [Tooltip("ก้อนที่จะเสกช่องรายการลงไป (ควรมี Grid/Vertical Layout Group)")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private JournalEntryButton entryPrefab;

    [Header("รายละเอียดฝั่งขวา")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;

    [Header("ข้อความตอนไม่มีของ")]
    [SerializeField] private GameObject emptyMessage;

    private readonly List<JournalEntryButton> spawned = new List<JournalEntryButton>();
    private IKeyItemHolder holder;
    private KeyItemData selected;

    private void OnEnable()
    {
        // เกาะ event ไว้ เผื่อเก็บกุญแจใหม่ระหว่างเปิดสมุดค้างไว้
        holder = ServiceLocator.Get<IKeyItemHolder>();
        if (holder != null) holder.OnChanged += Refresh;
    }

    private void OnDisable()
    {
        if (holder != null) holder.OnChanged -= Refresh;
    }

    public override void Refresh()
    {
        if (holder == null) holder = ServiceLocator.Get<IKeyItemHolder>();

        ClearList();

        IReadOnlyList<KeyItemData> keys = holder?.All;
        bool hasAny = keys != null && keys.Count > 0;

        if (emptyMessage != null) emptyMessage.SetActive(!hasAny);

        if (!hasAny)
        {
            ShowDetail(null);
            return;
        }

        foreach (KeyItemData key in keys)
        {
            if (key == null) continue;
            SpawnEntry(key);
        }

        // ถ้าของที่เลือกไว้หายไป (ถูกใช้ไปแล้ว) ให้เด้งกลับไปอันแรก
        ShowDetail(ListContains(keys, selected) ? selected : keys[0]);
    }

    private static bool ListContains(IReadOnlyList<KeyItemData> list, KeyItemData target)
    {
        if (list == null || target == null) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == target) return true;
        }
        return false;
    }

    private void SpawnEntry(KeyItemData key)
    {
        if (entryPrefab == null || listContainer == null) return;

        JournalEntryButton entry = Instantiate(entryPrefab, listContainer);
        entry.Setup(key.icon, key.itemName, false, () => ShowDetail(key));
        spawned.Add(entry);
    }

    private void ShowDetail(KeyItemData key)
    {
        selected = key;

        if (detailPanel != null) detailPanel.SetActive(key != null);
        if (key == null) return;

        if (detailIcon != null)
        {
            detailIcon.sprite = key.icon;
            detailIcon.enabled = key.icon != null;
        }
        if (detailName != null) detailName.text = key.itemName;
        if (detailDescription != null) detailDescription.text = key.description;

        // ไฮไลท์ช่องที่เลือก
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            spawned[i].SetSelected(holder != null && i < holder.All.Count && holder.All[i] == key);
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
}
