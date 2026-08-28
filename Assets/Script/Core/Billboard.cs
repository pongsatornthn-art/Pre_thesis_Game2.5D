using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam != null)
        {
            // ดึงค่าองศากล้องมา
            Vector3 cameraForward = cam.transform.forward;

            // 🌟 ล็อกแกน Y ให้เป็น 0 เสมอ (เพื่อไม่ให้รูปเงยหน้าหรือก้มหน้า)
            cameraForward.y = 0;

            // สั่งให้รูปหันไปตามทิศทางกล้อง (แต่ล็อกไม่ให้เงยหน้า)
            transform.rotation = Quaternion.LookRotation(cameraForward);
        }
    }
}