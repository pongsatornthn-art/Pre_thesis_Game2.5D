using System.Collections.Generic;
using System.IO;
using UnityEngine;

// --- Data Models (โครงสร้างไฟล์เซฟ) ---
[System.Serializable]
public class GameSaveData
{
    // เก็บรายการวัตถุทั้งหมดในฉาก
    public List<EntitySaveData> entities = new List<EntitySaveData>();
}

[System.Serializable]
public class EntitySaveData
{
    public string uuid; // รหัสประจำตัววัตถุ (เช่น ของ Spawner)
    public List<ComponentSaveData> components = new List<ComponentSaveData>(); // สคริปต์ต่างๆ ในวัตถุนี้
}

[System.Serializable]
public class ComponentSaveData
{
    public string componentName; // ชื่อสคริปต์ (เช่น SpawnerSaveWrapper)
    public string stateJson; // ข้อมูลก้อน JSON ที่ซ่อนอยู่ข้างใน
}

/// <summary>
/// ผู้จัดการศูนย์กลางการ Save / Load ไฟล์ (JSON)
/// เอาไปแปะไว้ที่ [CORE_SERVICES]
/// </summary>
public class SaveManager : MonoBehaviour
{
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "gamesave.json");

    private void Awake()
    {
        ServiceLocator.Register<SaveManager>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<SaveManager>();
    }

    [ContextMenu("TEST SAVE (จำลองการกดเซฟ)")]
    public void SaveGame()
    {
        GameSaveData gameData = new GameSaveData();

        // 1. กวาดสายตาหาทุกวัตถุในฉากที่มีรหัส UUID — รวมของที่ถูกซ่อนอยู่ด้วย
        foreach (var entity in FindAllEntities())
        {
            if (string.IsNullOrEmpty(entity.UUID)) continue;

            EntitySaveData entityData = new EntitySaveData { uuid = entity.UUID };

            // 2. ดึงสคริปต์ทุกอันในวัตถุนั้น ที่มีป้าย ISaveable
            var saveables = entity.GetComponents<ISaveable>();
            foreach (var saveable in saveables)
            {
                string typeName = saveable.GetType().Name;
                string stateJson;
                try
                {
                    stateJson = saveable.CaptureState(); // ขอข้อมูลจากสคริปต์นั้นๆ
                }
                catch (System.Exception e)
                {
                    // ชิ้นเดียวพัง ห้ามลากทั้งไฟล์เซฟพังตาม
                    Debug.LogError($"[SaveManager] เซฟ {typeName} บน {entity.name} ไม่สำเร็จ — ข้ามไป\n{e}", entity);
                    continue;
                }

                entityData.components.Add(new ComponentSaveData
                {
                    componentName = typeName,
                    stateJson = stateJson
                });
            }

            // ถ้าวัตถุนั้นมีข้อมูลเซฟ ก็ยัดใส่กระเป๋าใหญ่
            if (entityData.components.Count > 0)
            {
                gameData.entities.Add(entityData);
            }
        }

        // 3. แปลงเป็นอักษร JSON แบบสวยงาม (Pretty Print)
        string finalJson = JsonUtility.ToJson(gameData, true);

        // 4. เขียนทับลงไฟล์ในเครื่อง
        File.WriteAllText(SaveFilePath, finalJson);
        Debug.Log($"[SaveManager] 💾 เซฟเกมสำเร็จ! บันทึกลงที่: {SaveFilePath}");
    }

    [ContextMenu("TEST LOAD (จำลองการโหลดเกม)")]
    public void LoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.LogWarning("[SaveManager] ❌ ไม่พบไฟล์เซฟ! เริ่มเกมใหม่");
            return;
        }

        // 1. อ่านไฟล์ขึ้นมา
        string json = File.ReadAllText(SaveFilePath);
        GameSaveData gameData = JsonUtility.FromJson<GameSaveData>(json);

        // 2. ทำสมุดรายชื่อ UUID → วัตถุ ครั้งเดียว (รวมของที่ถูกซ่อน เช่น โลก PTSD ที่ปิดอยู่)
        var entityByUuid = new Dictionary<string, SaveableEntity>();
        foreach (var entity in FindAllEntities())
        {
            if (string.IsNullOrEmpty(entity.UUID)) continue;
            if (entityByUuid.ContainsKey(entity.UUID))
            {
                Debug.LogWarning($"[SaveManager] UUID ซ้ำ: {entity.name} กับ {entityByUuid[entity.UUID].name} (มักเกิดจาก Duplicate object) — กด 'บังคับสร้าง UUID ใหม่' ที่ตัวใดตัวหนึ่ง", entity);
                continue;
            }
            entityByUuid.Add(entity.UUID, entity);
        }

        // 3. หากล่องข้อมูลแต่ละใบ
        foreach (var entityData in gameData.entities)
        {
            if (!entityByUuid.TryGetValue(entityData.uuid, out SaveableEntity entityObj)) continue;

            // 4. กระจายข้อมูลให้แต่ละสคริปต์
            var saveables = entityObj.GetComponents<ISaveable>();
            foreach (var saveable in saveables)
            {
                string typeName = saveable.GetType().Name;
                var compData = entityData.components.Find(c => c.componentName == typeName);
                if (compData == null) continue;

                try
                {
                    saveable.RestoreState(compData.stateJson); // คืนความทรงจำ
                }
                catch (System.Exception e)
                {
                    // ของที่ถูกซ่อนตั้งแต่เริ่มฉากยังไม่เคยรัน Awake — สคริปต์ที่เตรียมของใน Awake อาจพังตรงนี้
                    Debug.LogError($"[SaveManager] โหลด {typeName} บน {entityObj.name} ไม่สำเร็จ — ข้ามไป\n{e}", entityObj);
                }
            }
        }

        Debug.Log("[SaveManager] 📂 โหลดเกมสำเร็จ!");
    }

    // ต้อง Include ของที่ถูกซ่อน — PTSDManager สลับโลกด้วยการปิดทั้งก้อน (RealWorld_Env / MemoryWorld_Env)
    // ถ้าไม่ Include ของในโลกที่ปิดอยู่จะถูกข้ามทั้งตอนเซฟและตอนโหลด
    private static SaveableEntity[] FindAllEntities()
    {
        return Object.FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
