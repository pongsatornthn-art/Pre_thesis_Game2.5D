using UnityEngine;

/// <summary>
/// Scene Data Provider: พื้นที่สำหรับให้มอนสเตอร์เดินสุ่ม (Roam)
/// รองรับทั้งแบบวงกลม และแบบกล่องสี่เหลี่ยม (ปรับขนาด X, Y, Z ได้อิสระ)
/// </summary>
public class PatrolZone : MonoBehaviour
{
    public enum ZoneShape { Sphere, Box }
    
    [Header("Zone Shape Settings")]
    [Tooltip("รูปร่างของพื้นที่ (วงกลม หรือ กล่องสี่เหลี่ยม)")]
    public ZoneShape shape = ZoneShape.Sphere;

    [Tooltip("รัศมีการเดินสุ่ม (ใช้กับรูปทรง Sphere)")]
    public float roamRadius = 10f;
    
    [Tooltip("ขนาดของพื้นที่ (ใช้กับรูปทรง Box)")]
    public Vector3 boxSize = new Vector3(10f, 2f, 10f);

    [Header("Patrol Settings")]
    [Tooltip("เวลาที่จะยืนรอในแต่ละจุด (วินาที)")]
    public float waitTimeAtPoint = 2f;

    private void OnDrawGizmos()
    {
        // คำนวณแกนหมุนให้วาดกล่องเอียงตาม Object ได้
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        if (shape == ZoneShape.Sphere)
        {
            // วาดวงกลม
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(Vector3.zero, roamRadius);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Vector3.zero, roamRadius);
        }
        else
        {
            // วาดกล่องสี่เหลี่ยม
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawCube(Vector3.zero, boxSize);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
        }
    }

    /// <summary>
    /// สุ่มหาจุดเป้าหมายภายในโซนนี้ เพื่อส่งให้มอนสเตอร์เดินไปหา
    /// </summary>
    public Vector3 GetRandomPoint()
    {
        if (shape == ZoneShape.Sphere)
        {
            // สุ่มในวงกลม
            return transform.position + Random.insideUnitSphere * roamRadius;
        }
        else
        {
            // สุ่มในกล่องสี่เหลี่ยม (อ้างอิงจากแกน X Y Z ของตัวมันเอง)
            Vector3 extents = boxSize / 2f;
            Vector3 localRandom = new Vector3(
                Random.Range(-extents.x, extents.x),
                Random.Range(-extents.y, extents.y),
                Random.Range(-extents.z, extents.z)
            );
            
            // แปลงพิกัดจาก Local เป็น World Space (รองรับกรณีที่กล่องหมุนเอียง)
            return transform.TransformPoint(localRandom); 
        }
    }
}
