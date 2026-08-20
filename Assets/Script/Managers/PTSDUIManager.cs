using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สคริปต์จัดการหน้าจอ UI ตอนที่หลุดเข้าไปในโลก PTSD
/// เกาะติดกับ PTSDManager แบบ OOP ชิลๆ ไม่ต้องเรียกหากันให้วุ่นวาย
/// </summary>
public class PTSDUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ลาก Image ภาพเบลอๆ หลอนๆ เลือดสาด หรือ Effect ภาพหน้าจอมาใส่ตรงนี้")]
    public Image ptsdOverlayImage;
    
    [Tooltip("ลาก Text คำเตือนเวลาเข้าโลกหลอนมาใส่ตรงนี้ (ถ้ามี)")]
    public GameObject warningText;

    private void OnEnable()
    {
        // พอเปิดสคริปต์ปุ๊บ ก็ไปบอก PTSDManager ว่า "ถ้าโลกสลับเมื่อไหร่ เรียกฟังก์ชันฉันด้วยนะ!"
        PTSDManager.OnPTSDStateChanged += HandlePTSDStateChange;
    }

    private void OnDisable()
    {
        // ปิดสคริปต์ปุ๊บ เลิกเกาะ Event ทันที ป้องกันบัค Error จุกจิก
        PTSDManager.OnPTSDStateChanged -= HandlePTSDStateChange;
    }

    private void Start()
    {
        // เริ่มเกมมาต้องหน้าจอใสปิ๊ง โลกปกติ
        if (ptsdOverlayImage != null)
        {
            ptsdOverlayImage.gameObject.SetActive(false);
        }
        
        if (warningText != null)
        {
            warningText.SetActive(false);
        }
    }

    /// <summary>
    /// ฟังก์ชันนี้จะถูก Trigger อัตโนมัติเวลา PTSDManager โดนกดเปลี่ยนโลก
    /// </summary>
    /// <param name="isPTSDActive">สถานะว่าตอนนี้หลอนอยู่หรือเปล่า</param>
    private void HandlePTSDStateChange(bool isPTSDActive)
    {
        // ถ้าระบบส่งมาว่าหลอน (true) ก็เปิดภาพหน้าจอหลอนๆ
        if (ptsdOverlayImage != null)
        {
            ptsdOverlayImage.gameObject.SetActive(isPTSDActive);
        }
        
        // เปิดคำเตือนสั่นๆ หรืออะไรก็ว่าไป
        if (warningText != null)
        {
            warningText.SetActive(isPTSDActive);
        }

        if (isPTSDActive)
        {
            Debug.Log("👁️‍🗨️ UI: ภาพตัดเข้าโหมดหลอนแล้ว ระวังตัวด้วย!");
            // เผื่ออนาคตอยากใส่ Animation กล้องสั่น หรือเฟดภาพดำ ก็ใส่ตรงนี้ได้เลย
        }
        else
        {
            Debug.Log("🌈 UI: โล่งอก กลับมาโลกความจริง ภาพชัดแจ๋ว!");
        }
    }
}
