using UnityEngine;

/// <summary>
/// ⭐ รวมโค้ด "สลับรูปตอนเมาส์ชี้" ที่เดิมซ้ำกันอยู่ 2 ที่ (InteractiveObject เดิม และในตัว ItemPickup เดิม)
/// ใช้ได้ทั้งกับของที่เก็บได้ (ItemPickup) และวัตถุที่แค่กดโต้ตอบเฉยๆ (ประตู, คันโยก ฯลฯ)
/// </summary>
public class HoverVisual : MonoBehaviour
{
    [Header("รูปภาพ (ลากตัวลูกมาใส่)")]
    public GameObject normalObject;
    public GameObject hoverObject;
    public GameObject promptUI;

    public bool IsHovering { get; private set; }

    void Start() => SetHovering(false);

    public void SetHovering(bool hovering)
    {
        IsHovering = hovering;
        if (normalObject != null) normalObject.SetActive(!hovering);
        if (hoverObject != null) hoverObject.SetActive(hovering);
        if (promptUI != null) promptUI.SetActive(hovering);
    }
}