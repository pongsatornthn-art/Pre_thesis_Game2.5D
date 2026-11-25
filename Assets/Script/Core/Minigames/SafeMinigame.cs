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

    // [Claude 2026-11-24] หมุนหน้าปัดให้เลขตรงลูกศรเป๊ะ + หมุนนุ่ม
    [Header("หน้าปัด (หมุนตรงเลข)")]
    [Tooltip("SafeDialFace บน Dial Image — ใส่แล้วใช้จำนวนช่อง/ทิศตามหน้าปัด (เว้นว่าง = 10 ช่อง ตามเข็ม)")]
    public SafeDialFace dialFace;
    [Tooltip("ชดเชยมุม ถ้าเลข 0 ในรูปไม่ได้อยู่บนสุดพอดี (องศา)")]
    public float dialZeroOffset = 0f;
    [Tooltip("เวลาหมุนไป 1 ช่อง (วินาที) — 0 = กระโดดทันที")]
    [Min(0f)] public float dialTurnTime = 0.12f;
    [Tooltip("เสียงคลิกตอนหมุน (เว้นว่างได้)")]
    public AudioClip dialClickSound;
    public AudioClip wrongSound;
    public AudioClip openSound;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("ใช้ A/D (←/→) หมุน · Space/Enter ยืนยัน · ลูกกลิ้งเมาส์หมุน")]
    public bool keyboardControls = true;

    private float dialAngle;        // มุมที่แสดงอยู่
    private float dialTarget;       // มุมเป้าหมาย (สะสม ไม่วนกลับ → 9→0 หมุนแค่ 1 ช่อง)
    private float dialVelocity;
    private AudioSource sfx;

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
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && !isMinigameActive && !InteractPromptMarker.OtherPromptShown(gameObject))   // [นา 2026-11-24] E ปุ่มเดียว: ป้ายโชว์ของชิ้นอื่น = ไม่ใช่ของเรา
        {
            TryOpen();
        }

        if (isMinigameActive) UpdateDialMotion();

        if (isMinigameActive && keyboardControls)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) RotateDialLeft();
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) RotateDialRight();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmDigit();
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f) RotateDialRight();
            else if (wheel < -0.01f) RotateDialLeft();
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
        dialTarget = dialAngle = AngleFor(0);
        dialVelocity = 0f;
        ApplyDialAngle();
        UpdateDisplay();
    }

    public void CloseSafeMinigame()
    {
        isMinigameActive = false;
        AnyActive = false;
        if (safeCanvasUI != null) safeCanvasUI.SetActive(false);
    }

    private int DialCount => dialFace != null ? Mathf.Max(1, dialFace.count) : 10;
    private float DialStep => 360f / DialCount;
    private float DialDir => dialFace != null && !dialFace.clockwise ? -1f : 1f;
    private float AngleFor(int n) => DialDir * n * DialStep + dialZeroOffset;

    public void RotateDialLeft() => StepDial(-1);
    public void RotateDialRight() => StepDial(1);

    /// <summary>เลื่อนเลข ±1 — มุมเป้าหมายสะสมต่อเนื่อง หน้าปัดเลยหมุนทางสั้นเสมอ (9→0 = 1 ช่อง ไม่หมุนย้อนเกือบรอบ)</summary>
    private void StepDial(int delta)
    {
        currentDialNumber = ((currentDialNumber + delta) % DialCount + DialCount) % DialCount;
        dialTarget += DialDir * delta * DialStep;
        if (dialTurnTime <= 0f) { dialAngle = dialTarget; ApplyDialAngle(); }
        PlaySfx(dialClickSound);
    }

    private void UpdateDialMotion()
    {
        if (Mathf.Abs(dialTarget - dialAngle) < 0.01f) { dialAngle = dialTarget; ApplyDialAngle(); return; }
        // unscaledDeltaTime: หมุนได้แม้เกมหยุดเวลา
        dialAngle = Mathf.SmoothDamp(dialAngle, dialTarget, ref dialVelocity, dialTurnTime * 0.5f, Mathf.Infinity, Time.unscaledDeltaTime);
        ApplyDialAngle();
    }

    private void ApplyDialAngle()
    {
        if (dialImage != null) dialImage.localEulerAngles = new Vector3(0, 0, dialAngle);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        if (sfx == null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            sfx.ignoreListenerPause = true;
        }
        sfx.PlayOneShot(clip, sfxVolume);
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
            PlaySfx(openSound);

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
            PlaySfx(wrongSound);
            currentInput = "";
            UpdateDisplay();
        }
    }
}