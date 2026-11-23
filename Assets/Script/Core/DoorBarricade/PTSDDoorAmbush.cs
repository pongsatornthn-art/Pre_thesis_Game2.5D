using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// [2026-11-22] ผีดันประตูแบบสุ่ม ตอนอยู่ในโลก PTSD
/// เดินผ่านประตูบานนี้ (จากฝั่งหนึ่งไปอีกฝั่ง) ตอนอยู่ใน PTSD → สุ่มตามโอกาส → ถ้าโดน: ผีดันประตูตามหลังมา = เข้ามินิเกมดันประตู
///
/// วิธีใช้: แปะที่ตัวประตู (GameObject เดียวกับ DoorController) — แนะนำแปะใน prefab Door00 / Door01 ทีเดียว ประตูทุกบานได้หมด
///   ไม่ต้องทำจุดยืน/กล้องเอง — สร้างให้ตอนเกมรัน ตามฝั่งที่ผู้เล่นเดินเข้าไป (ผู้เล่นยืนฝั่งที่เดินเข้า · ผีดันมาจากฝั่งที่เดินมา)
///   ประตูที่มีมินิเกมดันประตูของตัวเองอยู่แล้ว (ประตูครัว Tutorial) → ข้ามให้เอง ไม่ซ้อน
///   ประตูล็อก → ไม่สุ่ม
/// </summary>
[DisallowMultipleComponent]
public class PTSDDoorAmbush : MonoBehaviour
{
    [Header("โอกาส")]
    [Tooltip("โอกาสโดนดันประตู ต่อการเดินผ่าน 1 ครั้ง (0.35 = 35%)")]
    [Range(0f, 1f)] public float chance = 0.35f;
    [Tooltip("หลังโดนดันไป 1 ครั้ง (ประตูไหนก็ได้) ต้องรออย่างน้อยกี่วินาที ถึงจะโดนอีก")]
    [Min(0f)] public float globalCooldown = 60f;
    [Tooltip("ประตูบานเดิมจะไม่สุ่มซ้ำภายในกี่วินาที (นับทั้งตอนโดนและไม่โดน)")]
    [Min(0f)] public float perDoorCooldown = 20f;
    [Tooltip("เพิ่งเข้า PTSD ไม่ถึงกี่วินาที → ยังไม่สุ่ม (ให้ตั้งตัวก่อน)")]
    [Min(0f)] public float ptsdGraceSeconds = 5f;
    [Tooltip("ระหว่างวิ่งหนีครั้งแรก (FirstChaseStalker ไล่อยู่) ไม่สุ่ม — ให้ไปเจอดันประตูครัว (Tutorial) อย่างเดียว")]
    public bool skipDuringFirstChase = true;

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
    [Tooltip("ติ๊ก = สุ่มติด 100% (ไว้เทสต์) — ตอนส่งงานปิด")]
    public bool debugAlwaysTrigger = false;

    private static float nextGlobalTime;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => nextGlobalTime = 0f;

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
    }

    private void Update()
    {
        bool inPtsd = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
        if (!inPtsd) ptsdSince = -1f;
        else if (ptsdSince < 0f) ptsdSince = Time.time;
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

    private void TryAmbush(int side, Transform player)
    {
        if (struggle == null || ptsdSince < 0f || Time.time - ptsdSince < ptsdGraceSeconds) return;
        if (Time.time < nextGlobalTime || Time.time < nextDoorTime) return;
        if (!door.isUnlocked) return;
        if (skipDuringFirstChase && firstChases != null)
            foreach (FirstChaseStalker fc in firstChases)
                if (fc != null && fc.IsChasing) return; // วิ่งหนีครั้งแรก → เก็บไว้ให้ประตูครัวอย่างเดียว
        if (DoorBarricadeStruggle.AnyActive || DoorBarricadeStruggle.SafeTimeRemaining > 0f) return;
        if (GameplayInputGate.Blocked) return;
        FirstPersonModeController fp = FirstPersonModeController.Instance;
        if (fp != null && fp.IsActive) return;

        nextDoorTime = Time.time + perDoorCooldown;
        if (!debugAlwaysTrigger && Random.value > chance) return;
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

        if (hidePlayer) PlayerVisibility.Hide(this);
        struggle.BeginNormal();
        if (!struggle.IsRunning) PlayerVisibility.Show(this); // เริ่มไม่ได้ (ระบบอื่นกันไว้) → คืนตัวผู้เล่น
    }

    private void HandleEnded(DoorBarricadeStruggle s, DoorBarricadeStruggle.Result r) => PlayerVisibility.Show(this);

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(doorCenter - doorNormal * standDistance, doorCenter + doorNormal * standDistance);
        Gizmos.DrawWireSphere(doorCenter, 0.15f);
    }
#endif
}