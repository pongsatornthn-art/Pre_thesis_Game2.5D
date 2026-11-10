using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CanvasRaycastClicker : MonoBehaviour
{
    [Header("Settings")]
    public Camera uiCamera; // ลาก FPS_Camera มาใส่ที่นี่
    public float raycastDistance = 100f;

    void Update()
    {
        // ถ้าผู้เล่นคลิกเมาส์ซ้าย
        if (Input.GetMouseButtonDown(0))
        {
            // เช็กว่าเมาส์ไม่ได้คลิกทับ UI ตัวอื่นอยู่
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (uiCamera == null) return;

            // ยิง Ray จากตำแหน่งเมาส์ผ่านกล้องเข้าไปในโลก 3D ของ World Space UI
            Ray ray = uiCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, raycastDistance))
            {
                // ตรวจสอบว่าคลิกโดนปุ่มบน Canvas หรือไม่
                Button btn = hit.collider.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.Invoke(); // จำลองการกดปุ่มทันที
                    Debug.Log($"[CanvasClicker] คลิกโดนปุ่ม: {btn.name}");
                }
            }
        }
    }
}