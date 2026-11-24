using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ประตูกันผีเฉพาะตอน "ล็อก" — ปลดล็อกแล้ว (กุญแจ/ยิงแม่กุญแจ) ผีเดินผ่านได้ทันที
/// แปะคู่กับ NavMeshObstacle (Carve) บนประตู · พื้นที่เดินได้อบโดยไม่นับบานประตู (NavMeshModifier ไม่นับ)
/// ประตูปิดแต่ไม่ล็อก = ผีเดินผ่านได้ (เจ้าของเลือก 2026-11-11 ไม่งั้นผีไล่ผู้เล่นเข้าห้องไม่ได้)
/// [Claude 2026-11-11]
/// </summary>
[RequireComponent(typeof(NavMeshObstacle))]
public class DoorNavBlocker : MonoBehaviour
{
    [Tooltip("ประตูที่จะดูสถานะล็อก (เว้นว่าง = หาในตัวเอง/ตัวแม่)")]
    public DoorController door;

    private NavMeshObstacle obstacle;

    private void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        if (door == null) door = GetComponentInParent<DoorController>();
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        bool block = door != null && !door.isUnlocked;
        if (obstacle.enabled != block) obstacle.enabled = block;
    }

    /// <summary>
    /// [2026-11-24 นา: AI เดินติดประตู] ทางเดินของมอนผ่านประตูที่ไม่ล็อกได้ แต่ตัวมอนมี Rigidbody ชนบานประตูจริง → เดินชนบานค้าง
    /// เรียกจาก OnCollisionEnter/Stay ของมอน: ชนบานประตูที่ปิดแต่ไม่ล็อก = ผลักเปิด (บานเปิดจริง กล่องชนปิดเอง แล้วเดินผ่าน)
    /// (รอบแรกให้ทะลุบานไปเลย → นาเจอว่ามอนเดินทะลุประตูที่ปิดอยู่ ดูผิด) · ประตูล็อก = ชนตามปกติ (ทางเดินก็ถูกตัดอยู่แล้ว)
    /// </summary>
    public static void PassThroughIfUnlocked(Collision hit, Collider self)
    {
        if (hit == null || hit.collider == null) return;
        DoorController d = hit.collider.GetComponentInParent<DoorController>();
        if (d != null) d.OpenByMonster();
    }
}
