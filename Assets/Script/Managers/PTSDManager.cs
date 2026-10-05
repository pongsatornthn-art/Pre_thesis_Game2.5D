using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public enum PTSDMode { None, Survival_TypeA, Narrative_TypeB }

public class PTSDManager : MonoBehaviour
{
    public static PTSDManager Instance { get; private set; }

    // 🌟 เปลี่ยนมาส่งค่าเป็น true (เปิด) / false (ปิด)
    public static event Action<bool> OnPTSDStateChanged;

    [Header("PTSD State")]
    public PTSDMode currentMode = PTSDMode.None;

    [Header("Dual Reality Environments")]
    public GameObject realWorldEnv;
    public GameObject memoryWorldEnv;

    [Header("Player Tracking")]
    public GameObject player;
    private Vector3 savedPlayerPosition;

    [Header("PTSD Effects (เสียง, ภาพ, จอสั่น)")]
    public GameObject[] ptsdEffectsObjects;
    public UnityEvent OnEnterPTSD;
    public UnityEvent OnExitPTSD;

    [Header("Post-Processing Transition (บีบขอบจอมืด)")]
    public Volume transitionVolume;
    public float transitionSpeed = 3f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (realWorldEnv != null) realWorldEnv.SetActive(true);
        if (memoryWorldEnv != null) memoryWorldEnv.SetActive(false);
        ToggleEffects(false);

        if (transitionVolume != null) transitionVolume.weight = 0f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O)) TriggerPTSDSurvival();
        if (Input.GetKeyDown(KeyCode.LeftBracket)) TriggerPTSDNarrative();
        if (Input.GetKeyDown(KeyCode.P)) ExitPTSD();
    }

    // ==========================================
    // 🔴 เปิดโหมด Type A (เอาชีวิตรอด)
    // ==========================================
    public void TriggerPTSDSurvival()
    {
        if (currentMode != PTSDMode.None) return;

        currentMode = PTSDMode.Survival_TypeA;
        OnPTSDStateChanged?.Invoke(true); // แจ้งระบบอื่นว่า "เปิด"

        StartCoroutine(TransitionRoutine(true));
        Debug.Log("เข้าสู่สภาวะ PTSD Type A: ผีโผล่แล้ว!");
    }

    // ==========================================
    // 🧩 เปิดโหมด Type B (แก้ปริศนา)
    // ==========================================
    public void TriggerPTSDNarrative()
    {
        if (currentMode != PTSDMode.None) return;

        currentMode = PTSDMode.Narrative_TypeB;
        OnPTSDStateChanged?.Invoke(true); // แจ้งระบบอื่นว่า "เปิด"

        StartCoroutine(TransitionRoutine(true));
        Debug.Log("เข้าสู่สภาวะ PTSD Type B: ล็อคการซ่อนตัว!");
    }

    // ==========================================
    // 🟢 ปิดโหมด PTSD (แบบดึงกลับที่เดิม)
    // ==========================================
    public void ExitPTSD()
    {
        if (currentMode == PTSDMode.None) return;

        currentMode = PTSDMode.None;
        OnPTSDStateChanged?.Invoke(false); // แจ้งระบบอื่นว่า "ปิด"

        StartCoroutine(TransitionRoutine(false));
        Debug.Log("กลับสู่โลกความจริง");
    }

    // ==========================================
    // 🟢 ปิดโหมด PTSD (แบบบังคับวาร์ปไปจุดใหม่ - สำหรับเวลาโดนผีจับ)
    // ==========================================
    public void ExitPTSD_AndWarp(Transform newRespawnPoint)
    {
        if (currentMode == PTSDMode.None) return;

        currentMode = PTSDMode.None;
        OnPTSDStateChanged?.Invoke(false);

        // รัน Transition ปิด PTSD โดยส่งเป้าหมายการวาร์ปใหม่เข้าไปด้วย
        StartCoroutine(TransitionRoutine(false, newRespawnPoint));
        Debug.Log("ออกจาก PTSD และกำลังวาร์ปไปจุดเกิดใหม่...");
    }

    // 🌟 แก้ไข TransitionRoutine ให้รับค่า newRespawnPoint (ถ้าไม่มีก็เป็น null)
    private IEnumerator TransitionRoutine(bool isEnteringPTSD, Transform newRespawnPoint = null)
    {
        if (transitionVolume != null)
        {
            while (transitionVolume.weight < 1f)
            {
                transitionVolume.weight += Time.deltaTime * transitionSpeed;
                yield return null;
            }
            transitionVolume.weight = 1f;
        }

        if (isEnteringPTSD)
        {
            if (player != null) savedPlayerPosition = player.transform.position;

            if (realWorldEnv != null) realWorldEnv.SetActive(false);
            if (memoryWorldEnv != null) memoryWorldEnv.SetActive(true);

            ToggleEffects(true);
            OnEnterPTSD?.Invoke();
        }
        else // ตอนออกจากโหมด
        {
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                yield return null; // รอ 1 เฟรมให้ปิดฟิสิกส์สนิท

                // 🌟 เช็คว่ามีการส่งจุดเกิดใหม่มาให้หรือไม่?
                if (newRespawnPoint != null)
                {
                    // ถ้ามี ให้วาร์ปไปจุดใหม่
                    player.transform.position = newRespawnPoint.position;
                    player.transform.rotation = newRespawnPoint.rotation;
                }
                else
                {
                    // ถ้าไม่มี ให้ดึงกลับจุดเดิมที่จำไว้ตอนเข้าโหมด
                    player.transform.position = savedPlayerPosition;
                }

                Physics.SyncTransforms(); // อัปเดตตำแหน่งทันที

                yield return null; // รออีก 1 เฟรม

                if (cc != null) cc.enabled = true; // เปิดฟิสิกส์คืน
            }

            if (realWorldEnv != null) realWorldEnv.SetActive(true);
            if (memoryWorldEnv != null) memoryWorldEnv.SetActive(false);

            ToggleEffects(false);
            OnExitPTSD?.Invoke();
        }

        yield return new WaitForSeconds(0.5f);

        if (transitionVolume != null)
        {
            while (transitionVolume.weight > 0f)
            {
                transitionVolume.weight -= Time.deltaTime * transitionSpeed;
                yield return null;
            }
            transitionVolume.weight = 0f;
        }
    }

    private void ToggleEffects(bool isActive)
    {
        foreach (GameObject effectObj in ptsdEffectsObjects)
        {
            if (effectObj != null) effectObj.SetActive(isActive);
        }
    }
}