using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class PTSDManager : MonoBehaviour
{
    public static PTSDManager Instance { get; private set; }
    public static event Action<bool> OnPTSDStateChanged;

    [Header("PTSD Settings")]
    public bool isPTSDActive = false;

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
    [Tooltip("ลากออบเจกต์ PTSD_TransitionVolume มาใส่ตรงนี้")]
    public Volume transitionVolume;
    [Tooltip("ความเร็วในการบีบจอ/ขยายจอ")]
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
        if (Input.GetKeyDown(KeyCode.O)) SetPTSDState(true);
        if (Input.GetKeyDown(KeyCode.P)) SetPTSDState(false);
    }

    public void SetPTSDState(bool state)
    {
        if (isPTSDActive == state) return;

        isPTSDActive = state;
        StartCoroutine(TransitionRoutine(state));
    }

    private IEnumerator TransitionRoutine(bool isEnteringPTSD)
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
        else
        {
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = savedPlayerPosition;
                if (cc != null) cc.enabled = true;
            }
            if (realWorldEnv != null) realWorldEnv.SetActive(true);
            if (memoryWorldEnv != null) memoryWorldEnv.SetActive(false);

            ToggleEffects(false);
            OnExitPTSD?.Invoke();
        }

        OnPTSDStateChanged?.Invoke(isPTSDActive);

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