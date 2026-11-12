using System.Collections;
using UnityEngine;

public class RadioMiniGameManager : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [Header("Cameras & HUD")]
    public GameObject mainCameraGroup;
    public GameObject fpsCamera;
    public GameObject mainHUD;

    [Header("Radio UI")]
    public GameObject radioMinigameUI;
    public GameObject interactPrompt;
    public GameObject crosshair; // 🔥 เปลี่ยนเป็น GameObject ให้ใส่ง่ายๆ

    [Header("Audio System")]
    public RadioTapeSystem tapeSystem;

    [Header("Player Positioning")]
    public Transform playerStandPoint;
    public bool returnToOriginalPosition = true;

    [Header("Controllers (ปิดตอนเล่นมินิเกม)")]
    public MonoBehaviour playerController;
    public MonoBehaviour playerInteraction;
    public MonoBehaviour playerCombat;
    public MonoBehaviour cameraAimScript;

    [Header("Exit")]
    public KeyCode exitKey = KeyCode.Escape;

    [Header("Story Integration")]
    public StoryFlagId onCompleteFlag;

    private bool isPlayerNear = false;
    private bool inMiniGame = false;
    private bool hasPlayed = false;
    private CursorLockMode savedLock = CursorLockMode.Confined;
    private bool savedVisible = true;

    private Vector3 playerReturnPos;
    private Quaternion playerReturnRot;

    private Rigidbody playerRb;
    private bool rbWasKinematic;

    private void Start()
    {
        if (fpsCamera != null)
        {
            if (fpsCamera.GetComponentInChildren<AudioListener>(true) == null)
            {
                fpsCamera.AddComponent<AudioListener>();
            }
            fpsCamera.SetActive(false);
        }

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void OnDisable()
    {
        IsActive = false;
    }

    private void Update()
    {
        if (isPlayerNear && !inMiniGame && !entering && Input.GetKeyDown(KeyCode.E)) // [Claude 2026-11-11 งานข้อ 8] เล่นวิทยุจบแล้วยังเข้าโต๊ะได้ (มาเซฟที่สมุด)
        {
            StartCoroutine(EnterAfterThumbnail());
        }

        // [Claude 2026-11-11] ESC ออกได้เสมอ (นอกจากปุ่ม Exit Key ที่ตั้งใน Inspector — ในซีนตั้งเป็น Q)
        if (inMiniGame && (Input.GetKeyDown(exitKey) || Input.GetKeyDown(KeyCode.Escape)))
        {
            // หน้าเลือกช่องเซฟ / อนิเมชั่นสมุดกำลังทำงาน = ปล่อยให้มันจัดการ ESC เอง
            if (EscapeGuard.HandledThisFrame || SaveSlotPickerUI.IsOpen || DeskSaveNotebook.IsBusy) return;

            EscapeGuard.MarkHandled(); // ใช้ ESC แล้ว → เมนูหยุดเกมไม่เด้งซ้อน

            // ปุ่มเทปเปิดอยู่ → ปิดแค่ปุ่มเทป กลับไปเล็งกลางจอ (กดอีกทีค่อยออกจากโต๊ะ)
            if (radioMinigameUI != null && radioMinigameUI.activeSelf) radioMinigameUI.SetActive(false);
            else ExitMiniGame();
        }
    }

    // [Claude 2026-11-11 งานข้อ 8] ถ่ายฉากก่อนเปลี่ยนเป็นมุมโต๊ะ → ใช้เป็นรูปบนการ์ดช่องเซฟ (ไม่งั้นได้รูปโต๊ะทุกช่อง)
    private bool entering;
    private IEnumerator EnterAfterThumbnail()
    {
        entering = true;
        yield return SaveThumbnail.Capture();
        entering = false;
        EnterMiniGame();
    }

    public void EnterMiniGame()
    {
        if (inMiniGame) return;
        inMiniGame = true;
        savedLock = Cursor.lockState;   // [Claude 2026-11-11] จำสถานะเมาส์ไว้คืนตอนออก
        savedVisible = Cursor.visible;
        IsActive = true;

        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (crosshair != null) crosshair.SetActive(false);
        if (mainHUD != null) mainHUD.SetActive(false);

        if (playerController != null)
        {
            Transform p = playerController.transform;
            playerReturnPos = p.position;
            playerReturnRot = p.rotation;

            playerController.enabled = false;

            playerRb = playerController.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                rbWasKinematic = playerRb.isKinematic;
                if (!playerRb.isKinematic)
                {
                    playerRb.linearVelocity = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                }
                playerRb.isKinematic = true;
            }

            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (playerStandPoint != null)
            {
                p.SetPositionAndRotation(playerStandPoint.position, playerStandPoint.rotation);
            }
            Physics.SyncTransforms();
        }

        if (playerInteraction != null) playerInteraction.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;
        if (cameraAimScript != null) cameraAimScript.enabled = false;

        if (fpsCamera != null) fpsCamera.SetActive(true);
        if (mainCameraGroup != null) mainCameraGroup.SetActive(false);

        // 🔥 เอาคำสั่ง radioMinigameUI.SetActive(true) ออกจากตรงนี้แล้ว
        // UI จะไม่เปิดเองจนกว่าเราจะคลิกที่วิทยุ

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitMiniGame()
    {
        if (!inMiniGame) return;
        inMiniGame = false;
        IsActive = false;
        StopAllCoroutines();

        if (tapeSystem != null) tapeSystem.StopTape();

        // 🔥 สั่งปิด UI ตรงนี้ ถ้าช่อง Inspector ใส่ถูก ยังไงก็หายชัวร์
        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);

        if (fpsCamera != null) fpsCamera.SetActive(false);
        if (mainCameraGroup != null) mainCameraGroup.SetActive(true);

        if (mainHUD != null) mainHUD.SetActive(true);
        if (crosshair != null) crosshair.SetActive(true);

        if (playerController != null)
        {
            Transform p = playerController.transform;
            CharacterController cc = p.GetComponent<CharacterController>();

            if (returnToOriginalPosition)
            {
                if (cc != null) cc.enabled = false;
                p.SetPositionAndRotation(playerReturnPos, playerReturnRot);
                Physics.SyncTransforms();
            }

            if (cc != null) cc.enabled = true;
            if (playerRb != null) playerRb.isKinematic = rbWasKinematic;
            playerController.enabled = true;
        }

        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        if (cameraAimScript != null) cameraAimScript.enabled = true;

        StartCoroutine(RelockCursor());

        if (isPlayerNear && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }
    }

    private IEnumerator RelockCursor()
    {
        yield return null;
        // [แก้โดย Claude 2026-11-11] เดิมล็อกเมาส์กลางจอ + ซ่อน → เกมนี้เล็งปืนด้วยเมาส์ เล็งไม่ได้หลังออกจากโต๊ะ
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }

    public void SolvePuzzleAndExit()
    {
        if (hasPlayed) return;
        hasPlayed = true;

        // [Claude 2026-11-11 งานข้อ 8] ไม่ปิดโต๊ะแล้ว — ผู้เล่นต้องกลับมาเซฟที่สมุดบนโต๊ะนี้ได้เสมอ

        if (onCompleteFlag != null)
        {
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null) flags.Set(onCompleteFlag);
        }

        StartCoroutine(ExitAfterTape());
    }

    private IEnumerator ExitAfterTape()
    {
        yield return null;
        while (inMiniGame && tapeSystem != null && tapeSystem.IsPlaying)
            yield return null;

        ExitMiniGame();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            if (interactPrompt != null && !inMiniGame) interactPrompt.SetActive(true);
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