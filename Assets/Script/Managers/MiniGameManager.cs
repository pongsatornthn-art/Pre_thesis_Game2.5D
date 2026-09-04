using UnityEngine;
using System.Collections;
using UnityEngine.Playables;

public class MiniGameManager : MonoBehaviour
{
    [Header("Cameras & UI")]
    public GameObject mainCamera;
    public GameObject fpsCamera;
    public GameObject mainHUD;
    public GameObject fpsCrosshair;

    [Header("Controllers")]
    public MonoBehaviour playerController;
    public MonoBehaviour playerInteraction;
    public MonoBehaviour playerCombat;
    public MonoBehaviour cameraAimScript;
    public FPSCameraController fpsController;

    [Header("Cutscenes")]
    public PlayableDirector enterCutscene;
    public PlayableDirector exitCutscene;

    private bool inMiniGame = false;
    private bool hasPlayed = false;

    private void Start()
    {
        if (fpsCamera != null) fpsCamera.SetActive(false);
        if (fpsController != null) fpsController.enabled = false;
        if (fpsCrosshair != null) fpsCrosshair.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !inMiniGame && !hasPlayed)
        {
            StartCoroutine(StartMiniGameSequence());
        }
    }

    private IEnumerator StartMiniGameSequence()
    {
        inMiniGame = true;

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

        if (mainCamera != null) mainCamera.SetActive(true);
        if (fpsCamera != null) fpsCamera.SetActive(false);

        float waitTime = 0f;

        if (enterCutscene != null)
        {
            enterCutscene.Play();
            waitTime = (float)enterCutscene.duration;
        }
        else
        {
            waitTime = 2.5f;
        }

        yield return new WaitForSeconds(waitTime);

        if (mainCamera != null) mainCamera.SetActive(false);
        if (fpsCamera != null) fpsCamera.SetActive(true);
        if (fpsController != null) fpsController.enabled = true;

        if (fpsCrosshair != null) fpsCrosshair.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SolvePuzzleWithCutscene()
    {
        StartCoroutine(ExitCutsceneSequence());
    }

    private IEnumerator ExitCutsceneSequence()
    {
        fpsController.enabled = false;
        if (fpsCrosshair != null) fpsCrosshair.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        float waitTime = 0f;

        if (exitCutscene != null)
        {
            if (fpsCamera != null) fpsCamera.SetActive(false);
            if (mainCamera != null) mainCamera.SetActive(true);

            exitCutscene.Play();
            waitTime = (float)exitCutscene.duration;
            Debug.Log("กำลังเล่นคัทซีนตอนออก... ความยาว: " + waitTime + " วินาที");
        }
        else
        {
            waitTime = 3f;
        }

        yield return new WaitForSeconds(waitTime);

        SolvePuzzleAndExit();
    }

    public void SolvePuzzleAndExit()
    {
        if (fpsController != null) fpsController.enabled = false;
        if (fpsCamera != null) fpsCamera.SetActive(false);
        if (mainCamera != null) mainCamera.SetActive(true);

        if (mainHUD != null) mainHUD.SetActive(true);

        if (playerController != null) playerController.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerCombat != null) playerCombat.enabled = true;
        if (cameraAimScript != null) cameraAimScript.enabled = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        inMiniGame = false;

        hasPlayed = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}