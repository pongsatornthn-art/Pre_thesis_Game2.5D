using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    [Header("Billboard Settings")]
    [Tooltip("1 = หันตั้งฉากรับมุมกล้องเป๊ะๆ (100%), 0 = ยืนตรงตั้งฉากกับพื้นเหมือนเดิม")]
    [Range(0f, 1f)]
    public float pitchTiltAmount = 1f;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // 1. ทิศทางที่หันขนานกับหน้าเลนส์กล้องเป๊ะๆ (รวมมุมก้ม-เงย)
        Vector3 fullBillboardForward = cam.transform.forward;

        // 2. ทิศทางที่ล็อกแกน Y (ยืนตั้งฉากกับพื้น)
        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        // 3. ผสมองศาการเอียงตามค่า pitchTiltAmount ที่ตั้งไว้
        Vector3 finalForward = Vector3.Slerp(flatForward, fullBillboardForward, pitchTiltAmount);

        // สั่งหมุนแผ่นภาพให้หันรับมุมกล้อง
        transform.rotation = Quaternion.LookRotation(finalForward, cam.transform.up);
    }
}