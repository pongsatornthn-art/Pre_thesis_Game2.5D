using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// การ์ด 1 ช่องในหน้าจอเลือกช่องเซฟ — กรอบ + รูปฉากตอนเซฟ + วันที่ + เวลา + ชื่อห้อง
/// ชี้เมาส์ = กรอบเปลี่ยนเป็นแบบเลือก + ส่งให้หน้าจอโชว์รายละเอียดด้านขวา · คลิก = เลือกช่องนี้
/// (SaveSlotPickerUI เป็นคนเติมข้อมูลให้ ตัวนี้แค่แสดงผล)
/// [Claude 2026-11-11 งานข้อ 8]
/// </summary>
public class SaveSlotCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;

    [Header("กรอบ")]
    [SerializeField] private Image frame;
    [SerializeField] private Sprite normalFrame;
    [SerializeField] private Sprite selectedFrame;

    [Header("ข้อมูล")]
    [SerializeField] private RawImage thumbnail;
    [Tooltip("ข้อความ 'ว่าง' ตอนช่องยังไม่มีเซฟ")]
    [SerializeField] private GameObject emptyLabel;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text locationText;

    public event Action<SaveSlotCardUI> Hovered;
    public event Action<SaveSlotCardUI> Clicked;

    public int Slot { get; private set; }
    public SaveSlotInfo Info { get; private set; }
    public Texture Thumbnail => thumbnail != null ? thumbnail.texture : null;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    public void Bind(SaveSlotInfo info, Texture2D thumb, ILocalizationService loc)
    {
        Slot = info.Slot;
        Info = info;

        if (thumbnail != null)
        {
            thumbnail.texture = thumb;
            thumbnail.enabled = thumb != null;
        }
        if (emptyLabel != null) emptyLabel.SetActive(!info.Exists);

        SetText(dateText, info.Exists ? info.SavedAtLocal.ToString("dd/MM/yyyy") : "", loc);
        SetText(timeText, info.Exists ? info.SavedAtLocal.ToString("HH:mm") : "", loc);
        string location = info.Exists && !string.IsNullOrEmpty(info.LocationKey)
            ? (loc != null ? loc.GetText(info.LocationKey) : info.LocationKey)
            : "";
        SetText(locationText, location, loc);

        SetHighlighted(false);
    }

    /// <summary>หน้าโหลด: ช่องว่างกดไม่ได้ + จางลง</summary>
    public void SetInteractable(bool on)
    {
        if (button != null) button.interactable = on;
        Color c = on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.7f);
        if (frame != null) frame.color = c;
        if (emptyLabel != null && emptyLabel.TryGetComponent(out TMP_Text t)) t.alpha = on ? 0.55f : 0.35f;
    }

    public bool IsInteractable => button == null || button.interactable;

    public void SetHighlighted(bool on)
    {
        if (frame == null) return;
        Sprite s = on ? selectedFrame : normalFrame;
        if (s != null) frame.sprite = s;
    }

    public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(this);
    public void OnPointerExit(PointerEventData eventData) { }

    private static void SetText(TMP_Text t, string value, ILocalizationService loc)
    {
        if (t == null) return;
        if (loc != null)
        {
            TMP_FontAsset f = loc.GetFont(FontCategory.Default);
            if (f != null) t.font = f;
        }
        t.text = value;
    }
}
