using UnityEngine;

/// <summary>
/// เศษรูป 1 ชิ้น "บนกระดาน" (โลก 3D) — การ์ดแบน SpriteRenderer + Collider บางๆ วางเป็นลูกของกระดาน
///
/// วางในซีน: ตั้งตำแหน่ง/ทิศของชิ้นนี้ให้ตรงกับ "ตำแหน่งที่ถูก" ในรูป (ระบบจำไว้เป็นเป้าหมายตอนเริ่ม)
///   Pre Placed ✔ = ชิ้นที่ติดบนกระดานอยู่แล้วตั้งแต่แรก (เช่น เศษรูป 1 ชิ้นที่ผู้เล่นเห็นตอนแรก)
///   Pre Placed ✘ = ชิ้นที่ต้องไปเก็บ → ซ่อนจนกว่าผู้เล่นจะลากจากช่อง UI ด้านขวามาวาง
/// เปลี่ยนเป็นอาร์ตจริง = เปลี่ยน Sprite ของ SpriteRenderer + ปรับตำแหน่งชิ้นนี้ใหม่ เท่านั้น
/// </summary>
public class AssemblyPiece : MonoBehaviour
{
    [Tooltip("ติดบนกระดานอยู่แล้วตั้งแต่แรก (ไม่ต้องไปเก็บ · ลากย้ายไม่ได้)")]
    [SerializeField] private bool prePlaced;

    [Tooltip("ไอเทมเศษรูปที่ต้องเก็บมาก่อนถึงจะลากชิ้นนี้ได้ (เว้นว่าง = มีเสมอ)")]
    [SerializeField] private ItemData fragment;

    [Tooltip("รูปที่โชว์ในช่อง UI ด้านขวา (เว้นว่าง = ใช้ Sprite ของชิ้นนี้)")]
    [SerializeField] private Sprite trayIcon;

    private Vector3 targetLocalPosition;
    private Quaternion targetLocalRotation;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool initialized;

    public bool PrePlaced => prePlaced;
    public ItemData Fragment => fragment;
    public Sprite TrayIcon => trayIcon != null ? trayIcon : (TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null);

    /// <summary>อยู่บนกระดานตอนนี้ไหม (ชิ้นที่ยังอยู่ในช่อง UI = false)</summary>
    public bool OnBoard { get; private set; }

    /// <summary>หมุนไปกี่ครั้ง (ครั้งละ 90°) — 0 = ตรงทิศที่ถูก</summary>
    public int RotationSteps { get; private set; }

    public Vector3 TargetLocalPosition => targetLocalPosition;

    private void Awake()
    {
        Init();
        // ก่อนเริ่มมินิเกม ผู้เล่นต้องเห็นแค่ชิ้นที่ติดอยู่แล้ว (ไม่สปอยรูปเต็ม)
        if (!prePlaced) SetOnBoard(false);
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;
        // ตำแหน่งที่วางไว้ในซีน = ตำแหน่งที่ถูก
        targetLocalPosition = transform.localPosition;
        targetLocalRotation = transform.localRotation;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    internal void ResetForRound(int startRotationSteps)
    {
        Init();
        RotationSteps = prePlaced ? 0 : ((startRotationSteps % 4) + 4) % 4;
        transform.localRotation = targetLocalRotation * Quaternion.Euler(0f, 0f, RotationSteps * 90f);

        if (prePlaced) PlaceAtTarget();
        else SetOnBoard(false);
    }

    /// <summary>วางบนกระดานที่ตำแหน่ง (local ของกระดาน) — แบบวางอิสระ ชิ้นอยู่ตรงที่ปล่อย</summary>
    internal void PlaceAt(Vector3 boardLocalPosition)
    {
        transform.localPosition = boardLocalPosition;
        SetOnBoard(true);
    }

    internal void PlaceAtTarget()
    {
        transform.localPosition = targetLocalPosition;
        transform.localRotation = targetLocalRotation;
        RotationSteps = 0;
        SetOnBoard(true);
    }

    internal void RotateOnce()
    {
        RotationSteps = (RotationSteps + 1) % 4;
        transform.localRotation = targetLocalRotation * Quaternion.Euler(0f, 0f, RotationSteps * 90f);
    }

    /// <summary>ห่างจากตำแหน่งที่ถูกเท่าไหร่ (หน่วยเดียวกับกระดาน)</summary>
    internal float DistanceFromTarget() => Vector3.Distance(transform.localPosition, targetLocalPosition);

    internal void SetOnBoard(bool onBoard)
    {
        Init();
        OnBoard = onBoard;
        foreach (Renderer r in renderers) if (r != null) r.enabled = onBoard;
        foreach (Collider c in colliders) if (c != null) c.enabled = onBoard && !prePlaced;   // ชิ้นติดตายตัวคลิกไม่ได้
    }

    /// <summary>ระหว่างลาก: โชว์ภาพแต่ปิดตัวชน (ไม่บังเรย์ตอนเล็งกระดาน)</summary>
    internal void SetDragging(bool dragging)
    {
        Init();
        foreach (Renderer r in renderers) if (r != null) r.enabled = true;
        foreach (Collider c in colliders) if (c != null) c.enabled = !dragging && !prePlaced;
    }
}
