using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [ข้อ 4] ตัวกลางเข้า/ออกโหมด First-Person (วิทยุ / โต๊ะทำงาน / ดันประตู ฯลฯ) — วางในซีน 1 ตัว
///
/// ทำ 4 อย่างในที่เดียว แทนโค้ดที่เคยเขียนซ้ำใน MiniGameManager + RadioMiniGameManager:
///   1) สลับกล้อง: เปิด GameObject กล้อง First-Person ของจุดนั้น แล้วปิดก้อนกล้องหลัก (แบบเดียวกับโค้ดเดิม)
///   2) ล็อกผู้เล่น: ปิดสคริปต์ผู้เล่น + ซ่อน HUD + หยุดฟิสิกส์ + (ถ้ามี) ย้ายไปจุดยืน
///   3) คุมเมาส์: บอก CrosshairController ให้หยุดยุ่งกับ Cursor แล้วตั้งล็อก/โชว์เมาส์ตามจุดนั้น
///   4) ออกโหมด: คืนทุกอย่าง "กลับเป็นสถานะก่อนเข้า" (ไม่บังคับเปิดของที่เดิมปิดอยู่)
///
/// หมายเหตุ: กล้อง FPS ในโปรเจกต์นี้เป็น GameObject ที่มี Camera ของตัวเอง (FPSCameraController ใช้ GetComponent&lt;Camera&gt;)
/// จึงสลับด้วย SetActive ไม่ใช่ Priority ของ CinemachineCamera
/// </summary>
public class FirstPersonModeController : MonoBehaviour
{
    private static FirstPersonModeController instance;
    public static FirstPersonModeController Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<FirstPersonModeController>();
            return instance;
        }
    }

    [Header("กล้องหลัก (มุมมอง 2.5D)")]
    [Tooltip("ก้อนกล้องหลักทั้งชุด (Main Camera + กล้อง Cinemachine ของผู้เล่น) — จะถูกปิดตอนเข้าโหมด First-Person")]
    public GameObject mainCameraGroup;

    [Header("ผู้เล่น")]
    [Tooltip("ตัวผู้เล่น (ก้อนที่มี PlayerMovement / Rigidbody) — เว้นว่างได้ จะหาจาก PlayerMovement ให้เอง")]
    public Transform playerRoot;
    [Tooltip("สคริปต์ที่ต้องปิดตอนเข้าโหมด เช่น PlayerMovement, PlayerInteraction, PlayerCombat, สคริปต์เล็งกล้อง")]
    public Behaviour[] playerScriptsToDisable;
    [Tooltip("UI/ออบเจกต์ที่ต้องซ่อนตอนเข้าโหมด เช่น HUD หลัก, หลอด Stamina")]
    public GameObject[] objectsToHideInFP;

    /// <summary>true = ผู้เล่นถูกล็อกอยู่ในโหมดนี้ (รวมช่วงเล่นคัตซีนก่อน/หลังเข้ากล้อง FPS)</summary>
    public bool IsActive { get; private set; }
    /// <summary>true = ตอนนี้กำลังมองผ่านกล้อง First-Person</summary>
    public bool IsShowingFirstPersonCamera => currentFpCamera != null;

    private GameObject currentFpCamera;
    private Behaviour currentLook;

    private CursorLockMode savedLockState;
    private bool savedCursorVisible;
    private readonly List<bool> savedScriptStates = new List<bool>();
    private readonly List<bool> savedObjectStates = new List<bool>();

    private Rigidbody playerRb;
    private bool rbWasKinematic;
    private CharacterController playerCc;
    private bool ccWasEnabled;
    private bool restorePose;
    private Vector3 returnPos;
    private Quaternion returnRot;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[FirstPersonModeController] มีมากกว่า 1 ตัวในซีน — ปิดตัวนี้ไว้", this);
            enabled = false;
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        if (IsActive) CrosshairController.IsCursorOverriddenByMinigame = false; // กันเมาส์ค้างตอนเปลี่ยนซีนกลางมินิเกม
        instance = null;
    }

    // ───────────────────────── API หลัก ─────────────────────────

    /// <summary>
    /// เข้าโหมดทันที (ล็อกผู้เล่น + สลับกล้อง) — ใช้กับจุดที่ไม่มีคัตซีนคั่น เช่น วิทยุ / ดันประตู
    /// </summary>
    /// <param name="fpCamera">GameObject กล้อง First-Person ของจุดนั้น</param>
    /// <param name="unlockCursor">true = โชว์เมาส์ให้คลิก UI (วิทยุ) · false = ล็อกเมาส์กลางจอ (โต๊ะ/ดันประตู)</param>
    /// <param name="lookController">สคริปต์หันกล้อง เช่น FPSCameraController (null = กล้องนิ่ง)</param>
    /// <param name="standPoint">จุดยืนของผู้เล่น (null = ยืนที่เดิม)</param>
    /// <param name="returnToOriginalPosition">ออกโหมดแล้วให้วาร์ปกลับตำแหน่งเดิมไหม (ใช้เมื่อมี standPoint)</param>
    public void EnterMode(GameObject fpCamera, bool unlockCursor, Behaviour lookController = null,
                          Transform standPoint = null, bool returnToOriginalPosition = true)
    {
        LockPlayer(standPoint, returnToOriginalPosition);
        ShowFirstPersonCamera(fpCamera, unlockCursor, lookController);
    }

    /// <summary>
    /// ขั้นที่ 1: ล็อกผู้เล่นอย่างเดียว ยังไม่สลับกล้อง (ใช้ตอนต้องเล่นคัตซีนก่อนเข้ากล้อง FPS เช่น โต๊ะทำงาน)
    /// </summary>
    public void LockPlayer(Transform standPoint = null, bool returnToOriginalPosition = true)
    {
        if (IsActive) return;
        IsActive = true;

        savedLockState = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        CrosshairController.IsCursorOverriddenByMinigame = true;

        savedScriptStates.Clear();
        if (playerScriptsToDisable != null)
        {
            foreach (Behaviour script in playerScriptsToDisable)
            {
                savedScriptStates.Add(script != null && script.enabled);
                if (script != null) script.enabled = false;
            }
        }

        savedObjectStates.Clear();
        if (objectsToHideInFP != null)
        {
            foreach (GameObject obj in objectsToHideInFP)
            {
                savedObjectStates.Add(obj != null && obj.activeSelf);
                if (obj != null) obj.SetActive(false);
            }
        }

        FreezePlayer(standPoint, returnToOriginalPosition);
    }

    /// <summary>
    /// ขั้นที่ 2: สลับไปกล้อง First-Person (ถ้ายังไม่ได้ล็อกผู้เล่น จะล็อกให้เอง)
    /// </summary>
    public void ShowFirstPersonCamera(GameObject fpCamera, bool unlockCursor, Behaviour lookController = null)
    {
        if (!IsActive) LockPlayer();

        // สลับจากจุด FPS หนึ่งไปอีกจุดหนึ่งตรงๆ → ปิดของจุดเดิมก่อน
        if (currentFpCamera != null && currentFpCamera != fpCamera) currentFpCamera.SetActive(false);
        if (currentLook != null && currentLook != lookController) currentLook.enabled = false;

        currentFpCamera = fpCamera;
        currentLook = lookController;

        if (currentFpCamera != null)
        {
            HiddenGeometryReveal.Reveal(this);                       // [นา 2026-11-24] มุม FPS ต้องเห็นตู้/กำแพงที่กล้องหลักซ่อนไว้
            currentFpCamera.SetActive(true);                         // เปิดกล้องใหม่ก่อน แล้วค่อยปิดกล้องหลัก (กันจอดำ 1 เฟรม)
            if (mainCameraGroup != null) mainCameraGroup.SetActive(false);
            else Debug.LogWarning("[FirstPersonModeController] ยังไม่ได้ใส่ Main Camera Group — กล้องหลักจะยังเปิดค้าง", this);
        }

        if (currentLook != null) currentLook.enabled = true;

        SetCursorUnlocked(unlockCursor);
    }

    /// <summary>
    /// กลับไปกล้องหลัก แต่ผู้เล่นยังล็อกอยู่ (ใช้ตอนเล่นคัตซีนขาออก ก่อนเรียก ExitMode)
    /// </summary>
    public void ShowMainCamera()
    {
        if (currentLook != null)
        {
            currentLook.enabled = false;
            currentLook = null;
        }

        if (mainCameraGroup != null) mainCameraGroup.SetActive(true);

        if (currentFpCamera != null)
        {
            currentFpCamera.SetActive(false);
            currentFpCamera = null;
        }
        HiddenGeometryReveal.Restore(this);   // [นา 2026-11-24] กลับมุมหลัก = ซ่อนกำแพงเหมือนเดิม
    }

    private void LateUpdate()
    {
        if (currentFpCamera != null) HiddenGeometryReveal.KeepVisible();   // [นา 2026-11-24]
    }

    /// <summary>
    /// ออกจากโหมด: กล้องหลัก + คืนการควบคุม + คืนเมาส์และ UI เป็นสถานะก่อนเข้า
    /// </summary>
    public void ExitMode()
    {
        if (!IsActive) return;

        ShowMainCamera();
        UnfreezePlayer();

        if (playerScriptsToDisable != null)
        {
            for (int i = 0; i < playerScriptsToDisable.Length && i < savedScriptStates.Count; i++)
            {
                if (playerScriptsToDisable[i] != null) playerScriptsToDisable[i].enabled = savedScriptStates[i];
            }
        }

        if (objectsToHideInFP != null)
        {
            for (int i = 0; i < objectsToHideInFP.Length && i < savedObjectStates.Count; i++)
            {
                if (objectsToHideInFP[i] != null) objectsToHideInFP[i].SetActive(savedObjectStates[i]);
            }
        }

        Cursor.lockState = savedLockState;
        Cursor.visible = savedCursorVisible;
        CrosshairController.IsCursorOverriddenByMinigame = false;

        IsActive = false;

        // คืนเมาส์ซ้ำอีกรอบเฟรมถัดไป (แบบเดียวกับ RelockCursor เดิม) กันสคริปต์อื่นที่ยังรันในเฟรมเดียวกันสั่งทับ
        StopCoroutine(nameof(RestoreCursorNextFrame));
        StartCoroutine(nameof(RestoreCursorNextFrame));
    }

    private System.Collections.IEnumerator RestoreCursorNextFrame()
    {
        yield return null;
        if (IsActive) yield break; // เข้าโหมดใหม่ไปแล้ว ไม่ต้องคืน
        Cursor.lockState = savedLockState;
        Cursor.visible = savedCursorVisible;
    }

    /// <summary>ล็อก/ปลดการหันกล้องชั่วคราว (เช่น ตอนลากเมาส์ดันประตู หรือกดปุ่มบนโต๊ะ)</summary>
    public void SetLookLocked(bool locked)
    {
        if (currentLook != null) currentLook.enabled = !locked;
    }

    /// <summary>สลับโชว์/ล็อกเมาส์ระหว่างอยู่ในโหมด</summary>
    public void SetCursorUnlocked(bool unlock)
    {
        if (!IsActive) return;
        Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlock;
    }

    // ───────────────────────── ภายใน ─────────────────────────

    private void FreezePlayer(Transform standPoint, bool returnToOriginalPosition)
    {
        if (playerRoot == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) playerRoot = pm.transform;
        }

        restorePose = false;
        playerRb = null;
        playerCc = null;
        if (playerRoot == null) return;

        playerRb = playerRoot.GetComponent<Rigidbody>();
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

        playerCc = playerRoot.GetComponent<CharacterController>();
        if (playerCc != null)
        {
            ccWasEnabled = playerCc.enabled;
            playerCc.enabled = false;
        }

        if (standPoint != null)
        {
            restorePose = returnToOriginalPosition;
            returnPos = playerRoot.position;
            returnRot = playerRoot.rotation;
            playerRoot.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            Physics.SyncTransforms();
        }
    }

    private void UnfreezePlayer()
    {
        if (playerRoot != null && restorePose)
        {
            playerRoot.SetPositionAndRotation(returnPos, returnRot);
            Physics.SyncTransforms();
        }
        restorePose = false;

        if (playerCc != null) playerCc.enabled = ccWasEnabled;
        if (playerRb != null) playerRb.isKinematic = rbWasKinematic;

        playerCc = null;
        playerRb = null;
    }
}
