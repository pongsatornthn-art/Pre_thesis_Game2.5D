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
    [Tooltip("จำนวนมอนสเตอร์ที่จะเกิดจากจุดนี้")]
    [Min(1)] public int spawnCount = 1;
    [Tooltip("ระยะกระจายตัวตอนเกิด (เพื่อไม่ให้มอนสเตอร์เกิดทับกัน)")]
    public float spawnRadius = 2f;

    // เก็บมอนสเตอร์ทั้งหมดที่ถูกเสกจากจุดนี้
    private System.Collections.Generic.List<GameObject> spawnedMonsters = new System.Collections.Generic.List<GameObject>();

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
            if (monsterPrefabToSpawn != null && spawnedMonsters.Count == 0)
            {
                for (int i = 0; i < spawnCount; i++)
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
                    }
                }
            }
        }
        else
        {
            // ลบมอนสเตอร์ทั้งหมดที่จุดนี้เสกมา
            foreach (var monster in spawnedMonsters)
            {
                if (monster != null) Destroy(monster);
            }
            spawnedMonsters.Clear();
        }
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
