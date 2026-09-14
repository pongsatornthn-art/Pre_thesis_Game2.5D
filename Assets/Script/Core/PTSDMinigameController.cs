using UnityEngine;
using UnityEngine.Events;

public class PTSDMinigameController : MonoBehaviour
{
    [Header("Minigame State")]
    public bool isMinigameActive = false;
    private bool isPrompting = false;

    [Header("Win/Loss Conditions")]
    public int requiredSuccesses = 3;
    public int maxFailures = 3;

    [SerializeField] private int currentSuccesses = 0;
    [SerializeField] private int currentFailures = 0;

    [Header("Timing Mechanics (ระบบกดค้างแล้วปล่อย)")]
    [Tooltip("ความเร็วของขีดวิ่ง")]
    public float gaugeSpeed = 120f;
    private bool movingRight = true;

    [Header("Zones (0-100)")]
    [SerializeField] private float currentGaugeValue = 0f;
    [Tooltip("ความกว้างเริ่มต้นของเป้าหมายสีเขียว")]
    public float initialZoneWidth = 20f;
    [Tooltip("เมื่อกดพลาด เป้าจะแคบลงทีละเท่าไหร่")]
    public float shrinkAmountOnMiss = 5f;

    private float currentZoneWidth;

    public float targetZoneMin = 40f;
    public float targetZoneMax = 60f;

    [Header("Events (สำหรับส่งให้ระบบอื่น)")]
    public UnityEvent<bool> OnPromptUIVisibility;

    public UnityEvent OnMinigameStart;
    public UnityEvent OnMinigameSuccess;
    public UnityEvent OnMinigameFailed;
    public UnityEvent OnMinigameCanceled;
    public UnityEvent<int> OnProgressUpdated;
    public UnityEvent<int> OnMissed;

    private void Update()
    {
        if (!isMinigameActive)
        {
            return;
        }

        HandleInput();
        CheckCancel();
    }

    public void StartMinigame()
    {
        isMinigameActive = true;
        isPrompting = false;

        OnPromptUIVisibility?.Invoke(false);

        currentSuccesses = 0;
        currentFailures = 0;
        currentGaugeValue = 0f;
        movingRight = true;

        currentZoneWidth = initialZoneWidth;
        RandomizeZone();

        OnMinigameStart?.Invoke();
        Debug.Log("เริ่มมินิเกม! กด Spacebar ค้างไว้ ขีดจะวิ่ง ปล่อยให้ตรงสีเขียว");
    }

    private void RandomizeZone()
    {
        targetZoneMin = Random.Range(10f, 100f - currentZoneWidth - 10f);
        targetZoneMax = targetZoneMin + currentZoneWidth;
    }

    private void HandleInput()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            MoveCursor();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            CheckHit();
        }
    }

    private void MoveCursor()
    {
        if (movingRight)
        {
            currentGaugeValue += gaugeSpeed * Time.deltaTime;
            if (currentGaugeValue >= 100f)
            {
                currentGaugeValue = 100f;
                movingRight = false;
            }
        }
        else
        {
            currentGaugeValue -= gaugeSpeed * Time.deltaTime;
            if (currentGaugeValue <= 0f)
            {
                currentGaugeValue = 0f;
                movingRight = true;
            }
        }
    }

    private void CheckHit()
    {
        if (currentGaugeValue >= targetZoneMin && currentGaugeValue <= targetZoneMax)
        {
            HandleSuccess();
        }
        else
        {
            HandleMiss();
        }
    }

    private void HandleSuccess()
    {
        currentSuccesses++;
        OnProgressUpdated?.Invoke(currentSuccesses);
        Debug.Log($"ปล่อยเป๊ะ! สำเร็จ {currentSuccesses}/{requiredSuccesses} ครั้ง");

        if (currentSuccesses >= requiredSuccesses)
        {
            EndMinigame(true);
        }
        else
        {
            currentGaugeValue = 0f;
            movingRight = true;
            RandomizeZone();
        }
    }

    private void HandleMiss()
    {
        currentFailures++;
        OnMissed?.Invoke(currentFailures);
        Debug.Log($"พลาด! ปล่อยไม่ตรงจังหวะ ครั้งที่ {currentFailures}/{maxFailures}");

        if (currentFailures >= maxFailures)
        {
            EndMinigame(false);
        }
        else
        {
            currentZoneWidth = Mathf.Max(5f, currentZoneWidth - shrinkAmountOnMiss);
            currentGaugeValue = 0f;
            movingRight = true;
            RandomizeZone();
        }
    }

    private void CheckCancel()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMinigameActive = false;
            OnMinigameCanceled?.Invoke();
            Debug.Log("ยกเลิกการตั้งสติกลางคัน! เตรียมตัววิ่งหนี");
        }
    }

    private void EndMinigame(bool isVictory)
    {
        isMinigameActive = false;

        if (isVictory)
        {
            OnMinigameSuccess?.Invoke();
            Debug.Log("รอดแล้ว! อาการ PTSD สงบลง");
            if (PTSDManager.Instance != null) PTSDManager.Instance.SetPTSDState(false);
        }
        else
        {
            OnMinigameFailed?.Invoke();
            Debug.Log("ล้มเหลว! ผีพุ่งเข้ามาหาแล้ว!");
        }
    }

    public float GetCurrentGaugeValue()
    {
        return currentGaugeValue;
    }
}