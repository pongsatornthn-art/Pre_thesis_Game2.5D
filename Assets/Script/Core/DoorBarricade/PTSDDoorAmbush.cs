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
    [Tooltip("ติ๊ก = ไม่ต้องรอบอสไล่ + ไม่สุ่ม (เดินเข้าห้องเมื่อไหร่โดนเลย) ไว้เทสต์ — ตอนส่งงานปิด")]
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
    private float nextDoorTime;
    private float ptsdSince = -1f;
    private Transform standPoint;
    private GameObject cam;
    private FirstChaseStalker[] firstChases;

    /// <summary>ฝั่งห้อง: +1 / -1 (ตามแนว doorNormal) · 0 = ห้องทั้งสองฝั่ง (เข้าทางไหนก็นับ) · 99 = ไม่มีฝั่งไหนเป็นห้อง (ไม่โดน)</summary>
    private int roomSide = 0;

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
        roomSide = FindRoomSide();

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
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !other.CompareTag("Player")) return;
        int exitSide = Side(other.transform.position);
        if (enterSide != 0 && exitSide != 0 && exitSide != enterSide) TryAmbush(exitSide, other.transform);
        enterSide = 0;
    }

    private int Side(Vector3 p)
    {
        float d = Vector3.Dot(p - doorCenter, doorNormal);
        return Mathf.Abs(d) < 0.1f ? 0 : (d > 0f ? 1 : -1);
    }

    /// <param name="side">ฝั่งที่ผู้เล่นเดินไปถึง (ฝั่งที่จะยืนดันประตู)</param>
    private void TryAmbush(int side, Transform player)
    {
        if (struggle == null || ptsdSince < 0f || Time.time - ptsdSince < ptsdGraceSeconds) return;

        // [2026-11-27] เฉพาะตอนเดินเข้าห้อง
        if (enterRule == EnterRule.AutoRoomSide)
        {
            if (roomSide == 99) { Log("ประตูนี้ไม่มีฝั่งไหนเป็นห้อง — ไม่โดน"); return; }
            if (roomSide != 0 && side != roomSide) { Log("เดินออกจากห้อง — ไม่โดน"); return; }
        }

        // [2026-11-27] เฉพาะตอนบอสไล่อยู่
        StalkerAI boss = null;
        if (onlyWhileBossChasing && !debugAlwaysTrigger)
        {
            boss = BossChasingNear(doorCenter);
            if (boss == null) { Log("เดินเข้าห้อง แต่บอสไม่ได้ไล่อยู่ (หรืออยู่ไกลเกิน) — ไม่โดน"); return; }
        }
        else boss = BossChasingNear(doorCenter);

        if (Time.time < nextGlobalTime || Time.time < nextDoorTime) { Log("ยังติดคูลดาวน์"); return; }
        if (!door.isUnlocked) return;
        if (skipDuringFirstChase && firstChases != null)
            foreach (FirstChaseStalker fc in firstChases)
                if (fc != null && fc.IsChasing) return; // วิ่งหนีครั้งแรก → เก็บไว้ให้ประตูครัวอย่างเดียว
        if (DoorBarricadeStruggle.AnyActive || DoorBarricadeStruggle.SafeTimeRemaining > 0f) return;
        if (StalkerJumpscareUI.AnyActive) return;   // บอสจับตัวอยู่แล้ว
        if (GameplayInputGate.Blocked) return;
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null && fp.IsActive) return;

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
    private int FindRoomSide()
    {
        if (roomBoxes == null) BuildRoomBoxes();

        float probe = standDistance + 0.3f;
        int a = RoomAt(doorCenter + doorNormal * probe);    // ฝั่ง +1
        int b = RoomAt(doorCenter - doorNormal * probe);    // ฝั่ง -1
        bool roomA = (a & 1) != 0 && (a & 2) == 0;
        bool roomB = (b & 1) != 0 && (b & 2) == 0;

        int result;
        if (roomA && !roomB) result = 1;
        else if (roomB && !roomA) result = -1;
        else if (roomA && roomB) result = 0;                // ห้องต่อห้อง → เข้าทางไหนก็นับ
        else if ((a & 2) == 0 && (b & 2) != 0) result = 1;  // ไม่มีกล่องห้อง แต่อีกฝั่งเป็นโถง → ฝั่งที่ไม่ใช่โถง
        else if ((b & 2) == 0 && (a & 2) != 0) result = -1;
        else result = 99;

        if (debugLog)
            Debug.Log($"[PTSDDoorAmbush] {door.name}: ฝั่ง+ = {Describe(a)} · ฝั่ง- = {Describe(b)} → " +
                      (result == 99 ? "ไม่มีห้อง (ไม่โดน)" : result == 0 ? "ห้องทั้งสองฝั่ง" : $"ห้องอยู่ฝั่ง {(result > 0 ? "+" : "-")}"), door);
        return result;
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
            Vector3 l = r.box.transform.InverseTransformPoint(p) - r.box.center;
            Vector3 h = r.box.size * 0.5f;
            if (Mathf.Abs(l.x) <= Mathf.Abs(h.x) && Mathf.Abs(l.z) <= Mathf.Abs(h.z))
                m |= r.hall ? 2 : 1;
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

    private void Log(string msg)
    {
        if (debugLog) Debug.Log($"[PTSDDoorAmbush] {(door != null ? door.name : name)}: {msg}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(doorCenter - doorNormal * standDistance, doorCenter + doorNormal * standDistance);
        Gizmos.DrawWireSphere(doorCenter, 0.15f);
        // ฝั่งห้อง = ลูกบอลเขียว
        if (roomSide == 1 || roomSide == 0) { Gizmos.color = Color.green; Gizmos.DrawSphere(doorCenter + doorNormal * standDistance, 0.25f); }
        if (roomSide == -1 || roomSide == 0) { Gizmos.color = Color.green; Gizmos.DrawSphere(doorCenter - doorNormal * standDistance, 0.25f); }
        if (roomSide == 99) { Gizmos.color = Color.red; Gizmos.DrawSphere(doorCenter, 0.3f); }
    }
#endif
}
