using UnityEngine;

/// <summary>
/// [ข้อ 1] โซนเริ่ม Door Barricade Struggle — วางเป็นกล่อง Trigger หน้าประตู
///
/// โหมด:
///   PlayerEntersZone  = ผู้เล่นเดินเข้ากล่อง → เริ่มเลย (ใช้กับ Tutorial ฉากวิ่งหนีแรก)
///   EnemyReachesDoor  = ผู้เล่นอยู่ในกล่อง (ในห้อง) แล้วศัตรูวิ่งตามมาถึงประตูภายในระยะ → เริ่ม (GDD: การไล่ล่าและหนีเข้าห้อง)
///
/// ระหว่างยันประตู: ปิดสคริปต์ศัตรูชั่วคราว (ไม่ให้วิ่งมาจับ) + ย้ายศัตรูไปยืนหลังประตู
/// ชนะ: เปิดสคริปต์คืน (+ ย้ายศัตรูไปจุดล่าถอยถ้าตั้งไว้)
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorBarricadeTrigger : MonoBehaviour
{
    public enum StartWhen { PlayerEntersZone, EnemyReachesDoor }

    [Header("ประตู")]
    public DoorBarricadeStruggle struggle;
    public StartWhen startWhen = StartWhen.PlayerEntersZone;
    [Tooltip("เล่นแบบ Tutorial (แรงศัตรูเบาลง · แพ้แล้วเริ่มใหม่ ไม่ตาย)")]
    public bool tutorial = false;

    [Header("เงื่อนไข")]
    [Tooltip("ทำงานเฉพาะในโลก PTSD (ตาม GDD)")]
    public bool requirePtsd = true;
    [Tooltip("Tutorial: ทำงานเฉพาะตอนบอสฉากแรกกำลังไล่อยู่ (ลาก FirstChase_Stalker มาใส่) — เว้นว่าง = ไม่เช็ค")]
    public FirstChaseStalker onlyWhileFirstChaseActive;
    [Tooltip("ทำงานครั้งเดียว")]
    public bool triggerOnce = true;

    [Header("EnemyReachesDoor")]
    [Tooltip("ศัตรูที่ไล่ (Transform)")]
    public Transform enemy;
    [Tooltip("ศัตรูเข้าใกล้ประตูเท่านี้ (เมตร) = เริ่ม")]
    [Min(0.1f)] public float enemyTriggerDistance = 3f;

    [Header("ศัตรูระหว่างยันประตู")]
    [Tooltip("สคริปต์ที่ต้องหยุดชั่วคราว เช่น FirstChaseStalker / StalkerAI / NavMeshAgent")]
    public Behaviour[] pauseDuringStruggle;
    [Tooltip("ตัวศัตรูที่จะย้ายไปยืนหลังประตู (เว้นว่าง = ใช้ช่อง Enemy)")]
    public Transform enemyToPlace;
    [Tooltip("จุดยืนศัตรูหลังประตู (ฝั่งนอกห้อง) — เว้นว่าง = ไม่ย้าย")]
    public Transform enemyDoorPoint;
    [Tooltip("ชนะแล้วย้ายศัตรูไปที่นี่ (ล่าถอย) — เว้นว่าง = ไม่ย้าย")]
    public Transform enemyRetreatPoint;

    private bool playerInside;
    private bool used;
    private bool[] savedStates;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        if (startWhen == StartWhen.PlayerEntersZone) TryFire();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInside = false;
    }

    private void Update()
    {
        if (startWhen != StartWhen.EnemyReachesDoor || !playerInside || enemy == null || struggle == null) return;
        Vector3 a = enemy.position; a.y = 0f;
        Vector3 b = struggle.transform.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= enemyTriggerDistance) TryFire();
    }

    /// <summary>เรียกเองได้ (เช่น จาก UnityEvent) — เช็คเงื่อนไขก่อนเริ่ม</summary>
    public void TryFire()
    {
        if (struggle == null || (triggerOnce && used)) return;
        if (DoorBarricadeStruggle.AnyActive) return;
        if (!tutorial && DoorBarricadeStruggle.SafeTimeRemaining > 0f) return; // ยังอยู่ในเวลาปลอดภัยจากรอบก่อน
        if (requirePtsd && !IsInPtsd()) return;
        if (onlyWhileFirstChaseActive != null && !onlyWhileFirstChaseActive.IsChasing) return;

        PauseEnemies(true);

        Transform placed = enemyToPlace != null ? enemyToPlace : enemy;
        if (placed != null && enemyDoorPoint != null)
            placed.position = new Vector3(enemyDoorPoint.position.x, placed.position.y, enemyDoorPoint.position.z);

        struggle.Ended += HandleEnded;
        if (!struggle.Begin(tutorial))
        {
            struggle.Ended -= HandleEnded;
            PauseEnemies(false);
            return;
        }
        used = true;
    }

    private void HandleEnded(DoorBarricadeStruggle s, DoorBarricadeStruggle.Result result)
    {
        s.Ended -= HandleEnded;

        if (result == DoorBarricadeStruggle.Result.Win)
        {
            Transform placed = enemyToPlace != null ? enemyToPlace : enemy;
            if (placed != null && enemyRetreatPoint != null)
                placed.position = new Vector3(enemyRetreatPoint.position.x, placed.position.y, enemyRetreatPoint.position.z);
        }

        // เปิดสคริปต์ศัตรูคืน (Tutorial: FirstChaseStalker เห็นผู้เล่นอยู่ในครัวแล้ว → หายตัว + ฉากหนีรอดเล่นต่อเอง)
        PauseEnemies(false);

        if (!triggerOnce) used = false;
    }

    private void PauseEnemies(bool pause)
    {
        if (pauseDuringStruggle == null) return;
        if (pause)
        {
            savedStates = new bool[pauseDuringStruggle.Length];
            for (int i = 0; i < pauseDuringStruggle.Length; i++)
            {
                Behaviour b = pauseDuringStruggle[i];
                savedStates[i] = b != null && b.enabled;
                if (b is UnityEngine.AI.NavMeshAgent agent && agent.enabled && agent.isOnNavMesh) agent.ResetPath(); // ต้องสั่งก่อนปิด
                if (b != null) b.enabled = false;
            }
        }
        else if (savedStates != null)
        {
            for (int i = 0; i < pauseDuringStruggle.Length && i < savedStates.Length; i++)
                if (pauseDuringStruggle[i] != null) pauseDuringStruggle[i].enabled = savedStates[i];
            savedStates = null;
        }
    }

    private static bool IsInPtsd()
    {
        IWorldModeService world = ServiceLocator.GetOptional<IWorldModeService>();
        if (world != null) return world.IsInPtsd;
        return PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
    }

    private void OnDrawGizmos()
    {
        Collider c = GetComponent<Collider>();
        if (c == null) return;
        Gizmos.color = tutorial ? new Color(0.2f, 0.8f, 1f, 0.25f) : new Color(1f, 0.3f, 0.2f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (c is BoxCollider box) Gizmos.DrawCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (startWhen == StartWhen.EnemyReachesDoor && struggle != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(struggle.transform.position, enemyTriggerDistance);
        }
    }
}
