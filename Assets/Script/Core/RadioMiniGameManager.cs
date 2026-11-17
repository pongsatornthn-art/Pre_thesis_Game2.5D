using System.Collections;
using UnityEngine;

/// <summary>
/// มินิเกมวิทยุ/โต๊ะเซฟ — [ข้อ 4] ย้ายงานสลับกล้อง/ล็อกผู้เล่น/คุมเมาส์ ไปให้ FirstPersonModeController ทำแล้ว
/// ลอจิกเดิมของ branch ทดสอบ ยังอยู่ครบ: ถ่ายรูปก่อนเข้า (การ์ดเซฟ), ESC/Q ออก, เข้าซ้ำได้เพื่อเซฟที่สมุด
/// </summary>
public class RadioMiniGameManager : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [Header("Camera")]
    [Tooltip("กล้อง First-Person ของโต๊ะ/วิทยุ (GameObject ที่มี Camera)")]
    public GameObject fpsCamera;

    [Header("Radio UI")]
    public GameObject radioMinigameUI;
    public GameObject interactPrompt;

    [Header("Audio System")]
    public RadioTapeSystem tapeSystem;

    [Header("Player Positioning (ข้อ 8: ขยับจุดนี้ให้ใกล้โต๊ะขึ้น)")]
    public Transform playerStandPoint;
    public bool returnToOriginalPosition = true;

    [Header("Exit")]
    public KeyCode exitKey = KeyCode.Escape;

    [Header("Story Integration")]
    public StoryFlagId onCompleteFlag;

    private bool isPlayerNear = false;
    private bool inMiniGame = false;
    private bool hasPlayed = false;

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

        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp == null)
        {
            Debug.LogError("[RadioMiniGameManager] ไม่พบ FirstPersonModeController ในซีน", this);
            return;
        }

        inMiniGame = true;
        IsActive = true;

        if (interactPrompt != null) interactPrompt.SetActive(false);

        // [ข้อ 4] สลับกล้อง + ล็อกผู้เล่น + ซ่อน HUD + จำ/ตั้งเมาส์ ทำในตัวกลางที่เดียว
        // (ถ้ากล้องโต๊ะเปิด centerAim ใน FPSCameraController มันจะล็อกเมาส์กลางจอต่อเอง เหมือนเดิม)
        fp.EnterMode(fpsCamera, unlockCursor: true, lookController: null,
                     standPoint: playerStandPoint, returnToOriginalPosition: returnToOriginalPosition);

        // UI วิทยุยังไม่เปิดเองตรงนี้ จะเปิดตอนคลิกที่วิทยุ (เหมือนเดิม)
    }

    public void ExitMiniGame()
    {
        if (!inMiniGame) return;
        inMiniGame = false;
        IsActive = false;
        StopAllCoroutines();
        entering = false;

        if (tapeSystem != null) tapeSystem.StopTape();
        if (radioMinigameUI != null) radioMinigameUI.SetActive(false);

        // [ข้อ 4] คืนกล้อง/ผู้เล่น/HUD/เมาส์ เป็นสถานะก่อนเข้า
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ExitMode();

        if (isPlayerNear && interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }
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
