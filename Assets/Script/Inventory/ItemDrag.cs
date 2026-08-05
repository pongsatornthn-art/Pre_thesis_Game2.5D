using UnityEngine;
using UnityEngine.EventSystems; // ⭐ สำคัญมาก: ต้องมีเพื่อใช้ระบบเมาส์ลาก

public class ItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventorySlotUI mySlot;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Vector2 startPosition;
    private Transform originalParent;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>(); // หาตัว Canvas หลัก
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // ถ้าช่องนั้นว่างเปล่า (ไม่มีไอเทม) ห้ามลาก
        if (mySlot == null || mySlot.item == null) return;

        startPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;

        // ดึงรูปขึ้นมาไว้ชั้นบนสุด จะได้ไม่โดนช่องอื่นบังตอนลาก
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        // 💥 ปิดบล็อก Raycast รูปไอเทมตัวเองชั่วคราว เพื่อให้เมาส์สามารถทะลุไปกดช่อง Slot ข้างล่างได้
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (mySlot == null || mySlot.item == null) return;
        // ให้รูปขยับตามเมาส์
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (mySlot == null || mySlot.item == null) return;

        // ดึงรูปกลับเข้าที่เดิมของตัวเองก่อน (ส่วนการสลับของ โค้ดของ Slot จะเป็นคนทำ)
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = startPosition;

        // เปิด Raycast กลับมาให้รูปคลิกได้ตามปกติ
        canvasGroup.blocksRaycasts = true;
    }
}