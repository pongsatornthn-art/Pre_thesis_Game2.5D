using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// [ข้อ 1] Door Barricade Struggle — ลากเมาส์ขึ้นเพื่อยันประตูสู้แรงดันของ Resonant (ตาม GDD)
///
/// แปะที่ "ประตู" (ตัวที่มี DoorController + Animator เช่น Door00) — 1 ประตู = 1 ตัว
/// เริ่มเกมได้ 3 ทาง: DoorBarricadeTrigger (โซนหน้าประตู) · เรียก BeginNormal()/BeginTutorial() จาก UnityEvent/Timeline · โค้ดอื่นเรียก Begin()
///
/// Door_Pos: 100 = ปิดสนิท · 50 = จุดเริ่ม · 0 = ประตูพัง (แพ้)
/// ลากเมาส์ขึ้น → เพิ่ม PushVelocity (มีเพดาน + แรงเฉื่อย) · ลากลงไม่หักค่า (ยกเมาส์ตั้งหลักได้)
/// ศัตรูดันคงที่ตลอด + กระแทกเป็นระยะ · อยู่ในโซน 80–100 ครบ 1.5 วิ = ชนะ
///
/// ใช้ FirstPersonModeController (ข้อ 4) สลับกล้อง/ล็อกผู้เล่น/ล็อกเมาส์กลางจอ
/// </summary>
public class DoorBarricadeStruggle : MonoBehaviour
{
    public enum Result { Win, Fail, Aborted }

    // ───────── สถานะรวม (ให้ระบบอื่นเช็คได้) ─────────
    /// <summary>ประตูที่กำลังยันอยู่ (null = ไม่มี)</summary>
    public static DoorBarricadeStruggle Active { get; private set; }
    public static bool AnyActive => Active != null;
    /// <summary>เวลาปลอดภัยที่เหลือหลังยันประตูสำเร็จ (Safe_Time_Reward) — ให้ AI/ทริกเกอร์อื่นเช็คก่อนโจมตี</summary>
    public static float SafeTimeRemaining => Mathf.Max(0f, safeUntil - Time.time);
    private static float safeUntil = -1f;

    public static event Action<DoorBarricadeStruggle> OnAnyStarted;
    public static event Action<DoorBarricadeStruggle> OnAnySlam;
    public static event Action<DoorBarricadeStruggle> OnAnyTutorialRetry;
    public static event Action<DoorBarricadeStruggle, Result> OnAnyEnded;

    /// <summary>จบมินิเกมของประตูนี้ (ชนะ/แพ้/ยกเลิก)</summary>
    public event Action<DoorBarricadeStruggle, Result> Ended;

    // ───────── Inspector ─────────
    [Header("ประตู")]
    [Tooltip("ตัวที่หมุนเป็นบานประตู — เว้นว่าง = ตัวที่มี Animator ของประตู (อนิเมชันเปิด/ปิดเดิมหมุนตัวนี้) หรือตัวเอง")]
    public Transform doorPanel;
    [Tooltip("องศาที่บานแง้มตอน Door_Pos = 0 (ใส่ติดลบถ้าประตูแง้มผิดทาง)")]
    public float openAngle = 45f;
    [Tooltip("แกนบานพับในพิกัดโลก — ปกติชี้ขึ้น (0,1,0)")]
    public Vector3 hingeAxis = Vector3.up;
    [Tooltip("เว้นว่าง = หาในตัวเอง/ตัวแม่")]
    public DoorController doorController;
    [Tooltip("Animator ของประตู (ปิดชั่วคราวระหว่างยัน ไม่ให้แย่งหมุนบาน) — เว้นว่าง = หาในตัวเอง")]
    public Animator doorAnimator;

    [Header("กล้อง / ตำแหน่งผู้เล่น")]
    [Tooltip("กล้องมุมยันประตู (GameObject ที่มี Camera วางในห้อง มองไปที่ประตู) — ปิดไว้ได้ สคริปต์จัดการเอง")]
    public GameObject struggleCamera;
    [Tooltip("จุดยืนผู้เล่นตอนยันประตู (ฝั่งในห้อง ชิดประตู)")]
    public Transform playerStandPoint;

    [Header("ค่าตาม GDD")]
    [Range(0f, 100f)] public float doorStartPos = 50f;
    [Range(0f, 100f)] public float lockZoneMin = 80f;
    [Tooltip("ต้องอยู่ในโซนล็อกต่อเนื่องกี่วินาทีถึงชนะ (Win_Countdown_Timer)")]
    [Min(0.1f)] public float winHoldSeconds = 1.5f;
    [Tooltip("Mouse_Push_Sensitivity — ตัวคูณแปลงความเร็วลากเมาส์ขึ้น เป็นความเร็วดันประตู")]
    [Min(0f)] public float mousePushSensitivity = 1.8f;
    [Tooltip("Max_Push_Velocity (หน่วย/วินาที)")]
    [Min(0f)] public float maxPushVelocity = 45f;
    [Tooltip("ความเร็วที่แรงดันเพิ่มขึ้นถึงเป้า (หน่วย/วินาที²) — ยิ่งน้อยยิ่งรู้สึกประตูหนัก")]
    [Min(1f)] public float pushAcceleration = 260f;
    [Tooltip("Push_Inertia_Damping — แรงหน่วงตอนหยุดลาก (6.5 ≈ แรงดันตกหมดใน ~0.25 วิ)")]
    [Min(0f)] public float pushInertiaDamping = 6.5f;
    [Tooltip("Enemy_Base_Force (หน่วย/วินาที)")]
    [Min(0f)] public float enemyBaseForce = 18f;
    [Tooltip("Enemy_Slam_Interval — สุ่มระหว่าง X ถึง Y วินาที")]
    public Vector2 slamIntervalRange = new Vector2(1.5f, 2.0f);
    [Tooltip("Enemy_Slam_Impulse — สุ่มระหว่าง X ถึง Y หน่วย")]
    public Vector2 slamImpulseRange = new Vector2(12f, 18f);
    [Tooltip("Safe_Time_Reward (วินาที) หลังชนะ")]
    [Min(0f)] public float safeTimeReward = 20f;
    [Tooltip("ช่วงแรกหลังเริ่ม ศัตรูยังไม่ดัน (ให้ผู้เล่นตั้งตัว)")]
    [Min(0f)] public float startGraceSeconds = 0.6f;

    [Header("Tutorial (ฉากวิ่งหนีแรก)")]
    [Range(0.1f, 1f)] public float tutorialForceMultiplier = 0.6f;
    [Range(0.1f, 1f)] public float tutorialSlamMultiplier = 0.6f;
    [Min(1f)] public float tutorialSlamIntervalMultiplier = 1.3f;
    [Min(0f)] public float tutorialGraceSeconds = 1.5f;
    [Tooltip("Tutorial แพ้ = เริ่มใหม่ (ไม่ตาย)")]
    public bool tutorialRetryOnFail = true;

    [Header("แพ้ (เกมจริง)")]
    [Tooltip("แพ้แล้วฆ่าผู้เล่น (Instant Death → ระบบตาย/โหลดเซฟเดิมทำงานต่อ)")]
    public bool killPlayerOnFail = true;
    [Tooltip("รอกี่วินาทีหลังประตูพัง ก่อนตาย (เวลาให้ jumpscare/คัตซีนจาก On Fail เล่น)")]
    [Min(0f)] public float failDelay = 1.2f;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip[] slamSounds;
    public AudioClip[] enemyGrowls;
    public Vector2 growlIntervalRange = new Vector2(2.5f, 4.5f);
    public AudioClip winSound;
    public AudioClip failSound;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("กล้องสั่นตอนโดนกระแทก")]
    public float slamShakeAmplitude = 0.06f;
    public float slamShakeDuration = 0.25f;
    [Tooltip("บานประตูสั่นตอนโดนกระแทก (องศา)")]
    public float slamDoorRattle = 4f;

    [Header("Events")]
    public UnityEvent onStruggleStart;
    public UnityEvent onSlam;
    public UnityEvent onWin;
    [Tooltip("ประตูพัง — ใส่ jumpscare/คัตซีน Resonant คว้าตัวได้ตรงนี้")]
    public UnityEvent onFail;
    public UnityEvent onTutorialRetry;

    // ───────── สถานะ (UI อ่าน) ─────────
    public bool IsRunning { get; private set; }
    public bool IsTutorial { get; private set; }
    public float DoorPos { get; private set; }
    public float PushVelocity { get; private set; }
    /// <summary>เวลานับถอยหลังที่เหลือ (เต็ม = winHoldSeconds)</summary>
    public float WinTimerRemaining { get; private set; }
    public float WinProgress01 => winHoldSeconds > 0f ? 1f - WinTimerRemaining / winHoldSeconds : 0f;
    public bool InLockZone => DoorPos >= lockZoneMin;

    private Quaternion closedRotation;
    private float slamTimer;
    private float growlTimer;
    private float graceTimer;
    private float rattle;
    private bool ending;

    private bool doorControllerWasEnabled;
    private bool animatorWasEnabled;
    private Vector3 cameraBaseLocalPos;
    private Coroutine shakeRoutine;

    // ───────── Unity ─────────
    private void Awake()
    {
        if (doorController == null) doorController = GetComponentInParent<DoorController>();
        if (doorAnimator == null) doorAnimator = GetComponentInParent<Animator>();
        if (doorPanel == null) doorPanel = doorAnimator != null ? doorAnimator.transform : transform;
        closedRotation = doorPanel.rotation; // ตอนเริ่มเกมประตูต้องปิดอยู่ (ท่าในซีน = ท่าปิด)

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (struggleCamera != null)
        {
            if (struggleCamera.GetComponentInChildren<AudioListener>(true) == null)
                struggleCamera.AddComponent<AudioListener>(); // กล้องหลักถูกปิด → ต้องมีหูฟังที่กล้องนี้ ไม่งั้นเงียบ
            cameraBaseLocalPos = struggleCamera.transform.localPosition;
            struggleCamera.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (IsRunning) Abort();
    }

    // ───────── เริ่ม ─────────
    /// <summary>เริ่มแบบเกมจริง (เรียกจาก UnityEvent ได้)</summary>
    public void BeginNormal() => Begin(false);
    /// <summary>เริ่มแบบ Tutorial (เรียกจาก UnityEvent ได้)</summary>
    public void BeginTutorial() => Begin(true);

    public bool Begin(bool tutorial)
    {
        if (IsRunning || AnyActive) return false;
        if (doorPanel == null) { Debug.LogError("[DoorBarricadeStruggle] ยังไม่ได้ใส่ Door Panel", this); return false; }

        Active = this;
        IsRunning = true;
        IsTutorial = tutorial;
        ending = false;

        ResetRound();

        // ประตู: ห้ามกด E เปิด/ปิด และห้าม Animator แย่งหมุนบาน
        if (doorController != null) { doorControllerWasEnabled = doorController.enabled; doorController.enabled = false; }
        if (doorAnimator != null) { animatorWasEnabled = doorAnimator.enabled; doorAnimator.enabled = false; }

        // กล้อง/ผู้เล่น/เมาส์ (ล็อกเมาส์กลางจอ เพื่ออ่าน Mouse Y ได้ไม่สุดขอบจอ)
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null)
        {
            fp.EnterMode(struggleCamera, unlockCursor: false, lookController: null,
                         standPoint: playerStandPoint, returnToOriginalPosition: false);
        }
        else
        {
            Debug.LogWarning("[DoorBarricadeStruggle] ไม่พบ FirstPersonModeController — ล็อกเมาส์อย่างเดียว ไม่สลับกล้อง", this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        ApplyDoorVisual();
        DoorBarricadeStruggleUI.Ensure();

        onStruggleStart?.Invoke();
        OnAnyStarted?.Invoke(this);
        return true;
    }

    private void ResetRound()
    {
        DoorPos = doorStartPos;
        PushVelocity = 0f;
        WinTimerRemaining = winHoldSeconds;
        graceTimer = IsTutorial ? tutorialGraceSeconds : startGraceSeconds;
        slamTimer = NextSlamInterval();
        growlTimer = UnityEngine.Random.Range(growlIntervalRange.x, growlIntervalRange.y);
        rattle = 0f;
    }

    // ───────── ลูปหลัก ─────────
    private void Update()
    {
        if (!IsRunning || ending) return;

        bool paused = Time.timeScale == 0f || (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
        if (paused) return;

        // กลับจากเมนูหยุด → ล็อกเมาส์กลับ (ไม่งั้นเมาส์ชนขอบจอแล้วดันต่อไม่ได้)
        if (Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1) แรงผู้เล่น: นับเฉพาะลากขึ้น (ลากลง = ตั้งหลัก ไม่หักค่า)
        float mouseUp = Mathf.Max(0f, Input.GetAxisRaw("Mouse Y"));
        if (mouseUp > 0f)
        {
            float target = Mathf.Min(maxPushVelocity, (mouseUp / dt) * mousePushSensitivity);
            if (target > PushVelocity) PushVelocity = Mathf.MoveTowards(PushVelocity, target, pushAcceleration * dt);
            else PushVelocity *= Mathf.Exp(-pushInertiaDamping * dt);
        }
        else
        {
            PushVelocity *= Mathf.Exp(-pushInertiaDamping * dt); // แรงเฉื่อย: ค่อยๆ ตก ไม่หายทันที
        }

        // 2) แรงศัตรู
        float forceMul = IsTutorial ? tutorialForceMultiplier : 1f;
        float enemyForce = 0f;
        if (graceTimer > 0f) graceTimer -= dt;
        else
        {
            enemyForce = enemyBaseForce * forceMul;

            slamTimer -= dt;
            if (slamTimer <= 0f)
            {
                slamTimer = NextSlamInterval();
                Slam();
            }

            growlTimer -= dt;
            if (growlTimer <= 0f)
            {
                growlTimer = UnityEngine.Random.Range(growlIntervalRange.x, growlIntervalRange.y);
                PlayRandom(enemyGrowls, 0.8f);
            }
        }

        DoorPos = Mathf.Clamp(DoorPos + (PushVelocity - enemyForce) * dt, 0f, 100f);

        // 3) ชนะ: อยู่ในโซนล็อกครบเวลา · หลุดต่ำกว่าโซน = รีเซ็ตเวลา
        if (DoorPos >= lockZoneMin)
        {
            WinTimerRemaining -= dt;
            if (WinTimerRemaining <= 0f)
            {
                WinTimerRemaining = 0f;
                StartCoroutine(WinRoutine());
                return;
            }
        }
        else
        {
            WinTimerRemaining = winHoldSeconds;
        }

        // 4) แพ้
        if (DoorPos <= 0f)
        {
            if (IsTutorial && tutorialRetryOnFail) TutorialRetry();
            else StartCoroutine(FailRoutine());
            return;
        }

        rattle = Mathf.MoveTowards(rattle, 0f, 40f * dt);
        ApplyDoorVisual();
    }

    private float NextSlamInterval()
    {
        float t = UnityEngine.Random.Range(Mathf.Min(slamIntervalRange.x, slamIntervalRange.y), Mathf.Max(slamIntervalRange.x, slamIntervalRange.y));
        return IsTutorial ? t * tutorialSlamIntervalMultiplier : t;
    }

    private void Slam()
    {
        float impulse = UnityEngine.Random.Range(Mathf.Min(slamImpulseRange.x, slamImpulseRange.y), Mathf.Max(slamImpulseRange.x, slamImpulseRange.y));
        if (IsTutorial) impulse *= tutorialSlamMultiplier;

        DoorPos = Mathf.Max(0f, DoorPos - impulse);
        PushVelocity *= 0.5f; // แรงกระแทกทำให้แขนเสียจังหวะ
        rattle = slamDoorRattle;

        PlayRandom(slamSounds, 1f);
        ShakeCamera();

        onSlam?.Invoke();
        OnAnySlam?.Invoke(this);
    }

    private void TutorialRetry()
    {
        ResetRound();
        ShakeCamera();
        onTutorialRetry?.Invoke();
        OnAnyTutorialRetry?.Invoke(this);
    }

    // ───────── จบ ─────────
    private IEnumerator WinRoutine()
    {
        ending = true;
        safeUntil = Time.time + safeTimeReward;
        Play(winSound, 1f);

        // ดันบานปิดสนิท
        float start = DoorPos;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            DoorPos = Mathf.Lerp(start, 100f, t / 0.25f);
            rattle = 0f;
            ApplyDoorVisual();
            yield return null;
        }
        DoorPos = 100f;
        ApplyDoorVisual();

        yield return new WaitForSeconds(0.35f);

        Finish(Result.Win, closeDoor: true);
        onWin?.Invoke();
    }

    private IEnumerator FailRoutine()
    {
        ending = true;
        Play(failSound, 1f);

        // ประตูถูกกระชากเปิด
        float startAngle = 0f;
        float elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(startAngle, 1f, elapsed / 0.15f);
            doorPanel.rotation = Quaternion.AngleAxis(openAngle * (1f + a * 1.5f), hingeAxis) * closedRotation;
            yield return null;
        }
        ShakeCamera();

        onFail?.Invoke();

        if (failDelay > 0f) yield return new WaitForSeconds(failDelay);

        Finish(Result.Fail, closeDoor: false);

        if (killPlayerOnFail)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null && pm.CurrentHealth > 0) pm.TakeRawDamage(pm.CurrentHealth); // Instant Death → PlayerDeathWatcher/DeathHandler ทำต่อ
        }
    }

    /// <summary>ยกเลิกกลางคัน (เช่น ออกจากโลก PTSD / ปิดซีน) — คืนทุกอย่าง ไม่นับแพ้ชนะ</summary>
    public void Abort()
    {
        if (!IsRunning) return;
        StopAllCoroutines();
        Finish(Result.Aborted, closeDoor: false);
    }

    private void Finish(Result result, bool closeDoor)
    {
        IsRunning = false;
        ending = false;

        if (shakeRoutine != null) { StopCoroutine(shakeRoutine); shakeRoutine = null; }
        if (struggleCamera != null) struggleCamera.transform.localPosition = cameraBaseLocalPos;

        if (closeDoor && doorPanel != null) doorPanel.rotation = closedRotation;

        if (doorAnimator != null) doorAnimator.enabled = animatorWasEnabled;
        if (doorController != null)
        {
            doorController.enabled = doorControllerWasEnabled;
            // ประตูเคยเปิดค้าง (isOpen) → สั่งปิดผ่าน ToggleDoor เดิม (Animator + กล่องชนทึบ ตรงกัน) ไม่ต้องแก้ DoorController
            if (closeDoor && doorController.isOpen)
                doorController.SendMessage("ToggleDoor", SendMessageOptions.DontRequireReceiver);
        }

        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null) fp.ExitMode();

        if (Active == this) Active = null;

        Ended?.Invoke(this, result);
        OnAnyEnded?.Invoke(this, result);
    }

    // ───────── ภาพ/เสียง ─────────
    private void ApplyDoorVisual()
    {
        if (doorPanel == null) return;
        float angle = Mathf.Lerp(openAngle, 0f, DoorPos / 100f);
        angle += UnityEngine.Random.Range(-rattle, rattle);
        doorPanel.rotation = Quaternion.AngleAxis(angle, hingeAxis) * closedRotation;
    }

    private void ShakeCamera()
    {
        if (struggleCamera == null || slamShakeAmplitude <= 0f) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        Transform cam = struggleCamera.transform;
        for (float t = 0f; t < slamShakeDuration; t += Time.deltaTime)
        {
            float k = 1f - t / slamShakeDuration;
            cam.localPosition = cameraBaseLocalPos + UnityEngine.Random.insideUnitSphere * (slamShakeAmplitude * k);
            yield return null;
        }
        cam.localPosition = cameraBaseLocalPos;
        shakeRoutine = null;
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[UnityEngine.Random.Range(0, clips.Length)], volume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip, volume * sfxVolume);
    }

    private void OnDrawGizmosSelected()
    {
        if (playerStandPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerStandPoint.position, 0.3f);
            Gizmos.DrawLine(playerStandPoint.position, transform.position);
        }
    }
}
