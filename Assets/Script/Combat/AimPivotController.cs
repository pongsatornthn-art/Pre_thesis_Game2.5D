using UnityEngine;

public class AimPivotController : MonoBehaviour
{
    public PlayerCombat playerCombat; // ลาก Player มาใส่ช่องนี้
    public float sensitivity = 200f;  // ความไวเมาส์

    private float xRot = 0f;
    private float yRot = 0f;
    private bool wasAiming = false;

    void Update()
    {
        if (playerCombat != null && playerCombat.isAiming)
        {
            // จังหวะแรกที่กดคลิกขวา ให้ดึงมุมกล้องปัจจบันมาใช้ กล้องจะได้ไม่กระชาก
            if (!wasAiming)
            {
                Cursor.lockState = CursorLockMode.Locked; // ล็อคเมาส์ให้อยู่กลางจอและซ่อนเมาส์
                Vector3 camEuler = Camera.main.transform.eulerAngles;
                yRot = camEuler.y;
                xRot = camEuler.x;
                if (xRot > 180f) xRot -= 360f;
                wasAiming = true;
            }

            // อ่านค่าการขยับเมาส์ (ซ้าย-ขวา, ขึ้น-ลง)
            float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            yRot += mouseX;
            xRot -= mouseY;

            // จำกัดการก้ม/เงย ไม่ให้กล้องตีลังกา (ปรับได้ตามชอบ)
            xRot = Mathf.Clamp(xRot, -40f, 60f);

            // หมุนจุด Pivot
            transform.rotation = Quaternion.Euler(xRot, yRot, 0f);
        }
        else
        {
            if (wasAiming)
            {
                // ปล่อยเมาส์ให้เป็นอิสระเมื่อเลิกเล็ง
                Cursor.lockState = CursorLockMode.Confined;
                wasAiming = false;
            }
        }
    }
}