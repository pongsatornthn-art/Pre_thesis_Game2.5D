using UnityEngine;

public class SeeThroughWall : MonoBehaviour
{
    [Header("Settings")]
    public Transform player;         // ลาก Player มาใส่ช่องนี้
    public LayerMask wallLayer;      // เลือก Layer "Wall"
    [Range(0f, 1f)]
    public float transparentAlpha = 0.3f; // ความโปร่งใส (0 = ล่องหน, 1 = ทึบ)

    private Transform currentWall;
    private Color originalColor;
    private Material wallMaterial;

    void Update()
    {
        if (player == null) return;

        // คำนวณทิศทางและระยะทางจาก กล้อง ไปหา ผู้เล่น
        Vector3 direction = player.position - transform.position;
        float distance = direction.magnitude;

        // ยิง Raycast จากกล้องไปหาผู้เล่น โดยเช็กเฉพาะ Layer ที่ตั้งไว้
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, wallLayer))
        {
            // ถ้าชนกำแพง และไม่ใช่กำแพงเดิมที่กำลังโปร่งใสอยู่
            if (currentWall != hit.transform)
            {
                ResetWall(); // คืนค่ากำแพงเก่า (ถ้ามี) กลับมาทึบก่อน
                currentWall = hit.transform;

                // ดึง Material ของกำแพงที่โดนชนมาปรับความโปร่งใส
                wallMaterial = currentWall.GetComponent<MeshRenderer>().material;
                originalColor = wallMaterial.color;

                Color newColor = originalColor;
                newColor.a = transparentAlpha; // ลดค่า Alpha
                wallMaterial.color = newColor;
            }
        }
        else
        {
            // ถ้าเลเซอร์ไม่โดนกำแพงแล้ว (ผู้เล่นเดินออกมาแล้ว) ให้คืนค่ากำแพงเดิม
            ResetWall();
        }
    }

    // ฟังก์ชันสำหรับคืนค่าความทึบให้กำแพง
    private void ResetWall()
    {
        if (currentWall != null && wallMaterial != null)
        {
            wallMaterial.color = originalColor;
            currentWall = null;
            wallMaterial = null;
        }
    }
}