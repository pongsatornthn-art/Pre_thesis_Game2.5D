using UnityEngine;

/// <summary>
/// Scene Data Provider: พื้นที่สำหรับให้มอนสเตอร์เดินสุ่ม (Roam)
/// </summary>
public class PatrolZone : MonoBehaviour
{
    [Tooltip("รัศมีการเดินสุ่ม (ยิ่งกว้างยิ่งเดินไกล)")]
    public float roamRadius = 10f;

    [Tooltip("เวลาที่จะยืนรอในแต่ละจุด (วินาที)")]
    public float waitTimeAtPoint = 2f;

    private void OnDrawGizmos()
    {
        // วาดขอบเขตวงกลมสีเขียวอ่อน ให้ Level Designer กะระยะความกว้างของห้องได้
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, roamRadius);
        
        // วาดขอบเส้นสีเขียวเข้มให้ชัดขึ้น
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, roamRadius);
    }
}
