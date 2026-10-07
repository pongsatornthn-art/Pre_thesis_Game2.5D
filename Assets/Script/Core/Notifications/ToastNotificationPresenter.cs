using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ป้ายแจ้งเตือนเด้งมุมจอ (หน้าตา) — "บันทึกใหม่: …" / "เศษรูปวาด (3/5)" / "เซฟแล้ว"
/// QuestNotifier (ที่ [STORY]) เป็นคนตัดสินใจข้อความ + เข้าคิว + เล่นเสียง → ตัวนี้แค่โชว์ทีละป้าย
///
/// วางในซีน (ใน UI_Manager):
///   ToastNotification (สคริปต์นี้ — ตัวนี้ต้องเปิดอยู่เสมอ)
///     └ ToastPanel  CanvasGroup + UIPanelController (ไม่มีพื้นป้าย — เจ้าของขอตัวหนังสืออย่างเดียว)
///         ├ Icon   (Image ไม่บังคับ — รูปตามชนิดป้าย)
///         └ Text   (TextMeshProUGUI)
///   แล้วลาก ToastNotification ใส่ช่อง Presenter ของ QuestNotifier
/// ป้ายไม่บังเมาส์ (ปิด Raycast Target ให้เอง) · เวลาใช้แบบ unscaled (ขึ้นได้ตอนเปิดสมุด/หยุดเกม)
/// </summary>
public class ToastNotificationPresenter : MonoBehaviour, INotificationPresenter
{
    [Serializable]
    private struct KindIcon
    {
        public NotificationKind kind;
        public Sprite icon;
    }

    [SerializeField] private UIPanelController panel;
    [SerializeField] private TMP_Text text;

    [Tooltip("รูปไอคอนตามชนิดป้าย (ไม่บังคับ) — ไม่มีรูปของชนิดนั้น = ซ่อนไอคอน")]
    [SerializeField] private Image icon;
    [SerializeField] private KindIcon[] icons = new KindIcon[0];

    [Header("ตัวหนังสืออย่างเดียว (ไม่มีกรอบ) — ขอบดำรอบตัวอักษรให้อ่านออกทุกพื้นหลัง")]
    [SerializeField, Range(0f, 0.5f)] private float outlineWidth = 0.2f;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.85f);

    [Tooltip("รอให้ป้ายจางหายก่อนขึ้นป้ายถัดไป (วินาทีจริง)")]
    [SerializeField, Min(0f)] private float gapSeconds = 0.25f;

    private void Awake()
    {
        if (panel != null) panel.HideImmediate();
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;   // ป้ายห้ามบังการคลิก
    }

    public IEnumerator Show(NotificationMessage message)
    {
        if (panel == null || text == null)
        {
            Debug.LogWarning("[ToastNotification] ยังไม่ได้ลาก Panel / Text — โชว์ป้ายไม่ได้", this);
            yield break;
        }

        text.text = message.Text;
        ILocalizationService loc = ServiceLocator.GetOptional<ILocalizationService>();
        if (loc != null) text.font = loc.GetFont(FontCategory.Default);

        if (icon != null)
        {
            Sprite s = FindIcon(message.Kind);
            icon.sprite = s;
            icon.gameObject.SetActive(s != null);
        }

        panel.Show();
        ApplyOutline();   // หลัง Show — ตัวหนังสือต้องเปิดอยู่ก่อนถึงใส่ขอบได้ · เปลี่ยนฟอนต์ตามภาษาแล้วต้องใส่ใหม่ทุกครั้ง
        yield return new WaitForSecondsRealtime(message.HoldSeconds);
        panel.Hide();
        yield return new WaitForSecondsRealtime(gapSeconds);
    }

    private void ApplyOutline()
    {
        if (outlineWidth <= 0f) return;
        text.outlineWidth = outlineWidth;   // สร้าง material ชั่วคราวตอนเล่นเท่านั้น (ไม่แตะไฟล์ฟอนต์)
        text.outlineColor = outlineColor;
    }

    private Sprite FindIcon(NotificationKind kind)
    {
        foreach (KindIcon k in icons) if (k.kind == kind) return k.icon;
        return null;
    }
}
