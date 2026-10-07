using UnityEngine;

/// <summary>
/// ชิ้นส่วนรูปบนโต๊ะ (โลก 3D) — วางในซีนตรงถาด/กองชิ้นส่วน
///
/// การคลิกใช้ระบบของปอ: แปะ MiniGameInteractable คู่กัน + Collider (เรืองแสงตอนเล็ง = glowEffect ของปอ)
///   → ช่อง On Interact ลาก object นี้ → เลือก AssemblyPiece.OnClicked
///
/// "ถูก" = วางในช่อง Correct Slot และหมุนกลับมาตรงทิศของช่อง (หมุนทีละ 90° ด้วย R)
/// ชิ้นที่ผู้เล่นยังไม่ได้เก็บ (Fragment ยังไม่ปลดในคลังความทรงจำ) จะถูกซ่อนตอนเริ่มมินิเกม
/// </summary>
public class AssemblyPiece : MonoBehaviour
{
    [SerializeField] private PictureAssemblyMinigame minigame;

    [Tooltip("ไอเทมเศษรูปที่ต้องเก็บมาก่อนชิ้นนี้ถึงจะโผล่ (เว้นว่าง = โผล่เสมอ)")]
    [SerializeField] private ItemData fragment;

    [Tooltip("ช่องที่ถูกต้องของชิ้นนี้")]
    [SerializeField] private AssemblySlot correctSlot;

    private Vector3 trayPosition;
    private Quaternion trayRotation;
    private Collider[] colliders;

    public ItemData Fragment => fragment;
    public AssemblySlot CorrectSlot => correctSlot;
    public bool IsPlaced { get; private set; }

    /// <summary>หมุนไปกี่ครั้ง (ครั้งละ 90°) — 0 = ตรงทิศของช่อง</summary>
    public int RotationSteps { get; private set; }

    private void Awake()
    {
        trayPosition = transform.position;
        trayRotation = transform.rotation;
        colliders = GetComponentsInChildren<Collider>(true);
    }

    /// <summary>ผูกกับ MiniGameInteractable.onInteract ของปอ (คลิกซ้ายตอนเล็งชิ้นนี้)</summary>
    public void OnClicked()
    {
        if (minigame != null) minigame.OnPieceClicked(this);
    }

    internal void ResetForNewRound(int startRotationSteps, Vector3 boardNormal)
    {
        IsPlaced = false;
        RotationSteps = ((startRotationSteps % 4) + 4) % 4;
        transform.SetPositionAndRotation(trayPosition, Quaternion.AngleAxis(RotationSteps * 90f, boardNormal) * trayRotation);
        SetClickable(true);
    }

    internal void RotateOnce(Vector3 boardNormal)
    {
        RotationSteps = (RotationSteps + 1) % 4;
        transform.rotation = Quaternion.AngleAxis(90f, boardNormal) * transform.rotation;
    }

    internal void ReturnToTray(Vector3 boardNormal)
    {
        transform.SetPositionAndRotation(trayPosition, Quaternion.AngleAxis(RotationSteps * 90f, boardNormal) * trayRotation);
        SetClickable(true);
    }

    internal void SnapInto(AssemblySlot slot)
    {
        IsPlaced = true;
        RotationSteps = 0;
        transform.SetPositionAndRotation(slot.SnapPoint.position, slot.SnapPoint.rotation);
        SetClickable(false);   // วางแล้วล็อก คลิกไม่ได้อีก
    }

    /// <summary>ตอนถือ ปิดตัวชน ไม่ให้บังเรย์ของกล้อง FPS (จะได้เล็งโดนช่องข้างใต้)</summary>
    internal void SetClickable(bool clickable)
    {
        foreach (Collider c in colliders) if (c != null) c.enabled = clickable;
    }
}
