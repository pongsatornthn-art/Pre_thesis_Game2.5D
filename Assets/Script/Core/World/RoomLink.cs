using UnityEngine;

/// <summary>
/// จุดเชื่อมระหว่าง 2 ห้อง (ประตู / ช่องโล่ง) สำหรับระบบเงาดำห้อง — "room &amp; portal"
///   ช่อง Door ว่าง        = ช่องโล่ง เปิดตลอด (เห็นห้องอีกฝั่งเสมอ)
///   ช่อง Door มีตัวชน     = เปิดเมื่อตัวชนนั้นปิดอยู่ (ประตูของปอปิดตัวชนทึบตอนประตูเปิด)
///   ติ๊ก Always Closed    = ปิดตลอด (เช่นประตูหลอก)
/// วางไว้ใต้ Map_Test/Rooms/_Links · ตำแหน่ง object = ตรงช่องประตู (ไว้ดูใน Scene view เท่านั้น)
/// สร้างอัตโนมัติด้วยเมนู Tools › ห้อง (เงาดำ) › หาจุดเชื่อมห้อง แล้วตรวจ/แก้ทีละอันได้
/// </summary>
public class RoomLink : MonoBehaviour
{
    public RoomArea roomA;
    public RoomArea roomB;

    [Tooltip("ตัวชนของประตู — ตัวชนเปิดอยู่ = ประตูปิด (มองไม่ทะลุ) · ว่าง = ช่องโล่ง")]
    public Collider door;

    [Tooltip("ปิดตลอด ไม่สนประตู")]
    public bool alwaysClosed;

    /// <summary>ตอนนี้มองทะลุได้ไหม</summary>
    public bool IsOpen
    {
        get
        {
            if (alwaysClosed) return false;
            if (door == null) return true;
            return !door.enabled || !door.gameObject.activeInHierarchy;
        }
    }

    /// <summary>ห้องอีกฝั่ง (null = จุดนี้ไม่ได้เชื่อมกับห้อง r)</summary>
    public RoomArea Other(RoomArea r) => r == roomA ? roomB : r == roomB ? roomA : null;

    private void OnDrawGizmos()
    {
        if (roomA == null || roomB == null) return;
        bool open = Application.isPlaying ? IsOpen : !alwaysClosed;
        Gizmos.color = alwaysClosed ? new Color(1f, 0.2f, 0.2f) : door == null ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.8f, 0.2f);
        Vector3 p = transform.position + Vector3.up * 0.3f;
        Gizmos.DrawSphere(p, 0.25f);
        Gizmos.DrawLine(p, Center(roomA));
        Gizmos.DrawLine(p, Center(roomB));
        if (!open) Gizmos.DrawWireCube(p, Vector3.one * 0.7f);
    }

    private static Vector3 Center(RoomArea r)
    {
        Bounds b = r.TotalBounds;
        return new Vector3(b.center.x, 0.3f, b.center.z);
    }
}
