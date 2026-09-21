using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// คอมโพเนนต์แสดงผลแถวเป้าหมายเควส 1 รายการในหน้าสมุด
/// รองรับการแสดงผลแบบขีดฆ่า (<s>) และลดความสว่างของสีเมื่อทำสำเร็จแล้ว
/// </summary>
public class QuestEntryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image checkmark;

    [Header("สีข้อความ")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);

    /// <summary>กำหนดข้อความและสถานะความสำเร็จของเป้าหมาย</summary>
    public void Setup(string text, bool isDone, TMP_FontAsset font = null)
    {
        if (label != null)
        {
            if (font != null) label.font = font;

            // ใช้แท็ก <s> ของ TextMeshPro สำหรับขีดฆ่าข้อความที่ทำสำเร็จแล้ว
            label.text = isDone ? $"<s>{text}</s>" : text;
            label.color = isDone ? completedColor : normalColor;
        }

        if (checkmark != null)
        {
            checkmark.gameObject.SetActive(isDone);
        }
    }
}
