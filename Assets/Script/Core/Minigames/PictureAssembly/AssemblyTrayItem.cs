using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ช่องเศษรูป 1 ช่องในแถบ UI ด้านขวา — กดค้างแล้วลากออกไปวางบนกระดานในโลก 3D
/// วางในซีน (ใน Canvas) ช่องละ 1 ชิ้น แล้วลากชิ้นบนกระดาน (AssemblyPiece) ที่คู่กันใส่ช่อง Piece
/// ซ่อนเองเมื่อ: ยังไม่ได้เก็บเศษชิ้นนั้น · หรือชิ้นนั้นอยู่บนกระดานแล้ว
/// </summary>
[RequireComponent(typeof(Image))]
public class AssemblyTrayItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private PictureAssemblyMinigame minigame;
    [SerializeField] private AssemblyPiece piece;

    private Image image;

    public AssemblyPiece Piece => piece;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (piece != null && image.sprite == null) image.sprite = piece.TrayIcon;
    }

    /// <summary>โชว์/ซ่อนช่องนี้ (มินิเกมเป็นคนเรียก) · inUse = กำลังลากชิ้นนี้อยู่ → จางลงแทนการซ่อน
    /// (ห้ามปิด object ระหว่างลาก ไม่งั้นระบบ UI ไม่ส่ง OnEndDrag มา → ชิ้นติดเมาส์ค้าง)</summary>
    internal void SetAvailable(bool available, bool inUse = false)
    {
        gameObject.SetActive(available || inUse);
        if (image == null) image = GetComponent<Image>();
        Color c = image.color;
        c.a = inUse ? 0.35f : 1f;
        image.color = c;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (minigame != null && piece != null) minigame.BeginDragFromTray(piece);
    }

    public void OnDrag(PointerEventData e) { }   // ต้องมีเพื่อให้ระบบ UI ส่ง OnEndDrag มา · ตำแหน่งชิ้นมินิเกมคุมเอง

    public void OnEndDrag(PointerEventData e)
    {
        if (minigame != null) minigame.EndDrag();
    }
}
