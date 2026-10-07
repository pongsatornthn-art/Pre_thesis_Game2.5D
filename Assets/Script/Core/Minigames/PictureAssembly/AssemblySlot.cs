using UnityEngine;

/// <summary>
/// ช่องบนกระดานประกอบรูป — วางในซีนตรงตำแหน่ง/มุมที่ชิ้นต้องไปอยู่ (หมุนช่องให้ตรงกับรูปที่ถูกต้อง)
///
/// การคลิกใช้ระบบของปอ: แปะ MiniGameInteractable คู่กัน + Collider
///   → ช่อง On Interact ลาก object นี้ → เลือก AssemblySlot.OnClicked
/// </summary>
public class AssemblySlot : MonoBehaviour
{
    [Tooltip("มินิเกมที่ช่องนี้อยู่")]
    [SerializeField] private PictureAssemblyMinigame minigame;

    [Tooltip("จุดที่ชิ้นจะไปวาง (เว้นว่าง = ตำแหน่งช่องนี้)")]
    [SerializeField] private Transform snapPoint;

    public Transform SnapPoint => snapPoint != null ? snapPoint : transform;
    public AssemblyPiece Occupant { get; internal set; }

    /// <summary>ผูกกับ MiniGameInteractable.onInteract ของปอ (คลิกซ้ายตอนเล็งช่องนี้)</summary>
    public void OnClicked()
    {
        if (minigame != null) minigame.OnSlotClicked(this);
    }
}
