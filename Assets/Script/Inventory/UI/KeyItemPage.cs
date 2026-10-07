using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้า "ของสำคัญ" (K) ในสมุด — อ่านจากคลังแยก (IKeyItemHolder) ไม่ใช่กระเป๋าปกติ · ทิ้งไม่ได้ ลากไม่ได้ ดูได้อย่างเดียว
///
/// หน้าตา (เจ้าของออกแบบ 2026-10-07 — ดัดแปลงอาร์ตจากหน้ากระเป๋า I):
///   ซ้าย  = ช่องของ 8 ช่อง (KeyItemSlot วางในซีนตายตัว — แทนที่รูป+ชื่อตัวละคร ซึ่งสั่งซ่อนผ่านช่อง Hide While Open)
///   กลาง = รูปใหญ่ (Description Image ของไอเทม · ไม่มีใช้ไอคอน)
///   ขวา  = ชื่อ + ข้อความยาวเต็มแถบ (ของสำคัญมีเนื้อเรื่อง)
/// ของเกินจำนวนช่อง → เตือนใน Console (เพิ่มช่องในซีนได้เลย ไม่ต้องแก้โค้ด)
/// </summary>
public class KeyItemPage : JournalPage
{
    [Header("ซ้าย — ช่องของ (เรียงตามลำดับที่เก็บได้)")]
    [SerializeField] private List<KeyItemSlot> slots = new List<KeyItemSlot>();

    [Header("กลาง — รูปใหญ่")]
    [SerializeField] private Image bigImage;

    [Header("ขวา — ข้อความ")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("ซ่อนตอนยังไม่ได้เลือกอะไร (เช่น กรอบรูปกลาง + กระดาษข้อความขวา)")]
    [SerializeField] private GameObject[] detailParts = new GameObject[0];

    [Tooltip("ข้อความตอนยังไม่มีของ (เว้นว่างได้)")]
    [SerializeField] private GameObject emptyMessage;

    private IKeyItemHolder holder;
    private ILocalizationService locService;
    private KeyItemData selected;

    private void Awake()
    {
        foreach (KeyItemSlot s in slots) if (s != null) s.Bind(this);
    }

    private void OnEnable()
    {
        // เกาะ event ไว้ เผื่อเก็บของใหม่ระหว่างเปิดสมุดค้างไว้
        holder = ServiceLocator.GetOptional<IKeyItemHolder>();
        if (holder != null) holder.OnChanged += Refresh;

        locService = ServiceLocator.GetOptional<ILocalizationService>();
        if (locService != null) locService.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (holder != null) holder.OnChanged -= Refresh;
        if (locService != null) locService.OnLanguageChanged -= Refresh;
    }

    public override void Refresh()
    {
        if (holder == null) holder = ServiceLocator.GetOptional<IKeyItemHolder>();
        IReadOnlyList<KeyItemData> keys = holder?.All;
        int count = keys?.Count ?? 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null) slots[i].Set(i < count ? keys[i] : null);
        }
        if (count > slots.Count)
            Debug.LogWarning($"[KeyItemPage] ของสำคัญมี {count} ชิ้น แต่มีช่องแค่ {slots.Count} — เพิ่มช่อง KeyItemSlot ในซีนแล้วลากใส่ Slots", this);

        if (emptyMessage != null) emptyMessage.SetActive(count == 0);

        // ของที่เลือกไว้หายไป (ถูกใช้เปิดประตูแล้ว) → เด้งไปชิ้นแรก
        Select(Contains(keys, selected) ? selected : (count > 0 ? keys[0] : null));
    }

    /// <summary>โชว์รายละเอียดชิ้นนี้ (ช่องเป็นคนเรียกตอนคลิก) · null = ซ่อนรายละเอียด</summary>
    internal void Select(KeyItemData item)
    {
        selected = item;
        foreach (KeyItemSlot s in slots) if (s != null) s.SetSelected(item != null && s.Item == item);
        foreach (GameObject go in detailParts) if (go != null) go.SetActive(item != null);
        if (item == null) return;

        if (bigImage != null)
        {
            Sprite big = item.descriptionImage != null ? item.descriptionImage : item.icon;
            bigImage.sprite = big;
            bigImage.enabled = big != null;
            bigImage.preserveAspect = true;
        }

        if (locService == null) locService = ServiceLocator.GetOptional<ILocalizationService>();
        if (nameText != null)
        {
            nameText.text = item.DisplayName;
            if (locService != null) nameText.font = locService.GetFont(FontCategory.Header);
        }
        if (descriptionText != null)
        {
            descriptionText.text = item.DisplayDescription;
            if (locService != null) descriptionText.font = locService.GetFont(FontCategory.Default);
        }
    }

    private static bool Contains(IReadOnlyList<KeyItemData> list, KeyItemData target)
    {
        if (list == null || target == null) return false;
        for (int i = 0; i < list.Count; i++) if (list[i] == target) return true;
        return false;
    }
}
