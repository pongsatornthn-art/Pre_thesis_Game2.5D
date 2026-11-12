using UnityEngine;
using UnityEngine.UI;
using TMPro; // 🌟 1. เพิ่มบรรทัดนี้เพื่อเรียกใช้ TextMeshPro

public class SafeMinigame : MonoBehaviour
{
    [Header("ระบบ 3D (ตู้เซฟ)")]
    public GameObject safeCanvasUI;
    private bool isPlayerNear = false;
    private bool isMinigameActive = false;

    [Header("ตั้งค่ารหัสผ่านและของรางวัล")]
    public string correctPasscode = "1234";
    public KeyItemData rewardKey;

    [Header("เงื่อนไขเนื้อเรื่อง")]
    [Tooltip("ต้องครบก่อนถึงจะใส่รหัสได้ เช่น มีรูปวาดในคีย์ไอเทม (เว้นว่าง = กดได้เสมอ)")]
    [SerializeReference, SubclassPicker] private IStoryCondition unlockWhen;
    [Tooltip("ฉากที่เล่นตอนกดแล้วยังไม่ครบเงื่อนไข เช่น บทพูด 'ดูเหมือนต้องหารหัสก่อน'")]
    [SerializeField] private StorySequence onLocked;
    [Tooltip("ธงจำว่าเปิดแล้ว — โหลดเซฟแล้วตู้ไม่กลับมากดได้อีก")]
    [SerializeField] private StoryFlagId openedFlag;

    private GameObject nearPlayer;

    [Header("ระบบ UI มินิเกม")]
    public RectTransform dialImage;

    // 🌟 2. เปลี่ยนจาก Text ธรรมดา เป็น TextMeshProUGUI
    public TextMeshProUGUI[] digitDisplays;

    private string currentInput = "";
    private int currentDialNumber = 0;

    void Start()
    {
        if (safeCanvasUI != null) safeCanvasUI.SetActive(false);
    }

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && !isMinigameActive)
        {
            TryOpen();
        }

        if (isMinigameActive && Input.GetKeyDown(KeyCode.Escape))
        {
            EscapeGuard.MarkHandled(); // [Claude 2026-11-11] ใช้ ESC แล้ว → เมนูหยุดเกมไม่เด้งซ้อน
            CloseSafeMinigame();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            nearPlayer = other.gameObject;
        }
    }

    // กดที่ตู้: เปิดแล้ว = ไม่ทำอะไร · ยังไม่ครบเงื่อนไข = เล่นบทพูด · ครบ = เข้ามินิเกม
    private void TryOpen()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        StoryDirector director = StoryDirector.Instance;
        if (director != null && director.IsPlaying) return;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (openedFlag != null && flags != null && flags.Has(openedFlag)) return;

        if (unlockWhen != null && !unlockWhen.IsMet(StoryContext.Create(this, nearPlayer)))
        {
            if (onLocked != null && director != null) director.Play(onLocked);
            return;
        }

        OpenSafeMinigame();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            CloseSafeMinigame();
        }
    }

    /// <summary>[Claude 2026-11-12] ตู้เซฟเปิดอยู่ — ตัวกั้นปุ่มลัดใช้เช็ค (ห้ามถือปืนตี/ยิงระหว่างหมุนรหัส)</summary>
    public static bool AnyActive { get; private set; }

    // โหลดซีนใหม่/ปิด object ตอนเปิดตู้อยู่ → ไม่ให้ค่าค้าง (ไม่งั้นปุ่มลัดทั้งเกมโดนกั้นถาวร)
    private void OnDestroy() { if (isMinigameActive) AnyActive = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyActive = false;

    public void OpenSafeMinigame()
    {
        isMinigameActive = true;
        AnyActive = true;
        if (safeCanvasUI != null) safeCanvasUI.SetActive(true);
        currentInput = "";
        currentDialNumber = 0;
        UpdateDialVisual();
        UpdateDisplay();
    }

    public void CloseSafeMinigame()
    {
        isMinigameActive = false;
        AnyActive = false;
        if (safeCanvasUI != null) safeCanvasUI.SetActive(false);
    }

    public void RotateDialLeft()
    {
        currentDialNumber = (currentDialNumber - 1 + 10) % 10;
        UpdateDialVisual();
    }

    public void RotateDialRight()
    {
        currentDialNumber = (currentDialNumber + 1) % 10;
        UpdateDialVisual();
    }

    private void UpdateDialVisual()
    {
        if (dialImage != null)
            dialImage.localEulerAngles = new Vector3(0, 0, currentDialNumber * 36f);
    }

    public void ConfirmDigit()
    {
        if (currentInput.Length < 4)
        {
            currentInput += currentDialNumber.ToString();
            UpdateDisplay();

            if (currentInput.Length == 4)
            {
                CheckPasscode();
            }
        }
    }

    private void UpdateDisplay()
    {
        for (int i = 0; i < 4; i++)
        {
            if (digitDisplays != null && i < digitDisplays.Length && digitDisplays[i] != null)
            {
                if (i < currentInput.Length)
                    digitDisplays[i].text = currentInput[i].ToString();
                else
                    digitDisplays[i].text = "-";
            }
        }
    }

    private void CheckPasscode()
    {
        if (currentInput == correctPasscode)
        {
            Debug.Log("รหัสถูกต้อง! ตู้เซฟเปิดแล้ว");

            if (rewardKey != null)
            {
                rewardKey.Collect();
            }

            if (openedFlag != null) ServiceLocator.GetOptional<IStoryFlags>()?.Set(openedFlag);

            CloseSafeMinigame();

            if (GetComponent<Collider>() != null)
                GetComponent<Collider>().enabled = false;

            this.enabled = false;
        }
        else
        {
            Debug.Log("รหัสผิดพลาด! รีเซ็ตใหม่");
            currentInput = "";
            UpdateDisplay();
        }
    }
}