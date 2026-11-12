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

    // [Claude 2026-11-11 งานข้อ 8] โหมดเล็งกลางจอแบบเกม FPS — เปิด/ปิดได้ ไม่ทับโหมดเดิมของปอ
    [Header("เล็งกลางจอ (แบบเกม FPS)")]
    [Tooltip("เปิด = ล็อกเมาส์กลางจอ ขยับเมาส์หันกล้อง เล็งด้วยจุดกลางจอ คลิกซ้ายใช้ของ\nปิด =แบบเดิม (เมาส์ลอย ชี้ของด้วยเมาส์)")]
    public bool centerAim = false;
    [Tooltip("จุดเล็งกลางจอ (ซ่อนเองตอนมีหน้าต่างเปิด)")]
    public GameObject crosshairDot;
    [Tooltip("หน้าต่างที่เปิดแล้วต้องปล่อยเมาส์ให้กด เช่น ปุ่มเทปวิทยุ — เปิดอยู่ = หยุดหันกล้อง/หยุดเล็ง")]
    public GameObject[] blockingPanels;

    /// <summary>สั่งหยุดหันกล้อง/เล็งชั่วคราว (เช่น ตอนเล่นอนิเมชั่นสมุดเซฟ) — เมาส์ถูกปล่อย</summary>
    public bool InputBlocked { get; set; }

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
        if (crosshairDot != null) crosshairDot.SetActive(false);
    }

    void Update()
    {
        if (centerAim)
        {
            bool blocked = IsBlocked();
            ApplyCenterAimCursor(blocked);
            if (blocked)
            {
                ClearTarget();
                return;
            }
        }

        HandleCameraRotation();
        HandleInteraction();
    }

    private bool IsBlocked()
    {
        if (InputBlocked || SaveSlotPickerUI.IsOpen || JournalController.IsAnyOpen) return true;
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return true;
        if (blockingPanels != null)
            foreach (GameObject p in blockingPanels)
                if (p != null && p.activeInHierarchy) return true;
        return false;
    }

    private void ApplyCenterAimCursor(bool blocked)
    {
        if (crosshairDot != null && crosshairDot.activeSelf == blocked) crosshairDot.SetActive(!blocked);

        // ระบบอื่นที่เปิดหน้าต่าง (สมุด/หน้าเซฟ/เมนูหยุด) จัดการเมาส์เอง — เราแค่คืนล็อกตอนกลับมาเล็ง
        if (blocked)
        {
            if (Cursor.lockState == CursorLockMode.Locked && !SaveSlotPickerUI.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
        if (Cursor.visible) Cursor.visible = false;
    }

    private void HandleCameraRotation()
    {
        if (!centerAim && holdRightClickToLook && !Input.GetMouseButton(1)) return;

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

        // ป้องกันคลิกทะลุ UI (โหมดเล็งกลางจอเมาส์ถูกล็อก ไม่ต้องเช็ค)
        if (!centerAim && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearTarget();
            return;
        }

        Ray ray = centerAim ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : cam.ScreenPointToRay(Input.mousePosition);

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