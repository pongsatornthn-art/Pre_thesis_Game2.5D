using UnityEngine;
using UnityEngine.EventSystems;

public class FPSCameraController : MonoBehaviour
{
    [Header("Camera Limits")]
    public float mouseSensitivity = 150f;
    public float clampAnglePitch = 30f;
    public float clampAngleYaw = 60f;
    public float interactDistance = 10f;

    [Header("Controls")]
    [Tooltip("ต้องคลิกขวาค้างถึงจะหันกล้องได้ (กล้องไม่ส่ายตอนเอาเมาส์ไปกด UI)")]
    public bool holdRightClickToLook = true;

    private float rotX = 0f;
    private float rotY = 0f;
    private Vector3 startRotation;
    private Quaternion initialLocalRot;

    private MiniGameInteractable currentTarget;
    private Camera cam; // 🔥 เปลี่ยนมาดึงกล้องของตัวเองแทน
    private bool initialized;

    void Start()
    {
        cam = GetComponent<Camera>(); // ดึงจากออบเจกต์ที่สคริปต์นี้แปะอยู่ (FPS Camera)

        initialLocalRot = transform.localRotation;

        Vector3 angles = transform.localEulerAngles;
        rotX = angles.x > 180 ? angles.x - 360 : angles.x;
        rotY = angles.y > 180 ? angles.y - 360 : angles.y;
        startRotation = new Vector3(rotX, rotY, 0f);

        initialized = true;
    }

    void OnEnable()
    {
        cam = GetComponent<Camera>();

        if (!initialized) return;
        rotX = startRotation.x;
        rotY = startRotation.y;
        transform.localRotation = initialLocalRot;
    }

    void OnDisable()
    {
        ClearTarget();
    }

    void Update()
    {
        HandleCameraRotation();
        HandleInteraction();
    }

    private void HandleCameraRotation()
    {
        if (holdRightClickToLook && !Input.GetMouseButton(1)) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        rotY += mouseX;
        rotX -= mouseY;

        rotX = Mathf.Clamp(rotX, startRotation.x - clampAnglePitch, startRotation.x + clampAnglePitch);
        rotY = Mathf.Clamp(rotY, startRotation.y - clampAngleYaw, startRotation.y + clampAngleYaw);

        transform.localRotation = Quaternion.Euler(rotX, rotY, 0f);
    }

    private void HandleInteraction()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        // ป้องกันคลิกทะลุ UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearTarget();
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // ไม่ชน Trigger (กันโต๊ะที่เป็น Trigger บังวิทยุ)
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            MiniGameInteractable interactable = hit.collider.GetComponentInParent<MiniGameInteractable>();
            if (interactable != null)
            {
                if (currentTarget != interactable)
                {
                    if (currentTarget != null) currentTarget.OnHoverExit();
                    currentTarget = interactable;
                    currentTarget.OnHoverEnter();
                }

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