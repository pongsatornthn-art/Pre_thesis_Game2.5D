using UnityEngine;
using UnityEngine.EventSystems;

public class ItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventorySlotUI mySlot;
    private CanvasGroup canvasGroup;
    private Vector3 startPosition;
    private Transform originalParent;

    void Awake()
    {
        // ค้นหา Canvas Group ที่เราแปะไว้ใน Inspector
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPosition = transform.position;
        originalParent = transform.parent;

        // 🌟 พระเอกของเราอยู่ตรงนี้! สั่งให้รูปไอเทม "โปร่งใสต่อเมาส์" ชั่วคราว
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        // ดึงไอเทมขึ้นมาวาดหน้าสุด จะได้ไม่มุดไปอยู่ใต้กรอบ UI อื่นตอนลาก
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // ให้รูปไอเทมลอยตามเมาส์
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 🌟 พอปล่อยเมาส์ปุ๊บ เปิดให้มันกลับมาขวางเมาส์เหมือนเดิม (เพื่อรอบางคนมาจับลากใหม่)
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        // ดึงรูปกลับเข้าช่องแม่เดิมทันที 
        // (ถ้าสลับของสำเร็จเดี๋ยวสคริปต์ UpdateUI จะจัดการรีเฟรชภาพให้เอง)
        transform.SetParent(originalParent);
        transform.position = startPosition;
    }
}