using UnityEngine;

/// <summary>
/// Scene Data Provider: เส้นทางเดินสำหรับมอนสเตอร์ (Waypoints)
/// </summary>
public class PatrolRoute : MonoBehaviour
{
    [Tooltip("ลากจุด (Transform) ในฉากมาใส่เรียงตามลำดับที่อยากให้เดิน")]
    public Transform[] waypoints;

    [Tooltip("เวลาที่จะยืนรอในแต่ละจุด (วินาที)")]
    public float waitTimeAtPoint = 2f;

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                // วาดทรงกลมเล็กๆ ตรงจุด
                Gizmos.DrawSphere(waypoints[i].position, 0.3f);

                // วาดเส้นเชื่อมไปยังจุดถัดไป (วนลูปกลับจุดแรก)
                Transform nextWaypoint = waypoints[(i + 1) % waypoints.Length];
                if (nextWaypoint != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, nextWaypoint.position);
                }
            }
        }
    }
}
