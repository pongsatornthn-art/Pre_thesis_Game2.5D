using UnityEngine;

public class FPSCameraController : MonoBehaviour
{
    [Header("Camera Limits")]
    public float mouseSensitivity = 150f;
    public float clampAnglePitch = 30f; // ก้ม-เงย (ขึ้นลง) +- 30 องศา
    public float clampAngleYaw = 60f;   // หันซ้าย-ขวา +- 60 องศา
    public float interactDistance = 10f; // ระยะยิงเลเซอร์ตรวจจับวัตถุ

    private float rotX = 0f;
    private float rotY = 0f;
    private Vector3 startRotation;

    private MiniGameInteractable currentTarget;

    void Start()
    {
        // บันทึกมุมเริ่มต้นของกล้อง FPS ที่ตั้งไว้ใน Scene
        startRotation = transform.localEulerAngles;
        rotX = startRotation.x;
        rotY = startRotation.y;
    }

    void Update()
    {
        HandleCameraRotation();
        HandleInteraction();
    }

    private void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        rotY += mouseX;
        rotX -= mouseY;

        // ล็อกองศากล้องไม่ให้หันเกินกำหนด
        rotX = Mathf.Clamp(rotX, startRotation.x - clampAnglePitch, startRotation.x + clampAnglePitch);
        rotY = Mathf.Clamp(rotY, startRotation.y - clampAngleYaw, startRotation.y + clampAngleYaw);

        transform.localRotation = Quaternion.Euler(rotX, rotY, 0f);
    }

    private void HandleInteraction()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // ยิง Raycast ออกไปตรงกลางจอ (เป้าเล็ง)
        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            MiniGameInteractable interactable = hit.collider.GetComponent<MiniGameInteractable>();
            if (interactable != null)
            {
                // ถ้าชี้โดนเป้าหมายใหม่ ให้เปิดแสง Glow
                if (currentTarget != interactable)
                {
                    if (currentTarget != null) currentTarget.OnHoverExit();
                    currentTarget = interactable;
                    currentTarget.OnHoverEnter();
                }

                // ถ้ากดคลิกซ้าย
                if (Input.GetMouseButtonDown(0))
                {
                    currentTarget.OnClick();
                }
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnHoverExit();
            currentTarget = null;
        }
    }
}