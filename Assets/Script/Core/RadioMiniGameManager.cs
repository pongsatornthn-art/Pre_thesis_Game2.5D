using UnityEngine;

public class RadioMiniGameManager : MonoBehaviour
{
    [Header("Cameras & HUD")]
    public GameObject mainCamera;
    public GameObject fpsCamera;
    public GameObject mainHUD;

    [Header("Radio UI")]
    public GameObject radioMinigameUI;
    public GameObject interactPrompt;

    [Header("Crosshair")]
    public CrosshairUI crosshair;

    [Header("Radio Audio")]
    [Tooltip("MiniGameInteractable ของวิทยุ (ใช้เล่นเสียงทันทีตอนเข้ามินิเกม)")]
    public MiniGameInteractable radioInteractable;
    public bool playSoundOnEnter = true;

    [Header("Controllers (ปิดตอนเล่นมินิเกม)")]
    public MonoBehaviour playerController;
    public MonoBehaviour playerInteraction;
    public MonoBehaviour playerCombat;
    public MonoBehaviour cameraAimScript;

    [Header("Story Integration")]
    public StoryFlagId onCompleteFlag;

    [Header("Minigame Raycast")]
    public float raycastDistance = 10f;

    private bool isPlayerNear = false;
    private bool inMiniGame = false;
    private bool hasPlayed = false;

    private Camera fpsCam;
    private MiniGameInteractable currentHover;

    private void Start()
    {
        if (fpsCamera != null)
        {
            fpsCam = fpsCamera.GetComponentInChildren<Camera>(true);

            // กันเคสไม่มี AudioListener พอปิดกล้องหลักแล้วเสียงหาย
            if (fpsCamera.GetComponentInChildren<AudioListener>(true) == null)
            {
                fpsCamera.AddComponent<AudioListener>();
            }
            fpsCamera.SetActive(false);
        }

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerNear && !inMiniGame && !hasPlayed && Input.GetKeyDown(KeyCode.E))
        {
            EnterMiniGame();
        }

        if (inMiniGame)
        {
            HandleMiniGameMouse();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitMiniGame();
            }
        }
    }

    // เปลี่ยนเป็น public เพื่อให้เรียกจาก UnityEvent / สคริปต์อื่นได้
    public void EnterMiniGame()
    {
        if (inMiniGame || hasPlayed) return;
        inMiniGame = true;

        if (interactPrompt != null) interactPrompt.SetActive(false);
        if (crosshair != null) crosshair.SetVisible(false);

        if (playerController != null)
        {
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            playerController.enabled = false;
        }

        if (playerInteraction != null) playerInteraction.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;
        if (cameraAimScript != null) cameraAimScript.enabled = false;

        if (mainHUD != null) mainHUD.SetActive(false);
        if (fpsCamera != null) fpsCamera.SetActive(true);   // เปิดก่อน จะได้มี AudioListener ตลอด
        if (mainCamera != null) mainCamera.SetActive(false);

        if (radioMinigameUI != null) radioMinigameUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // เล่นเสียงทันที
        if (playSoundOnEnter && radioInteractable != null)
        {
            radioInteractable.PlaySound();
        }
    }

    // ใช้จัดการ hover / คลิกของ MiniGameInteractable ตอนอยู่ในมินิเกม
    private void HandleMiniGameMouse()
    {
        if (fpsCam == null) return;

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
        }
    }

    public void SolvePuzzleAndExit()
    {
        hasPlayed = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (onCompleteFlag != null)
        {
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null)
            {
                flags.Set(onCompleteFlag);
            }
        }

        ExitMiniGame();
    }

    public void ExitMiniGame()
    {
        inMiniGame = false;

        if (currentHover != null)
        {
            currentHover.OnHoverExit();
            currentHover = null;
        }

        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);

        if (mainCamera != null) mainCamera.SetActive(true);
        if (fpsCamera != null) fpsCamera.SetActive(false);
        if (mainHUD != null) mainHUD.SetActive(true);
        if (crosshair != null) crosshair.SetVisible(true);

        if (playerController != null) playerController.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        if (cameraAimScript != null) cameraAimScript.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (isPlayerNear && !hasPlayed && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }
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