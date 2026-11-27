using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// [2026-11-22] ผีดันประตู ตอนอยู่ในโลก PTSD → เข้ามินิเกมดันประตู
/// [2026-11-27 ปอ] เปลี่ยนเงื่อนไข:
///   1) เฉพาะตอน "บอส (Stalker) กำลังไล่" ผู้เล่นอยู่เท่านั้น — เดินเล่นปกติไม่โดน
///   2) เฉพาะตอน "เดินเข้าห้อง" — เดินออกจากห้องไปโถงไม่โดน (เดิมเดินออกแล้วต้องดันประตู ดูแปลก)
///      ฝั่งไหนคือห้อง: ดูจาก Trigger ห้อง (RoomWallFade) ที่มีอยู่แล้ว — กล่องยาว/อยู่ใต้ Room_Trigger_Main = โถง
///   3) ระหว่างดันประตู บอสหยุด + ไปยืนหลังประตู (ฝั่งโถง) · จบแล้วบอสลืมเรา วาร์ปไปไกลๆ แล้วกลับไปเดินล่า
///
/// วิธีใช้: แปะที่ตัวประตู (GameObject เดียวกับ DoorController) — อยู่ใน prefab Door01 แล้ว ประตูทุกบานได้หมด
///   ไม่ต้องทำจุดยืน/กล้องเอง — สร้างให้ตอนเกมรัน
///   ประตูที่มีมินิเกมดันประตูของตัวเองอยู่แล้ว (ประตูครัว Tutorial) → ข้ามให้เอง ไม่ซ้อน
///   ประตูล็อก → ไม่โดน
/// </summary>
[DisallowMultipleComponent]
public class PTSDDoorAmbush : MonoBehaviour
{
    public enum EnterRule
    {
        [Tooltip("หาเองว่าฝั่งไหนคือห้อง — โดนเฉพาะตอนเดินเข้าห้อง")]
        AutoRoomSide,
        [Tooltip("โดนทั้งเข้าและออก (แบบเดิม)")]
        AnyDirection
    }

    [Header("เงื่อนไข (2026-11-27)")]
    [Tooltip("AutoRoomSide = โดนเฉพาะตอนเดินเข้าห้อง (หาฝั่งห้องเองจาก Trigger ห้อง)\nAnyDirection = เข้า/ออกก็โดน (แบบเดิม)")]
    public EnterRule enterRule = EnterRule.AutoRoomSide;
    [Tooltip("ติ๊ก = โดนเฉพาะตอนบอส (Stalker) กำลังไล่ผู้เล่นอยู่")]
    public bool onlyWhileBossChasing = true;
    [Tooltip("บอสเพิ่งเลิกไล่ไม่เกินกี่วินาที ยังนับว่า 'ไล่อยู่' (เผื่อบอสคลาดสายตาตอนเราวิ่งเข้าห้องพอดี)")]
    [Min(0f)] public float chaseGraceSeconds = 2f;
    [Tooltip("บอสต้องอยู่ห่างประตูไม่เกินกี่เมตร (ไกลกว่านี้ = ยังตามมาไม่ทัน ไม่ดันประตู)")]
    [Min(1f)] public float maxBossDistance = 25f;
    [Tooltip("โอกาสโดนดันประตู ตอนเดินเข้าห้องระหว่างโดนบอสไล่ (1 = โดนทุกครั้ง)")]
    [Range(0f, 1f)] public float chanceWhileChased = 1f;

    [Header("คูลดาวน์")]
    [Tooltip("หลังโดนดันไป 1 ครั้ง (ประตูไหนก็ได้) ต้องรออย่างน้อยกี่วินาที ถึงจะโดนอีก")]
    [Min(0f)] public float globalCooldown = 60f;
    [Tooltip("ประตูบานเดิมจะไม่สุ่มซ้ำภายในกี่วินาที (นับทั้งตอนโดนและไม่โดน)")]
    [Min(0f)] public float perDoorCooldown = 20f;
    [Tooltip("เพิ่งเข้า PTSD ไม่ถึงกี่วินาที → ยังไม่โดน (ให้ตั้งตัวก่อน)")]
    [Min(0f)] public float ptsdGraceSeconds = 5f;
    [Tooltip("ระหว่างวิ่งหนีครั้งแรก (FirstChaseStalker ไล่อยู่) ไม่โดน — ให้ไปเจอดันประตูครัว (Tutorial) อย่างเดียว")]
    public bool skipDuringFirstChase = true;

    [Header("บอสระหว่างดันประตู")]
    [Tooltip("ย้ายบอสไปยืนหลังประตู (ฝั่งที่ผู้เล่นเดินมา) ระหว่างดัน")]
    public bool placeBossBehindDoor = true;
    [Tooltip("จบมินิเกมแล้ว บอสวาร์ปไปห่างผู้เล่นกี่เมตร แล้วกลับไปเดินล่า (0 = ไม่วาร์ป)")]
    [Min(0f)] public float bossRetreatDistance = 15f;

    [Header("จุดยืน / กล้อง (สร้างให้เอง)")]
    [Tooltip("ผู้เล่นยืนห่างประตูกี่เมตร (ฝั่งที่เดินเข้าไป)")]
    [Min(0.3f)] public float standDistance = 1.2f;
    [Tooltip("ความสูงกล้อง (ระดับตา) จากพื้น")]
    public float cameraHeight = 1.5f;
    [Tooltip("กล้องถอยหลังจากจุดยืนกี่เมตร (มากขึ้น = เห็นประตูทั้งบาน)")]
    public float cameraBack = 0.3f;
    [Tooltip("มุมกว้างกล้อง")]
    [Range(30f, 100f)] public float cameraFov = 65f;

    [Header("มินิเกม")]
    [Tooltip("แพ้แล้วตาย (ปิด = แพ้แล้วแค่ประตูเปิด ไม่ตาย)")]
    public bool killPlayerOnFail = true;
    [Tooltip("ซ่อนตัวผู้เล่นระหว่างดันประตู (กล้องอยู่ข้างหลังตัวละคร ไม่ให้บังประตู)")]
    public bool hidePlayer = true;

    [Header("Debug")]
    [Tooltip("ติ๊ก = ไม่ต้องรอบอสไล่ + ไม่สุ่ม + ไม่มีคูลดาวน์ (เดินเข้าห้องใน PTSD เมื่อไหร่โดนเลย) ไว้เทสต์ — ตอนส่งงานปิด\nติ๊กอันนี้แล้ว Console จะบอกเหตุผลทุกครั้งที่ไม่โดน")]
    public bool debugAlwaysTrigger = false;
    [Tooltip("ติ๊ก = เขียนใน Console ว่าประตูนี้ฝั่งไหนคือห้อง / ทำไมไม่โดน")]
    public bool debugLog = false;

    // ───────── สถานะรวม (ทุกประตูใช้ร่วมกัน) ─────────
    private static float nextGlobalTime;
    private static StalkerAI[] bosses;
    private static float bossesRefreshAt;
    private static int lastScanFrame = -1;
    private static float lastChaseTime = -999f;
    private static StalkerAI chasingBoss;
    private static List<RoomBox> roomBoxes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        nextGlobalTime = 0f;
        bosses = null; bossesRefreshAt = 0f; lastScanFrame = -1;
        lastChaseTime = -999f; chasingBoss = null;
        roomBoxes = null;
    }

    private struct RoomBox { public BoxCollider box; public bool hall; }

    private DoorController door;
    private DoorBarricadeStruggle struggle;
    private Vector3 doorCenter;
    private Vector3 doorNormal;
    private int enterSide;
    private Vector3 enterPos;
    private float nextDoorTime;
    private float ptsdSince = -1f;
    private Transform standPoint;
    private GameObject cam;
    private FirstChaseStalker[] firstChases;

    /// <summary>[v4] ทิศจากประตูไปฝั่งห้อง (โลก) · roomKind: 1 = รู้ฝั่งห้อง · 0 = ห้องทั้งสองฝั่ง · 99 = หาไม่เจอ</summary>
    private Vector3 roomDir;
    private int roomKind = 99;

    // บอสที่หยุดไว้ระหว่างดันประตู
    private StalkerAI pausedBoss;
    private bool pausedBossWasEnabled;

    private void Start()
    {
        door = GetComponent<DoorController>();
        if (door == null) door = GetComponentInParent<DoorController>();
        if (door == null) { Debug.LogWarning($"[PTSDDoorAmbush] {name}: ไม่เจอ DoorController — ปิดตัวเอง", this); enabled = false; return; }

        // ประตูที่มีมินิเกมดันประตูของตัวเองอยู่แล้ว (เช่น ครัว Tutorial) → ไม่ยุ่ง
        if (door.GetComponentInChildren<DoorBarricadeStruggle>(true) != null || door.GetComponentInParent<DoorBarricadeStruggle>() != null)
        {
            enabled = false;
            return;
        }

        firstChases = FindObjectsByType<FirstChaseStalker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CacheGeometry(); // ตอนเริ่มฉากประตูปิดอยู่ → จำกลางบานและแนวประตูไว้
        FindRoomDir();

        // มินิเกมใช้ตัวเดิมของระบบดันประตู — เพิ่มลงประตูนี้ (Awake หา Animator/DoorController เอง)
        struggle = door.gameObject.AddComponent<DoorBarricadeStruggle>();
        struggle.killPlayerOnFail = killPlayerOnFail;
        struggle.Ended += HandleEnded;

        // จุดยืน + กล้อง: ไว้นอกตัวประตู (ตัวประตูหมุนตอนเปิด → ลูกจะหมุนตาม)
        standPoint = new GameObject($"AmbushStand_{door.name}").transform;
        cam = new GameObject($"AmbushCam_{door.name}");
        Camera c = cam.AddComponent<Camera>();
        if (Camera.main != null) c.CopyFrom(Camera.main);
        c.orthographic = false;
        c.fieldOfView = cameraFov;
        c.nearClipPlane = 0.05f;
        c.targetTexture = null;
        UniversalAdditionalCameraData data = c.GetUniversalAdditionalCameraData();
        if (data != null) { data.renderPostProcessing = true; data.renderType = CameraRenderType.Base; }
        cam.SetActive(false);
    }

    private void OnDestroy()
    {
        if (struggle != null) struggle.Ended -= HandleEnded;
        if (standPoint != null) Destroy(standPoint.gameObject);
        if (cam != null) Destroy(cam);
        PlayerVisibility.Show(this);
        ResumeBoss(false);
    }

    private void Update()
    {
        bool inPtsd = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
        if (!inPtsd) ptsdSince = -1f;
        else if (ptsdSince < 0f) ptsdSince = Time.time;

        ScanBosses();
    }

    // ───────── จับว่าผู้เล่นเดินผ่านประตู (ใช้ Trigger ตัวเดียวกับป้าย [E] ของประตู) ─────────
    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !other.CompareTag("Player")) return;
        enterSide = Side(other.transform.position);
        enterPos = other.transform.position;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !other.CompareTag("Player")) return;
        int exitSide = Side(other.transform.position);
        if (enterSide != 0 && exitSide != 0 && exitSide != enterSide) TryAmbush(exitSide, other.transform, enterPos);
        else if (Verbose) Log($"เดินเข้า-ออกฝั่งเดิม ไม่ได้ผ่านประตู (เข้า {enterSide} ออก {exitSide})");
        enterSide = 0;
    }

    private int Side(Vector3 p)
    {
        float d = Vector3.Dot(p - doorCenter, doorNormal);
        return Mathf.Abs(d) < 0.1f ? 0 : (d > 0f ? 1 : -1);
    }

    /// <param name="side">ฝั่งที่ผู้เล่นเดินไปถึง (ฝั่งที่จะยืนดันประตู)</param>
    private void TryAmbush(int side, Transform player, Vector3 fromPos)
    {
        if (struggle == null) return;
        if (ptsdSince < 0f) { Log("ไม่ได้อยู่ในโลก PTSD — ไม่โดน"); return; }
        if (Time.time - ptsdSince < ptsdGraceSeconds) { Log($"เพิ่งเข้า PTSD ยังไม่ครบ {ptsdGraceSeconds} วิ — ไม่โดน"); return; }

        // [2026-11-27] เฉพาะตอนเดินเข้าห้อง
        if (enterRule == EnterRule.AutoRoomSide)
        {
            int entering = IsEnteringRoom(fromPos, player.position, out string how);   // 1 เข้าห้อง · 0 ออกจากห้อง · -1 บอกไม่ได้
            if (entering == 0) { Log($"เดินออกจากห้อง — ไม่โดน ({how})"); return; }
            if (entering < 0)
            {
                Log($"ประตูนี้บอกไม่ได้ว่าฝั่งไหนเป็นห้อง — ไม่โดน (เปลี่ยน Enter Rule เป็น AnyDirection ได้)\n{RoomDebug(fromPos, player.position)}");
                return;
            }
            Log($"เดินเข้าห้อง ({how})");
        }

        // [2026-11-27] เฉพาะตอนบอสไล่อยู่
        StalkerAI boss = null;
        if (onlyWhileBossChasing && !debugAlwaysTrigger)
        {
            boss = BossChasingNear(doorCenter);
            if (boss == null) { Log($"เดินเข้าห้อง แต่บอสไม่ได้ไล่อยู่ — ไม่โดน ({BossStatus()})"); return; }
        }
        else boss = BossChasingNear(doorCenter);

        if (!debugAlwaysTrigger)
        {
            if (Time.time < nextGlobalTime) { Log($"ยังติดคูลดาวน์รวม อีก {nextGlobalTime - Time.time:0} วิ"); return; }
            if (Time.time < nextDoorTime) { Log($"ประตูบานนี้ยังติดคูลดาวน์ อีก {nextDoorTime - Time.time:0} วิ"); return; }
            if (DoorBarricadeStruggle.SafeTimeRemaining > 0f) { Log($"ยังอยู่ในเวลาปลอดภัยหลังชนะรอบก่อน อีก {DoorBarricadeStruggle.SafeTimeRemaining:0} วิ"); return; }
        }
        if (!door.isUnlocked) { Log("ประตูล็อกอยู่ — ไม่โดน"); return; }
        if (skipDuringFirstChase && firstChases != null)
            foreach (FirstChaseStalker fc in firstChases)
                if (fc != null && fc.IsChasing) { Log("ช่วงวิ่งหนีครั้งแรก (Tutorial) — ไม่โดน"); return; } // เก็บไว้ให้ประตูครัวอย่างเดียว
        if (DoorBarricadeStruggle.AnyActive) { Log("มีมินิเกมดันประตูเล่นอยู่แล้ว"); return; }
        if (StalkerJumpscareUI.AnyActive) { Log("บอสจับตัวอยู่ (มินิเกมมือผี)"); return; }
        if (GameplayInputGate.Blocked) { Log("มีหน้าจอ/มินิเกมอื่นเปิดอยู่ (GameplayInputGate)"); return; }
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null && fp.IsActive) { Log("อยู่ในโหมดกล้องบุคคลที่หนึ่งอยู่แล้ว"); return; }

        nextDoorTime = Time.time + perDoorCooldown;
        if (!debugAlwaysTrigger && Random.value > chanceWhileChased) { Log("สุ่มไม่โดน"); return; }
        nextGlobalTime = Time.time + globalCooldown;

        // จุดยืน: ฝั่งที่ผู้เล่นเพิ่งเดินเข้าไป หันหน้าเข้าหาประตู
        Vector3 n = doorNormal * side;
        Vector3 floorCenter = new Vector3(doorCenter.x, player.position.y, doorCenter.z);
        Vector3 stand = floorCenter + n * standDistance;
        standPoint.SetPositionAndRotation(stand, Quaternion.LookRotation(-n, Vector3.up));

        Vector3 camPos = stand + n * cameraBack + Vector3.up * cameraHeight;
        Vector3 lookAt = new Vector3(doorCenter.x, Mathf.Lerp(player.position.y, doorCenter.y, 0.5f) + cameraHeight * 0.45f, doorCenter.z);
        cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookAt - camPos, Vector3.up));
        cam.GetComponent<Camera>().fieldOfView = cameraFov;

        struggle.playerStandPoint = standPoint;
        struggle.SetupCamera(cam); // จำตำแหน่งกล้องใหม่ไว้สั่นตอนผีกระแทก
        struggle.killPlayerOnFail = killPlayerOnFail;

        PauseBoss(boss, -n);

        if (hidePlayer) PlayerVisibility.Hide(this);
        struggle.BeginNormal();
        if (!struggle.IsRunning)
        {
            // เริ่มไม่ได้ (ระบบอื่นกันไว้) → คืนทุกอย่าง
            Log("สั่งเริ่มมินิเกมแล้วแต่เริ่มไม่ได้ (ดู Error สีแดงของ DoorBarricadeStruggle ใน Console)");
            PlayerVisibility.Show(this);
            ResumeBoss(false);
            return;
        }
        Log($"โดนดันประตู! (บอส: {(boss != null ? boss.name : "ไม่มี")})");
    }

    private void HandleEnded(DoorBarricadeStruggle s, DoorBarricadeStruggle.Result r)
    {
        PlayerVisibility.Show(this);
        ResumeBoss(true);
    }

    // ───────── บอส ─────────
    /// <summary>ดูทุกเฟรม (ครั้งเดียวต่อเฟรม ไม่ว่าประตูกี่บาน) ว่ามีบอสกำลังไล่อยู่ไหม</summary>
    private static void ScanBosses()
    {
        if (Time.frameCount == lastScanFrame) return;
        lastScanFrame = Time.frameCount;

        if (bosses == null || Time.time >= bossesRefreshAt)
        {
            bosses = FindObjectsByType<StalkerAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bossesRefreshAt = Time.time + 2f;
        }
        foreach (StalkerAI b in bosses)
        {
            if (b == null || !b.isActiveAndEnabled) continue;
            IStalkerState st = b.CurrentState;
            if (st is StalkerStateChase || st is StalkerStateScream)
            {
                lastChaseTime = Time.time;
                chasingBoss = b;
            }
        }
    }

    private StalkerAI BossChasingNear(Vector3 pos)
    {
        ScanBosses();
        if (chasingBoss == null || !chasingBoss.gameObject.activeInHierarchy) return null;
        if (Time.time - lastChaseTime > chaseGraceSeconds) return null;
        Vector3 d = chasingBoss.transform.position - pos; d.y = 0f;
        return d.magnitude <= maxBossDistance ? chasingBoss : null;
    }

    private string BossStatus()
    {
        ScanBosses();
        if (bosses == null || bosses.Length == 0) return "ไม่เจอบอส (Stalker) ที่เปิดอยู่ในซีน";
        var sb = new System.Text.StringBuilder();
        foreach (StalkerAI b in bosses)
        {
            if (b == null) continue;
            Vector3 d = b.transform.position - doorCenter; d.y = 0f;
            string st = b.CurrentState != null ? b.CurrentState.GetType().Name : "ไม่มี";
            sb.Append($"{b.name}: state={st} ห่างประตู {d.magnitude:0.0} ม.{(b.enabled ? "" : " (ปิดอยู่)")} · ");
        }
        sb.Append($"ไล่ล่าสุดเมื่อ {Mathf.Max(0f, Time.time - lastChaseTime):0.0} วิก่อน");
        return sb.ToString();
    }

    /// <summary>หยุดบอสระหว่างดันประตู (ไม่งั้นบอสวิ่งทะลุประตูมาจับซ้อนมินิเกม) + ย้ายไปยืนหลังประตู</summary>
    private void PauseBoss(StalkerAI boss, Vector3 behindDir)
    {
        if (boss == null) return;
        pausedBoss = boss;
        pausedBossWasEnabled = boss.enabled;

        NavMeshAgent agent = boss.Agent;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            if (placeBossBehindDoor)
            {
                Vector3 p = new Vector3(doorCenter.x, boss.transform.position.y, doorCenter.z) + behindDir * 1.3f;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
                Vector3 face = -behindDir; face.y = 0f;
                if (face.sqrMagnitude > 0.01f) boss.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
            }
        }
        if (boss.chaseRunLoop != null) boss.chaseRunLoop.Stop();
        boss.enabled = false;   // หยุด Update ของบอส (ไม่ไล่/ไม่จับ)
    }

    /// <summary>คืนบอส: ลืมผู้เล่น → วาร์ปไปไกลๆ → กลับไปเดินล่า</summary>
    private void ResumeBoss(bool retreat)
    {
        StalkerAI boss = pausedBoss;
        pausedBoss = null;
        if (boss == null) return;

        boss.enabled = pausedBossWasEnabled;
        if (!boss.gameObject.activeInHierarchy) return;

        if (retreat)
        {
            boss.Awareness?.ForceZeroAwareness();
            boss.lastContactTime = Time.time;   // เพิ่งเจอกัน → บอสเดินสุ่มทั่วบ้านก่อน ให้ผู้เล่นได้หายใจ
            if (bossRetreatDistance > 0f) WarpAway(boss);
        }
        NavMeshAgent agent = boss.Agent;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;
        if (retreat) boss.ChangeState(new StalkerStateLurk());
        if (chasingBoss == boss) lastChaseTime = -999f;
    }

    private void WarpAway(StalkerAI boss)
    {
        NavMeshAgent agent = boss.Agent;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        Vector3 center = boss.PlayerTransform != null ? boss.PlayerTransform.position : doorCenter;
        for (int i = 0; i < 15; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * bossRetreatDistance;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas)) continue;
            NavMeshPath path = new NavMeshPath();
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                agent.Warp(hit.position);
                return;
            }
        }
    }

    // ───────── หาฝั่งห้อง ─────────
    /// <summary>
    /// [v4] เดินเข้าห้องไหม — ลองทีละวิธี จนกว่าจะบอกได้:
    ///   1) จุดก่อน/หลังประตู ตามทิศที่ผู้เล่นเดินจริง (2 ม.) อยู่ในกล่องห้อง (RoomWallFade) ไหน
    ///   2) ตำแหน่งผู้เล่นตอนเข้า/ออก Trigger ประตู
    ///   3) ฝั่งห้องที่หาไว้ตอนเริ่มฉาก (ลองหลายแกน)
    ///   4) วัดที่ว่างด้วย Raycast: ฝั่งที่แคบกว่า (มีผนังใกล้กว่า) = ห้อง · ฝั่งที่โล่งยาว = โถง
    /// </summary>
    private int IsEnteringRoom(Vector3 from, Vector3 to, out string how)
    {
        if (roomBoxes == null) BuildRoomBoxes();

        Vector3 dir = to - from; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.ProjectOnPlane(doorNormal, Vector3.up) * Mathf.Sign(Vector3.Dot(to - doorCenter, doorNormal) + 0.0001f);
        dir.Normalize();

        Vector3 pFrom = doorCenter - dir * 2f, pTo = doorCenter + dir * 2f;
        pFrom.y = pTo.y = to.y;

        int r = EnteringRoom(pFrom, pTo);
        if (r >= 0) { how = "จุดก่อน/หลังประตู"; return r; }
        r = EnteringRoom(from, to);
        if (r >= 0) { how = "ตำแหน่งผู้เล่น"; return r; }
        if (roomKind == 0) { how = "ห้องทั้งสองฝั่ง"; return 1; }
        if (roomKind == 1) { how = "ฝั่งห้องที่หาไว้ตอนเริ่ม"; return Vector3.Dot(to - doorCenter, roomDir) > 0f ? 1 : 0; }
        r = EnteringByOpenSpace(pFrom, pTo, dir);
        how = "วัดที่ว่าง (Raycast)";
        return r;
    }

    /// <summary>หาฝั่งห้องตอนเริ่มฉาก — ลองแกนจากรูปทรงประตูก่อน แล้วค่อยลองแกนของตัวประตู</summary>
    private void FindRoomDir()
    {
        if (roomBoxes == null) BuildRoomBoxes();
        roomKind = 99;
        roomDir = Vector3.zero;

        Vector3[] axes = { doorNormal, door.transform.forward, door.transform.right };
        string tried = "";
        for (int i = 0; i < axes.Length; i++)
        {
            Vector3 ax = Vector3.ProjectOnPlane(axes[i], Vector3.up);
            if (ax.sqrMagnitude < 0.01f) continue;
            ax.Normalize();
            foreach (float probe in new[] { standDistance + 0.3f, 2.5f })
            {
                int a = RoomAt(doorCenter + ax * probe);
                int b = RoomAt(doorCenter - ax * probe);
                tried += $"[{Describe(a)}|{Describe(b)}] ";
                bool roomA = (a & 1) != 0 && (a & 2) == 0;
                bool roomB = (b & 1) != 0 && (b & 2) == 0;
                bool hallA = (a & 2) != 0, hallB = (b & 2) != 0;

                if (roomA && !roomB) { roomKind = 1; roomDir = ax; }
                else if (roomB && !roomA) { roomKind = 1; roomDir = -ax; }
                else if (roomA && roomB && i == 0) { roomKind = 0; }   // ห้องต่อห้อง (เชื่อเฉพาะแกนหน้าประตู)
                else if (!hallA && hallB && a == 0) { roomKind = 1; roomDir = ax; }    // ไม่มีกล่องห้อง แต่อีกฝั่งเป็นโถง
                else if (!hallB && hallA && b == 0) { roomKind = 1; roomDir = -ax; }
                if (roomKind != 99) break;
            }
            if (roomKind != 99) break;
        }

        if (Verbose)
            Debug.Log($"[PTSDDoorAmbush] {door.name}: " +
                      (roomKind == 99 ? "หาฝั่งห้องตอนเริ่มไม่เจอ (จะลองดูตอนผู้เล่นเดินผ่านแทน)" : roomKind == 0 ? "ห้องทั้งสองฝั่ง" : "เจอฝั่งห้องแล้ว") +
                      $" · กล่องห้องในซีน {RoomCount(false)} · กล่องโถง {RoomCount(true)} · ที่ลอง {tried}", door);
    }

    /// <summary>วัดที่ว่างซ้าย-ขวาของจุดก่อน/หลังประตู: ห้อง = ผนังใกล้ (แคบ) · โถง = โล่งยาว</summary>
    private int EnteringByOpenSpace(Vector3 pFrom, Vector3 pTo, Vector3 dir)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
        float runFrom = FreeRun(pFrom, side) + FreeRun(pFrom, -side);
        float runTo = FreeRun(pTo, side) + FreeRun(pTo, -side);
        if (runTo + 3f < runFrom) return 1;    // ฝั่งที่ไปแคบกว่า = เข้าห้อง
        if (runFrom + 3f < runTo) return 0;    // ฝั่งที่ไปโล่งกว่า = ออกไปโถง
        return -1;
    }

    private static float FreeRun(Vector3 p, Vector3 dir)
    {
        const float max = 40f;
        Vector3 o = p + Vector3.up * 1.0f;
        RaycastHit[] hits = Physics.RaycastAll(o, dir, max, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;   // ผู้เล่น/ของที่ขยับได้ ไม่นับ
            if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;          // มอน ไม่นับ
            return Mathf.Min(max, h.distance);
        }
        return max;
    }

    private static int RoomCount(bool hall)
    {
        int n = 0;
        if (roomBoxes != null) foreach (RoomBox r in roomBoxes) if (r.box != null && r.hall == hall) n++;
        return n;
    }

    private string RoomDebug(Vector3 from, Vector3 to)
    {
        return $"  ตอนเข้า {from} = {Describe(RoomAt(from))} · ตอนออก {to} = {Describe(RoomAt(to))}\n" +
               $"  กลางประตู {doorCenter} แนวหน้าประตู {doorNormal} · กล่องห้องในซีน {RoomCount(false)} · กล่องโถง {RoomCount(true)}";
    }

    /// <summary>
    /// จุด from → to: ไปอยู่ในห้องที่ตอนแรกยังไม่ได้อยู่ = เดินเข้าห้อง
    /// 1 = เข้าห้อง · 0 = ออกจากห้อง · -1 = บอกไม่ได้ (กล่องห้องไม่ครอบตำแหน่ง)
    /// </summary>
    private static int EnteringRoom(Vector3 from, Vector3 to)
    {
        if (roomBoxes == null) BuildRoomBoxes();
        bool newRoom = false, inAnyRoomFrom = false, inAnyRoomTo = false;
        foreach (RoomBox r in roomBoxes)
        {
            if (r.box == null || r.hall) continue;
            bool a = Inside(r.box, from), b = Inside(r.box, to);
            inAnyRoomFrom |= a; inAnyRoomTo |= b;
            if (b && !a) newRoom = true;
        }
        if (newRoom) return 1;
        if (inAnyRoomFrom && !inAnyRoomTo) return 0;
        return -1;
    }

    private static bool Inside(BoxCollider box, Vector3 p)
    {
        Vector3 l = box.transform.InverseTransformPoint(p) - box.center;
        Vector3 h = box.size * 0.5f;
        return Mathf.Abs(l.x) <= Mathf.Abs(h.x) && Mathf.Abs(l.z) <= Mathf.Abs(h.z);
    }

    private static string Describe(int m) => m == 0 ? "ไม่มีกล่อง" : (m & 3) == 3 ? "ห้อง+โถง" : (m & 1) != 0 ? "ห้อง" : "โถง";

    private static void BuildRoomBoxes()
    {
        roomBoxes = new List<RoomBox>();
        foreach (RoomWallFade f in FindObjectsByType<RoomWallFade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (BoxCollider bc in f.GetComponents<BoxCollider>())
            {
                if (!bc.enabled) continue;
                Vector3 s = Vector3.Scale(bc.size, f.transform.lossyScale);
                s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                if (s.x * s.z < 4f) continue;   // ช่องเล็กๆ (ประตู/ซอก) ไม่นับ
                // เกณฑ์เดียวกับเครื่องมือเติมเสียงอัตโนมัติ: ยาวกว่ากว้าง 3 เท่า หรืออยู่ใต้ ...Main = โถง
                bool hall = Mathf.Max(s.x, s.z) / Mathf.Max(0.1f, Mathf.Min(s.x, s.z)) > 3f
                            || (f.transform.parent != null && f.transform.parent.name.Contains("Main"));
                roomBoxes.Add(new RoomBox { box = bc, hall = hall });
            }
        }
    }

    /// <summary>bit 1 = อยู่ในกล่องห้อง · bit 2 = อยู่ในกล่องโถง</summary>
    private static int RoomAt(Vector3 p)
    {
        int m = 0;
        foreach (RoomBox r in roomBoxes)
        {
            if (r.box == null) continue;
            if (Inside(r.box, p)) m |= r.hall ? 2 : 1;
        }
        return m;
    }

    // ───────── หาแนวประตู ─────────
    private void CacheGeometry()
    {
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        Vector3 hinge = door.transform.position;
        doorCenter = any ? b.center : hinge;

        // แนวบาน = จากบานพับไปกลางบาน · หน้าประตู = ตั้งฉากกับแนวบาน
        Vector3 leaf = Vector3.ProjectOnPlane(doorCenter - hinge, Vector3.up);
        if (leaf.sqrMagnitude < 0.01f)
        {
            // บานพับอยู่กลางบาน → ใช้ด้านยาวของกรอบแทน
            leaf = any && b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
        }
        doorNormal = Vector3.Cross(Vector3.up, leaf.normalized).normalized;
    }

    private bool Verbose => debugLog || debugAlwaysTrigger;

    private void Log(string msg)
    {
        if (Verbose) Debug.Log($"[PTSDDoorAmbush] {(door != null ? door.name : name)}: {msg}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(doorCenter - doorNormal * standDistance, doorCenter + doorNormal * standDistance);
        Gizmos.DrawWireSphere(doorCenter, 0.15f);
        // ฝั่งห้อง = ลูกบอลเขียว · หาไม่เจอ = ลูกบอลแดง
        if (roomKind == 1) { Gizmos.color = Color.green; Gizmos.DrawSphere(doorCenter + roomDir * standDistance, 0.25f); }
        else if (roomKind == 0) { Gizmos.color = Color.green; Gizmos.DrawSphere(doorCenter + doorNormal * standDistance, 0.25f); Gizmos.DrawSphere(doorCenter - doorNormal * standDistance, 0.25f); }
        else { Gizmos.color = Color.red; Gizmos.DrawSphere(doorCenter, 0.3f); }
    }
#endif
}
