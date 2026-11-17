using UnityEngine;
using System.Collections;
using UnityEngine.Playables;

/// <summary>
/// มินิเกมโต๊ะทำงาน (ใช้ FPSCameraController) — [ข้อ 4] ย้ายงานสลับกล้อง/ล็อกผู้เล่น/คุมเมาส์
/// ไปให้ FirstPersonModeController ทำแล้ว ไฟล์นี้เหลือแค่ลำดับคัตซีนเข้า-ออก และธงเนื้อเรื่อง
/// </summary>
public class MiniGameManager : MonoBehaviour
{
    [Header("Camera & UI")]
    [Tooltip("กล้อง First-Person ของโต๊ะ (GameObject ที่มี Camera + FPSCameraController)")]
    public GameObject fpsCamera;
    public GameObject fpsCrosshair;
    public FPSCameraController fpsController;

    [Header("Player Positioning (ข้อ 8)")]
    [Tooltip("จุดยืนหน้าโต๊ะ (เว้นว่าง = ยืนตรงที่เดินเข้ามา)")]
    public Transform playerStandPoint;
    [Tooltip("ออกมินิเกมแล้วให้วาร์ปกลับจุดเดิมไหม (ใช้เมื่อมีจุดยืน)")]
    public bool returnToOriginalPosition = false;

    [Header("Cutscenes")]
    public PlayableDirector enterCutscene;
    public PlayableDirector exitCutscene;

    [Header("Story Integration")]
    [Tooltip("ใส่ธง (Flag) ที่ต้องการให้ระบบบันทึกว่าผ่านมินิเกมนี้แล้ว")]
    public StoryFlagId onCompleteFlag;

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
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp == null)
        {
            Debug.LogError("[MiniGameManager] ไม่พบ FirstPersonModeController ในซีน", this);
            yield break;
        }

        inMiniGame = true;

        // 1) ล็อกผู้เล่น + ซ่อน HUD ก่อน แต่ยังอยู่กล้องหลักระหว่างเล่นคัตซีนขาเข้า
        fp.LockPlayer(playerStandPoint, returnToOriginalPosition);

        float waitTime;
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

        // 2) เข้ากล้อง FPS: ล็อกเมาส์กลางจอ + เปิด FPSCameraController (คลิกขวาค้างเพื่อหัน)
        fp.ShowFirstPersonCamera(fpsCamera, unlockCursor: false, lookController: fpsController);

        if (fpsCrosshair != null) fpsCrosshair.SetActive(true);
    }

    public void SolvePuzzleWithCutscene()
    {
        StartCoroutine(ExitCutsceneSequence());
    }

    private IEnumerator ExitCutsceneSequence()
    {
        FirstPersonModeController fp = FirstPersonModeController.Instance;

        if (fp != null)
        {
            fp.SetLookLocked(true);
            fp.SetCursorUnlocked(false);
        }
        else if (fpsController != null)
        {
            fpsController.enabled = false;
        }

        if (fpsCrosshair != null) fpsCrosshair.SetActive(false);

        float waitTime;
        if (exitCutscene != null)
        {
            // กลับกล้องหลักเพื่อเล่นคัตซีน แต่ผู้เล่นยังล็อกอยู่
            if (fp != null) fp.ShowMainCamera();

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
        if (fpsCrosshair != null) fpsCrosshair.SetActive(false);

        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ExitMode();
        else if (fpsController != null) fpsController.enabled = false;

        inMiniGame = false;
        hasPlayed = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (onCompleteFlag != null)
        {
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null)
            {
                flags.Set(onCompleteFlag);
                Debug.Log($"[MiniGameManager] ส่งสัญญาณผ่านมินิเกมแล้ว: {onCompleteFlag.flagId}");
            }
            else
            {
                Debug.LogWarning("[MiniGameManager] ไม่พบระบบ IStoryFlags ในฉาก!");
            }
        }
    }
}
