using UnityEngine;

/// <summary>
/// จุดเกิดสำหรับมอนสเตอร์ในโลก PTSD 
/// เป็นผู้จ่าย Data (Injection) ให้กับมอนสเตอร์เมื่อมันเกิดออกมา
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Tooltip("ลาก Prefab มอนสเตอร์ (ตัวเดิมตัวเดียวทั้งเกม) มาใส่")]
    public GameObject monsterPrefabToSpawn;

    [Header("Behavior Setup (นิสัยมอนสเตอร์จุดนี้)")]
    public PTSDMonsterAI.PatrolBehaviorType spawnBehavior;
    
    [Tooltip("ลากกล่อง PatrolZone ในฉากมาใส่ (ลากตัวมันเองใส่ก็ได้ถ้าแปะสคริปต์ไว้ด้วยกัน)")]
    public PatrolZone linkedZone;
    
    [Tooltip("ลากกล่อง PatrolRoute ในฉากมาใส่")]
    public PatrolRoute linkedRoute;

    [Header("Spawn Settings (การตั้งค่าการเกิด)")]
    [Tooltip("จำนวนมอนสเตอร์ตั้งต้น (โควต้าสูงสุด)")]
    [Min(1)] public int spawnCount = 1;
    [Tooltip("ระยะกระจายตัวตอนเกิด (เพื่อไม่ให้มอนสเตอร์เกิดทับกัน)")]
    public float spawnRadius = 2f;

    [HideInInspector]
    public int remainingCount = -1; // ตัวแปรนี้จะถูกเซฟ (ถ้า -1 แปลว่าเพิ่งเริ่มเกม ยังไม่เคยเซฟ)

    // เก็บมอนสเตอร์ทั้งหมดที่ถูกเสกจากจุดนี้
    private System.Collections.Generic.List<GameObject> spawnedMonsters = new System.Collections.Generic.List<GameObject>();

    private void Awake()
    {
        // ถ้าโหลดเกมมาแล้วไม่มีข้อมูลเซฟ (ยังเป็น -1 อยู่) ให้โควต้าเท่ากับ spawnCount ตอนเริ่ม
        if (remainingCount == -1)
        {
            remainingCount = spawnCount;
        }
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePTSDStateChange;
    }

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePTSDStateChange;
    }

    private void HandlePTSDStateChange(bool isPTSDActive)
    {
        if (isPTSDActive)
        {
            // ถ้าโควต้าหมดแล้ว (โดนตีตายหมดแล้ว) ก็ไม่ต้องเสกให้เปลืองทรัพยากร
            if (remainingCount <= 0) return;

            if (monsterPrefabToSpawn != null && spawnedMonsters.Count == 0)
            {
                // เปลี่ยนจากเสกตาม spawnCount เป็นเสกตาม remainingCount (ยอดคงเหลือ)
                for (int i = 0; i < remainingCount; i++)
                {
                    Vector3 spawnPos = transform.position;

                    // สุ่มตำแหน่งเกิดรอบๆ จุด Spawner และเช็คให้อยู่บน NavMesh เสมอ
                    if (spawnCount > 1)
                    {
                        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
                        randomOffset.y = 0;
                        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position + randomOffset, out UnityEngine.AI.NavMeshHit hit, spawnRadius, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                        }
                    }

                    GameObject monster = Instantiate(monsterPrefabToSpawn, spawnPos, transform.rotation);
                    spawnedMonsters.Add(monster);
                    
                    var ai = monster.GetComponent<PTSDMonsterAI>();
                    if (ai != null)
                    {
                        ai.InjectBehavior(spawnBehavior, linkedZone, linkedRoute);
                        
                        // สมัครรอฟังว่าถ้ามอนตัวนี้ตาย ให้มาเรียกฟังก์ชัน HandleMonsterDeath ของเรา
                        ai.OnDeath += () => HandleMonsterDeath(monster);
                    }
                }
            }
        }
        else
        {
            // ลบมอนสเตอร์ทั้งหมดที่จุดนี้เสกมา (ลบออกไปชั่วคราวเพราะสลับโลก ไม่ได้ลบโควต้า)
            foreach (var monster in spawnedMonsters)
            {
                if (monster != null) Destroy(monster);
            }
            spawnedMonsters.Clear();
        }
    }

    private void HandleMonsterDeath(GameObject deadMonster)
    {
        if (remainingCount > 0)
        {
            remainingCount--; // หักยอดโควต้าลงไป 1 ทันที!
            Debug.Log($"[MonsterSpawner] มอนสเตอร์ตาย! ยอดคงเหลือในห้องนี้: {remainingCount}/{spawnCount}");
        }
        
        spawnedMonsters.Remove(deadMonster);
        Destroy(deadMonster, 2f); // หน่วงเวลาทำลายศพทิ้ง 2 วินาที
    }

    private void OnDrawGizmos()
    {
        // วาดรูปทรงกระบอกสีม่วง
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.5f);
        Gizmos.DrawMesh(GetCylinderMesh(), transform.position, transform.rotation, new Vector3(1f, 2f, 1f));
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    private Mesh cylinderMesh;
    private Mesh GetCylinderMesh()
    {
        if (cylinderMesh == null)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinderMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(primitive);
        }
        return cylinderMesh;
    }
}
