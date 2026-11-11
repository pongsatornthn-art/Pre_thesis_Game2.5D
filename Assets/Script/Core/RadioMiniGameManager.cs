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
        if (isPlayerNear && !inMiniGame && !hasPlayed && Input.GetKeyDown(KeyCode.E))
        {
            EnterMiniGame();
        }

        if (inMiniGame && Input.GetKeyDown(exitKey))
        {
            ExitMiniGame();
        }
    }

    public void EnterMiniGame()
    {
        if (inMiniGame || hasPlayed) return;
        inMiniGame = true;
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

        if (isPlayerNear && !hasPlayed && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }
    }

    private IEnumerator RelockCursor()
    {
        yield return null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SolvePuzzleAndExit()
    {
        if (hasPlayed) return;
        hasPlayed = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

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
        if (other.CompareTag("Player") && !hasPlayed)
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