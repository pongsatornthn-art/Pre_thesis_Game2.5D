using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RadioMiniGameManager : MonoBehaviour
{
    private enum State { None, AtTable, Transitioning, RadioView }

    [Header("Cameras & HUD")]
    [Tooltip("กล้องหลัก / กลุ่ม Cinemachine (fpsCamera ต้องไม่เป็นลูกของอันนี้)")]
    public GameObject mainCameraGroup;
    [Tooltip("กล้อง FPS ที่เป็นลูกของ Player ได้เลย จะตามผู้เล่นเอง")]
    public GameObject fpsCamera;
    public GameObject mainHUD;

    [Header("UI")]
    public GameObject interactPrompt;
    public GameObject radioMinigameUI;
    public CrosshairUI crosshair;
    public Button backButton;

    [Header("Radio")]
    public MiniGameInteractable radioInteractable;
    [Tooltip("Empty Object ตำแหน่ง/มุมกล้องตอนซูมเข้าวิทยุ (ตำแหน่งในโลก ไม่ต้องเป็นลูกของ Player)")]
    public Transform radioViewPoint;
    public float zoomDuration = 0.6f;
    public RadioTapeSystem tapeSystem;
    public bool stopAudioOnExit = true;

    [Header("Player Positioning")]
    public Transform playerStandPoint;
    public bool returnToOriginalPosition = true;

    [Header("Slide ซ้าย/ขวา (A/D หรือ ลูกศร)")]
    public float slideSpeed = 1.5f;
    public float slideLimit = 1.2f;

    [Header("Controllers (ปิดตอนเล่นมินิเกม)")]
    public MonoBehaviour playerController;
    public MonoBehaviour playerInteraction;
    public MonoBehaviour playerCombat;
    public MonoBehaviour cameraAimScript;

    [Header("Story Integration")]
    public StoryFlagId onCompleteFlag;

    [Header("Raycast")]
    public float raycastDistance = 10f;

    // 🔥 จุดที่แก้ที่ 1: เพิ่มตัวแปรปุ่มออก และสถานะ IsActive ให้ระบบอื่น (เช่น Pause Menu) เช็กได้
    [Header("Exit")]
    public KeyCode exitKey = KeyCode.Q;
    public static bool IsActive { get; private set; }

    private State state = State.None;
    private bool isPlayerNear;
    private bool hasPlayed;

    private Camera fpsCam;
    private MiniGameInteractable currentHover;

    private Vector3 origLocalPos;
    private Quaternion origLocalRot;
    private Vector3 camRelPos;
    private Quaternion camRelRot;
    private Vector3 slideAxis;
    private float slideOffset;
    private Vector3 playerStandPos;
    private Vector3 playerReturnPos;
    private Quaternion playerReturnRot;

    private Transform PlayerRoot => playerController != null ? playerController.transform : null;

    private void Start()
    {
        if (fpsCamera != null)
        {
            origLocalPos = fpsCamera.transform.localPosition;
            origLocalRot = fpsCamera.transform.localRotation;

            Transform root = PlayerRoot != null ? PlayerRoot : fpsCamera.transform.parent;
            if (root != null)
            {
                camRelPos = root.InverseTransformPoint(fpsCamera.transform.position);
                camRelRot = Quaternion.Inverse(root.rotation) * fpsCamera.transform.rotation;
            }

            fpsCam = fpsCamera.GetComponentInChildren<Camera>(true);
            if (fpsCam != null && fpsCamera.GetComponentInChildren<AudioListener>(true) == null)
                fpsCam.gameObject.AddComponent<AudioListener>();

            fpsCamera.SetActive(false);
        }
        else
        {
            Debug.LogError("[Radio] ยังไม่ได้ใส่ fpsCamera");
        }

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);
        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (backButton != null) backButton.onClick.AddListener(BackToTable);
    }

    private void Update()
    {
        if (state == State.None)
        {
            if (isPlayerNear && !hasPlayed && Input.GetKeyDown(KeyCode.E))
                EnterMiniGame();
            return;
        }

        if (state == State.AtTable)
        {
            HandleSlide();
            HandleMouse();
            // 🔥 เปลี่ยนมาใช้ปุ่ม exitKey (Q) แทน Escape
            if (Input.GetKeyDown(exitKey)) ExitMiniGame();
        }
        else if (state == State.RadioView)
        {
            // 🔥 เปลี่ยนมาใช้ปุ่ม exitKey (Q) แทน Escape
            if (Input.GetKeyDown(exitKey)) BackToTable();
        }
    }

    public void EnterMiniGame()
    {
        if (state != State.None || hasPlayed) return;

        state = State.AtTable;
        IsActive = true; // 🔥 เซ็ตสถานะว่าอยู่ในมินิเกมแล้ว

        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (crosshair != null) crosshair.SetVisible(false);
        if (mainHUD != null) mainHUD.SetActive(false);

        if (playerController != null)
        {
            Transform p = playerController.transform;
            playerReturnPos = p.position;
            playerReturnRot = p.rotation;

            playerController.enabled = false;
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (playerStandPoint != null)
                WarpPlayer(playerStandPoint.position, playerStandPoint.rotation);
            else
                WarpPlayer(p.position, p.rotation);

            playerStandPos = p.position;

            Vector3 right = p.right;
            right.y = 0f;
            slideAxis = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        if (playerInteraction != null) playerInteraction.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;
        if (cameraAimScript != null) cameraAimScript.enabled = false;

        slideOffset = 0f;

        if (mainCameraGroup != null) mainCameraGroup.SetActive(false);
        if (fpsCamera != null)
        {
            fpsCamera.SetActive(true);
            GetTablePose(out Vector3 pos, out Quaternion rot);
            fpsCamera.transform.SetPositionAndRotation(pos, rot);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitMiniGame()
    {
        StopAllCoroutines();

        state = State.None;
        IsActive = false; // 🔥 ปลดสถานะเมื่ออก

        if (stopAudioOnExit && tapeSystem != null) tapeSystem.StopTape();

        if (currentHover != null)
        {
            currentHover.OnHoverExit();
            currentHover = null;
        }

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);

        if (fpsCamera != null)
        {
            fpsCamera.transform.localPosition = origLocalPos;
            fpsCamera.transform.localRotation = origLocalRot;
            fpsCamera.SetActive(false);
        }
        if (mainCameraGroup != null) mainCameraGroup.SetActive(true);

        if (mainHUD != null) mainHUD.SetActive(true);
        if (crosshair != null) crosshair.SetVisible(true);

        if (playerController != null)
        {
            if (returnToOriginalPosition)
                WarpPlayer(playerReturnPos, playerReturnRot);

            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            playerController.enabled = true;
        }
        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        if (cameraAimScript != null) cameraAimScript.enabled = true;

        if (isPlayerNear && !hasPlayed && interactPrompt != null)
            interactPrompt.SetActive(true);

        // 🔥 เปลี่ยนไปใช้ Coroutine เพื่อรอปลดล็อกเมาส์ 1 เฟรม กันการชนกับ Editor
        StartCoroutine(RelockCursor());
    }

    // 🔥 Coroutine สำหรับล็อกเมาส์
    private IEnumerator RelockCursor()
    {
        yield return null; // รอให้ Editor ปลดล็อกเสร็จก่อน แล้วค่อยล็อกทับ
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SolvePuzzleAndExit()
    {
        hasPlayed = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (onCompleteFlag != null)
        {
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null) flags.Set(onCompleteFlag);
        }

        ExitMiniGame();
    }

    public void ZoomToRadio()
    {
        if (state != State.AtTable || radioViewPoint == null || fpsCamera == null) return;
        state = State.Transitioning;

        if (currentHover != null)
        {
            currentHover.OnHoverExit();
            currentHover = null;
        }

        StartCoroutine(MoveCamera(radioViewPoint.position, radioViewPoint.rotation, () =>
        {
            state = State.RadioView;
            if (radioMinigameUI != null)
            {
                radioMinigameUI.SetActive(true);
                // 🔥 เพิ่ม Log เช็กว่าเปิด UI ขึ้นจริงไหม
                Debug.Log($"[Radio] UI active in hierarchy = {radioMinigameUI.activeInHierarchy}");
            }
        }));
    }

    public void BackToTable()
    {
        if (state != State.RadioView) return;
        state = State.Transitioning;

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);

        GetTablePose(out Vector3 pos, out Quaternion rot);
        StartCoroutine(MoveCamera(pos, rot, () => state = State.AtTable));
    }

    private IEnumerator MoveCamera(Vector3 toPos, Quaternion toRot, System.Action onDone)
    {
        Transform t = fpsCamera.transform;
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;

        float time = 0f;
        while (time < zoomDuration)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, time / zoomDuration);
            t.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }

        t.SetPositionAndRotation(toPos, toRot);
        onDone?.Invoke();
    }

    private void HandleSlide()
    {
        if (PlayerRoot == null) return;

        float input = Input.GetAxisRaw("Horizontal");
        if (Mathf.Approximately(input, 0f)) return;

        slideOffset = Mathf.Clamp(slideOffset + input * slideSpeed * Time.deltaTime, -slideLimit, slideLimit);
        PlayerRoot.position = playerStandPos + slideAxis * slideOffset;
    }

    private void HandleMouse()
    {
        if (fpsCam == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = fpsCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        MiniGameInteractable found = null;
        foreach (var h in hits)
        {
            found = h.collider.GetComponentInParent<MiniGameInteractable>();
            if (found != null) break;
        }

        if (found != currentHover)
        {
            if (currentHover != null) currentHover.OnHoverExit();
            currentHover = found;
            if (currentHover != null) currentHover.OnHoverEnter();
        }

        if (currentHover != null && Input.GetMouseButtonDown(0))
        {
            currentHover.OnClick();
            if (currentHover == radioInteractable) ZoomToRadio();
        }
    }

    private void GetTablePose(out Vector3 pos, out Quaternion rot)
    {
        Transform root = PlayerRoot != null ? PlayerRoot : fpsCamera.transform.parent;
        if (root != null)
        {
            pos = root.TransformPoint(camRelPos);
            rot = root.rotation * camRelRot;
        }
        else
        {
            pos = fpsCamera.transform.position;
            rot = fpsCamera.transform.rotation;
        }
    }

    private void WarpPlayer(Vector3 pos, Quaternion rot)
    {
        Transform p = playerController.transform;
        CharacterController cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        p.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasPlayed)
        {
            isPlayerNear = true;
            if (interactPrompt != null && state == State.None) interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);
        }
    }
}