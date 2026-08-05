using UnityEngine;

/// <summary>วัตถุที่แค่ต้องการสลับรูปตอนเมาส์ชี้ (ประตู, คันโยก, ป้าย ฯลฯ) — ไม่มีระบบเก็บของ</summary>
[RequireComponent(typeof(HoverVisual))]
public class InteractiveObject : MonoBehaviour
{
    HoverVisual hoverVisual;

    void Awake() => hoverVisual = GetComponent<HoverVisual>();

    void OnMouseEnter() => hoverVisual.SetHovering(true);
    void OnMouseExit() => hoverVisual.SetHovering(false);
}