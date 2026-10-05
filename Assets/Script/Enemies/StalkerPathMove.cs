using UnityEngine;
using System.Collections; // 🌟 ต้องมีเพื่อใช้ Coroutine

public class StalkerPathMove : MonoBehaviour
{
    [Header("เส้นทางการวิ่ง (ลากจุด A, B, C มาใส่)")]
    public Transform[] waypoints;
    public float moveSpeed = 4.0f;

    [Header("การหันหน้า")]
    public bool lookAtTarget = true;

    [Header("การรีเซ็ต (เมื่อจับผู้เล่นได้)")]
    [Tooltip("ลากกล่อง Trigger_Cinematic_PTSD มาใส่ที่นี่ เพื่อให้ผีสั่งเปิดกล่องใหม่ได้")]
    public AutoTriggerTimeline startTrigger;

    private int currentTargetIndex = 0;
    private Vector3 startPosition; // จำจุดเกิดของผี

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        if (PTSDManager.Instance == null || PTSDManager.Instance.currentMode != PTSDMode.Survival_TypeA) return;
        if (waypoints == null || waypoints.Length == 0 || currentTargetIndex >= waypoints.Length) return;

        Transform targetPoint = waypoints[currentTargetIndex];
        Vector3 targetPosition = new Vector3(targetPoint.position.x, transform.position.y, targetPoint.position.z);

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        if (lookAtTarget) transform.LookAt(targetPosition);

        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(targetPoint.position.x, 0, targetPoint.position.z)
        );

        if (distance < 0.1f)
        {
            currentTargetIndex++;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // เมื่อผีชนผู้เล่น และอยู่ในโหมด PTSD
        if (other.CompareTag("Player") && PTSDManager.Instance.currentMode == PTSDMode.Survival_TypeA)
        {
            Debug.LogWarning("ผู้เล่นโดนจับ! ส่งต่อให้ระบบวาร์ปจัดการ...");

            // 1. ส่งคำสั่งให้เจ้านายใหญ่ (PTSDManager) จัดการวาร์ปและปิดโหมด
            if (startTrigger != null && startTrigger.respawnPoint != null)
            {
                PTSDManager.Instance.ExitPTSD_AndWarp(startTrigger.respawnPoint);
            }
            else
            {
                // ถ้าลืมใส่จุดเกิด ให้กลับจุดเดิม
                PTSDManager.Instance.ExitPTSD();
            }

            // 2. รีเซ็ตตัวผีเอง
            transform.position = startPosition;
            currentTargetIndex = 0;
            gameObject.SetActive(false);

            // 3. รีเซ็ตคัตซีน Timeline เพื่อรอให้ผู้เล่นมาเหยียบใหม่
            if (startTrigger != null)
            {
                startTrigger.ResetTrigger();
            }
        }
    }
}