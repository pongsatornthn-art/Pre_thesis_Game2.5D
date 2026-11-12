using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [Claude 2026-11-11 งานข้อ 1] แปลงตำแหน่งผู้เล่นเป็น "จุดบนพื้นเดินได้ที่ใกล้ที่สุด" ก่อนสั่งมอน/บอสเดินไป
/// ผู้เล่นยืนชิดโต๊ะ/ผนัง/ลัง (ไม่ใช่พื้นเดินได้) → ถ้าส่งตำแหน่งตรงๆ ทางจะค้างคำนวณ/ได้ทางครึ่งๆ แล้วมอนยืนนิ่ง
/// </summary>
public static class ChaseTarget
{
    public static Vector3 OnNavMesh(Vector3 position, float searchRadius = 2.5f)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas) ? hit.position : position;
    }
}
