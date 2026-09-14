using System;
using UnityEngine;

public class PTSDManager : MonoBehaviour
{
    public static PTSDManager Instance { get; private set; }
    public static event Action<bool> OnPTSDStateChanged;

    [Header("PTSD Settings")]
    public bool isPTSDActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
        Debug.Log("สถานะ PTSD: " + (isPTSDActive ? "ทำงาน!" : "สงบลง"));
        OnPTSDStateChanged?.Invoke(isPTSDActive);
    }
}