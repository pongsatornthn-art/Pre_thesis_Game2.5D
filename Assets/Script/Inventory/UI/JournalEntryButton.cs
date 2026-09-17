using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ช่องรายการ 1 อันในสมุด — ใช้ร่วมกันทั้งหน้าของสำคัญและหน้าเอกสาร
/// ทำแยกจาก InventorySlotUI เพราะช่องพวกนี้ **ลากย้ายไม่ได้ ทิ้งไม่ได้** ไม่ต้องมีระบบลากของ
/// </summary>
[RequireComponent(typeof(Button))]
public class JournalEntryButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [Tooltip("จุดแดง 'ยังไม่อ่าน' (เว้นว่างได้)")]
    [SerializeField] private GameObject unreadDot;
    [Tooltip("กรอบไฮไลท์ตอนถูกเลือก (เว้นว่างได้)")]
    [SerializeField] private GameObject selectedHighlight;

    private Button button;
    private Action onClick;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    /// <summary>ใส่ข้อมูลลงช่องนี้</summary>
    public void Setup(Sprite iconSprite, string text, bool showUnreadDot, Action clickHandler)
    {
        onClick = clickHandler;

        if (icon != null)
        {
            icon.sprite = iconSprite;
            // ไม่มีรูปก็ซ่อนไอคอนไปเลย จะได้ไม่เห็นกรอบสี่เหลี่ยมเปล่าๆ
            icon.enabled = iconSprite != null;
        }

        if (label != null)
        {
            label.text = text;
            ILocalizationService loc = ServiceLocator.Get<ILocalizationService>();
            if (loc != null)
            {
                TMP_FontAsset font = loc.GetFont(FontCategory.Default);
                if (font != null) label.font = font;
            }
        }
        if (unreadDot != null) unreadDot.SetActive(showUnreadDot);

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);
    }

    public void SetUnread(bool unread)
    {
        if (unreadDot != null) unreadDot.SetActive(unread);
    }
}
