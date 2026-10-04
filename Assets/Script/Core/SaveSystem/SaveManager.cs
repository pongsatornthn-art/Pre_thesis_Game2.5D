using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// --- Data Models (โครงสร้างไฟล์เซฟ) ---
[Serializable]
public class GameSaveData
{
    public int version;

    // หัวช่องเซฟ — หน้าจอเลือกช่องโชว์ได้โดยไม่ต้องโหลดเกม
    public int slot;
    public string sceneName;
    public string savedAtUtc;
    public float playTimeSeconds;
    public string locationKey;

    // ตอนเซฟอยู่โลกไหน — ไฟล์เซฟจริงเป็น Real เสมอ (ห้ามเซฟใน PTSD) · จุดเริ่มใหม่ตอนตายใน PTSD เป็นโหมด PTSD
    public WorldMode worldMode;

    public bool hasPlayerPose;
    public Vector3 playerPosition;
    public Quaternion playerRotation;

    // เก็บรายการวัตถุทั้งหมดในฉาก
    public List<EntitySaveData> entities = new List<EntitySaveData>();

    // ใช้เฉพาะสำเนาในหน่วยความจำ (จุดเริ่มใหม่ PTSD) — ไม่ลงไฟล์
    // ตายใน PTSD → เล่นฉากที่พาเข้าโลกต่อจากคำสั่งนี้ (ไม่งั้นบทพูด/คำสั่งหลัง EnterPtsd จะหายไป)
    [NonSerialized] public StorySequence resumeSequence;
    [NonSerialized] public int resumeActionIndex = -1;
}

[Serializable]
public class EntitySaveData
{
    public string uuid; // รหัสประจำตัววัตถุ (SaveableEntity)
    public List<ComponentSaveData> components = new List<ComponentSaveData>(); // สคริปต์ต่างๆ ในวัตถุนี้
}

[Serializable]
public class ComponentSaveData
{
    public string componentName; // ชื่อสคริปต์ (เช่น SpawnerSaveWrapper)
    public string stateJson; // ข้อมูลก้อน JSON ที่ซ่อนอยู่ข้างใน
}

/// <summary>
/// ผู้จัดการศูนย์กลางการ Save / Load (JSON) — เอาไปแปะไว้ที่ [CORE_SERVICES]
///
/// ช่องเซฟแบบ Resident Evil:
///   ช่องปกติ 0..N-1 (จุดเซฟในแมพ ผู้เล่นเลือกช่อง) + ช่องอัตโนมัติ 1 ช่องแยก (AutoSlot)
///   แต่ละช่องจำ วันเวลา · เวลาเล่น · ชื่อห้อง · ซีน → GetSlotInfos() ให้หน้าจอเลือกช่อง
///   "เล่นต่อ" = LoadMostRecent() ช่องที่เซฟล่าสุด ไม่ว่าปกติหรืออัตโนมัติ
///
/// หลักการ (QUEST_SAVE_SPEC.md หัวข้อ 6):
/// - **โหลด = เปิดซีนใหม่สะอาด แล้วค่อยใส่ความจำคืน** ข้อมูลรอข้ามซีนในตัวแปร static
/// - ใส่คืนตามลำดับ ISaveRestoreOrder → แล้วแจ้ง ISaveRestoreListener ทีเดียว
/// - **ห้ามเซฟในโลก PTSD** (ถาม IWorldModeService)
/// - เขียนไฟล์ชั่วคราวก่อนแล้วค่อยแทนที่ + เก็บสำรอง .bak ต่อช่อง
/// - มีเลขเวอร์ชัน · ชิ้นไหนพังข้ามแค่ชิ้นนั้น · หาของที่ถูกซ่อนด้วย (โลก PTSD ที่ปิดอยู่)
/// </summary>
public class SaveManager : MonoBehaviour
{
    public const int CurrentVersion = 3;
    public const int AutoSlot = -1;
    public const string BlockedInPtsdKey = "SAVE_BLOCKED_PTSD";

    [Header("ช่องเซฟ")]
    [Tooltip("จำนวนช่องเซฟปกติ (ไม่นับช่องอัตโนมัติ)")]
    [SerializeField, Min(1)] private int manualSlotCount = 6;

    // ข้อมูลที่รอใส่คืนหลังโหลดซีนเสร็จ — static เพราะ SaveManager ตัวเดิมถูกทำลายไปพร้อมซีนเก่า
    private static GameSaveData pendingRestore;
    private static bool pendingFromCheckpoint;

    /// <summary>เวลาเล่นสะสม (วินาที) — ไม่นับตอนหยุดเกม/เปิดสมุด · ถูกเซฟ/โหลดไปกับช่องเซฟ</summary>
    public static float PlayTimeSeconds { get; private set; }

    /// <summary>ห้องที่ผู้เล่นอยู่ล่าสุด (LocationZone / จุดเซฟตั้งให้) — ใช้เป็นชื่อในช่องเซฟอัตโนมัติ</summary>
    public static string CurrentLocationKey { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingRestore = null;
        pendingFromCheckpoint = false;
        PlayTimeSeconds = 0f;
        CurrentLocationKey = null;
    }

    public int ManualSlotCount => manualSlotCount;

    private void Awake()
    {
        ServiceLocator.Register<SaveManager>(this);
    }

    private void Start()
    {
        if (pendingRestore != null) StartCoroutine(ApplyPendingRestore());
    }

    private void Update()
    {
        if (Time.timeScale > 0f) PlayTimeSeconds += Time.unscaledDeltaTime;
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<SaveManager>();
    }

    /// <summary>เริ่มเกมใหม่ (ปุ่ม New Game ในเมนูหลัก) — รีเซ็ตเวลาเล่นและห้องล่าสุด</summary>
    public static void ResetForNewGame()
    {
        PlayTimeSeconds = 0f;
        CurrentLocationKey = null;
    }

    #region Slots

    private static string SlotFileName(int slot) => slot == AutoSlot ? "autosave" : $"save_{slot}";
    private static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, SlotFileName(slot) + ".json");
    private static string BackupPath(int slot) => Path.Combine(Application.persistentDataPath, SlotFileName(slot) + ".bak");
    private static string TempPath(int slot) => Path.Combine(Application.persistentDataPath, SlotFileName(slot) + ".tmp");

    private bool IsValidSlot(int slot) => slot == AutoSlot || (slot >= 0 && slot < manualSlotCount);

    /// <summary>ทุกช่อง (ปกติเรียงตามเลข แล้วตามด้วยช่องอัตโนมัติ) — ช่องว่างก็อยู่ในรายการ (Exists = false)</summary>
    public List<SaveSlotInfo> GetSlotInfos()
    {
        List<SaveSlotInfo> list = new List<SaveSlotInfo>();
        for (int i = 0; i < manualSlotCount; i++) list.Add(GetSlotInfo(i));
        list.Add(GetSlotInfo(AutoSlot));
        return list;
    }

    public SaveSlotInfo GetSlotInfo(int slot)
    {
        GameSaveData data = ReadSlot(slot, logErrors: false);
        if (data == null) return new SaveSlotInfo(slot, false, default, 0f, null, null);

        DateTime savedAt = DateTime.TryParse(data.savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime utc)
            ? utc.ToLocalTime()
            : File.GetLastWriteTime(SlotPath(slot));
        return new SaveSlotInfo(slot, true, savedAt, data.playTimeSeconds, data.locationKey, data.sceneName);
    }

    public bool HasAnySave
    {
        get
        {
            if (File.Exists(SlotPath(AutoSlot))) return true;
            for (int i = 0; i < manualSlotCount; i++) if (File.Exists(SlotPath(i))) return true;
            return false;
        }
    }

    /// <summary>ช่องที่เซฟล่าสุด (สำหรับปุ่ม "เล่นต่อ") · ไม่มีเซฟเลย = false</summary>
    public bool TryGetMostRecentSlot(out int slot)
    {
        slot = 0;
        bool found = false;
        DateTime latest = DateTime.MinValue;

        foreach (SaveSlotInfo info in GetSlotInfos())
        {
            if (!info.Exists || info.SavedAtLocal <= latest) continue;
            latest = info.SavedAtLocal;
            slot = info.Slot;
            found = true;
        }
        return found;
    }

    public void DeleteSlot(int slot)
    {
        if (!IsValidSlot(slot)) return;
        foreach (string path in new[] { SlotPath(slot), BackupPath(slot), TempPath(slot) })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    #endregion

    #region Save

    /// <summary>ตอนนี้เซฟได้ไหม — ไม่ได้จะบอกเหตุผลเป็น key แปลภาษา</summary>
    public bool CanSave(out string blockReasonKey)
    {
        blockReasonKey = null;

        if (ServiceLocator.TryGet(out IWorldModeService world) && world.IsInPtsd)
        {
            blockReasonKey = BlockedInPtsdKey;
            return false;
        }
        return true;
    }

    /// <summary>เซฟอัตโนมัติ → ช่องอัตโนมัติ</summary>
    public bool TryAutoSave() => TrySaveToSlot(AutoSlot, SaveReason.Auto, null, null);

    /// <summary>
    /// ขอเซฟลงช่อง — ถ้าไม่อนุญาตจะประกาศ SaveBlockedEvent แล้วคืน false
    /// spawnPoint = ตำแหน่งที่จะเกิดตอนโหลด (จุดเซฟ) · เว้นว่าง = ตำแหน่งผู้เล่นตอนนี้
    /// locationKey = ชื่อห้องที่โชว์ในช่อง · เว้นว่าง = ห้องล่าสุดที่เดินผ่าน (LocationZone)
    /// </summary>
    public bool TrySaveToSlot(int slot, SaveReason reason, Transform spawnPoint, string locationKey)
    {
        if (!IsValidSlot(slot))
        {
            Debug.LogError($"[SaveManager] ไม่มีช่องเซฟหมายเลข {slot} (มี 0..{manualSlotCount - 1} + อัตโนมัติ)");
            return false;
        }

        if (!CanSave(out string blockKey))
        {
            Debug.Log($"[SaveManager] ⛔ เซฟไม่ได้ตอนนี้ ({blockKey})");
            GameEventBus.Publish(new SaveBlockedEvent(reason, blockKey));
            return false;
        }

        if (!string.IsNullOrEmpty(locationKey)) CurrentLocationKey = locationKey;

        GameSaveData data = CaptureSnapshot(spawnPoint);
        data.slot = slot;

        try
        {
            WriteAtomic(slot, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] เขียนไฟล์เซฟไม่สำเร็จ — เซฟเก่ายังอยู่\n{e}");
            return false;
        }

        Debug.Log($"[SaveManager] 💾 เซฟสำเร็จ ช่อง {(slot == AutoSlot ? "อัตโนมัติ" : slot.ToString())} ({reason}) → {SlotPath(slot)}");
        GameEventBus.Publish(new GameSavedEvent(reason));
        return true;
    }

    /// <summary>
    /// ถ่ายสำเนาสถานะทั้งเกมเก็บไว้ในหน่วยความจำ (ไม่เขียนไฟล์ ไม่เช็คกติกาห้ามเซฟ)
    /// ใช้ทั้งตอนเซฟจริง และตอนจำจุดเริ่มใหม่ของโลก PTSD (PtsdCheckpoint)
    /// </summary>
    public GameSaveData CaptureSnapshot(Transform spawnPoint = null)
    {
        IWorldModeService world = ServiceLocator.TryGet(out IWorldModeService w) ? w : null;

        GameSaveData gameData = new GameSaveData
        {
            version = CurrentVersion,
            sceneName = SceneManager.GetActiveScene().name,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            playTimeSeconds = PlayTimeSeconds,
            locationKey = CurrentLocationKey,
            worldMode = world != null ? world.Mode : WorldMode.Real
        };

        Transform pose = spawnPoint;
        if (pose == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) pose = player.transform;
        }
        if (pose != null)
        {
            gameData.hasPlayerPose = true;
            gameData.playerPosition = pose.position;
            gameData.playerRotation = pose.rotation;
        }

        // 1. กวาดสายตาหาทุกวัตถุในฉากที่มีรหัส UUID — รวมของที่ถูกซ่อนอยู่ด้วย
        foreach (var entity in FindAllEntities())
        {
            if (string.IsNullOrEmpty(entity.UUID)) continue;

            EntitySaveData entityData = new EntitySaveData { uuid = entity.UUID };

            // 2. ดึงสคริปต์ทุกอันในวัตถุนั้น ที่มีป้าย ISaveable
            foreach (var saveable in entity.GetComponents<ISaveable>())
            {
                string typeName = saveable.GetType().Name;
                string stateJson;
                try
                {
                    stateJson = saveable.CaptureState(); // ขอข้อมูลจากสคริปต์นั้นๆ
                }
                catch (Exception e)
                {
                    // ชิ้นเดียวพัง ห้ามลากทั้งไฟล์เซฟพังตาม
                    Debug.LogError($"[SaveManager] เซฟ {typeName} บน {entity.name} ไม่สำเร็จ — ข้ามไป\n{e}", entity);
                    continue;
                }

                entityData.components.Add(new ComponentSaveData { componentName = typeName, stateJson = stateJson });
            }

            // ถ้าวัตถุนั้นมีข้อมูลเซฟ ก็ยัดใส่กระเป๋าใหญ่
            if (entityData.components.Count > 0) gameData.entities.Add(entityData);
        }

        return gameData;
    }

    private static void WriteAtomic(int slot, string json)
    {
        string path = SlotPath(slot);
        string temp = TempPath(slot);
        File.WriteAllText(temp, json);

        if (File.Exists(path)) File.Replace(temp, path, BackupPath(slot));
        else File.Move(temp, path);
    }

    #endregion

    #region Load

    /// <summary>"เล่นต่อ" — โหลดช่องที่เซฟล่าสุด</summary>
    public bool LoadMostRecent()
    {
        if (!TryGetMostRecentSlot(out int slot))
        {
            Debug.LogWarning("[SaveManager] ❌ ยังไม่มีเซฟเลย");
            return false;
        }
        return LoadSlot(slot);
    }

    /// <summary>โหลดช่อง — เปิดซีนที่เซฟไว้ใหม่ แล้วใส่ความจำคืน (ไฟล์หลักเสีย → ลองไฟล์สำรอง)</summary>
    public bool LoadSlot(int slot)
    {
        if (!IsValidSlot(slot)) return false;

        GameSaveData data = ReadSlot(slot, logErrors: true);
        if (data == null)
        {
            Debug.LogWarning($"[SaveManager] ❌ ช่อง {slot} ว่างหรือเสีย");
            return false;
        }

        LoadSnapshot(data, fromCheckpoint: false);
        return true;
    }

    /// <summary>กลับไปสถานะในสำเนา — เปิดซีนใหม่สะอาดแล้วใส่คืน (ใช้ทั้งโหลดเซฟ และเริ่มโลก PTSD ใหม่ตอนตาย)</summary>
    public void LoadSnapshot(GameSaveData data, bool fromCheckpoint)
    {
        if (data == null) return;

        pendingRestore = data;
        pendingFromCheckpoint = fromCheckpoint;
        SaveRestoreScope.HasPendingRestore = true;

        // มินิเกมสตอล์กเกอร์ / เมนูหยุด อาจค้าง timeScale = 0 ไว้ — ซีนใหม่ต้องเริ่มที่เวลาปกติ
        Time.timeScale = 1f;

        string scene = string.IsNullOrEmpty(data.sceneName) ? SceneManager.GetActiveScene().name : data.sceneName;
        Debug.Log($"[SaveManager] 📂 กำลังโหลด → เปิดซีน '{scene}' ใหม่");
        SceneManager.LoadScene(scene);
    }

    private static GameSaveData ReadSlot(int slot, bool logErrors)
    {
        return ReadSaveFile(SlotPath(slot), logErrors) ?? ReadSaveFile(BackupPath(slot), logErrors);
    }

    private static GameSaveData ReadSaveFile(string path, bool logErrors)
    {
        if (!File.Exists(path)) return null;
        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
            if (data != null && data.version > CurrentVersion && logErrors)
            {
                Debug.LogWarning($"[SaveManager] ไฟล์เซฟมาจากเกมเวอร์ชันใหม่กว่า ({data.version} > {CurrentVersion}) — บางอย่างอาจโหลดไม่ครบ");
            }
            return data;
        }
        catch (Exception e)
        {
            if (logErrors) Debug.LogError($"[SaveManager] ไฟล์เซฟเสีย อ่านไม่ได้: {path}\n{e}");
            return null;
        }
    }

    private IEnumerator ApplyPendingRestore()
    {
        // รอ 1 เฟรม — ให้ทุกชิ้นในซีนใหม่ Awake/Start ครบก่อน (ลงทะเบียนบริการ / Inventory สร้างช่อง)
        yield return null;

        GameSaveData data = pendingRestore;
        bool fromCheckpoint = pendingFromCheckpoint;
        pendingRestore = null;
        SaveRestoreScope.HasPendingRestore = false;

        PlayTimeSeconds = data.playTimeSeconds;
        CurrentLocationKey = data.locationKey;

        RestoreEntities(data);
        RestorePlayerPose(data);

        // จุดเริ่มใหม่ในโลก PTSD → พากลับเข้าโลกนั้น (ไฟล์เซฟจริงเป็น Real เสมอ ไม่เข้าเงื่อนไขนี้)
        if (data.worldMode != WorldMode.Real && ServiceLocator.TryGet(out IWorldModeService world))
        {
            world.EnterPtsd(data.worldMode);
        }

        // แล้วเล่นฉากที่พาเข้าโลกต่อ — เริ่มที่คำสั่ง "เข้าโลก PTSD" เดิม (สั่งซ้ำไม่มีผลเพราะเข้าไปแล้ว แต่ได้รอเวลาเท่าเดิม)
        // แล้วต่อด้วยบทพูด/คำสั่งที่อยู่หลังมันตามปกติ
        if (data.resumeSequence != null && StoryDirector.Instance != null)
        {
            StoryDirector.Instance.Play(data.resumeSequence, data.resumeActionIndex);
        }

        Debug.Log("[SaveManager] 📂 โหลดเกมสำเร็จ!");
        GameEventBus.Publish(new GameLoadedEvent(fromCheckpoint));
    }

    private void RestoreEntities(GameSaveData data)
    {
        // ทำสมุดรายชื่อ UUID → วัตถุ ครั้งเดียว (รวมของที่ถูกซ่อน เช่น โลก PTSD ที่ปิดอยู่)
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

        // จับคู่ข้อมูลกับสคริปต์จริง แล้วเรียงตาม RestoreOrder
        var jobs = new List<(int order, ISaveable saveable, string json, string label, SaveableEntity owner)>();
        if (data.entities != null)
        {
            foreach (var entityData in data.entities)
            {
                if (entityData == null || !entityByUuid.TryGetValue(entityData.uuid, out SaveableEntity owner)) continue;

                foreach (var saveable in owner.GetComponents<ISaveable>())
                {
                    string typeName = saveable.GetType().Name;
                    var compData = entityData.components.Find(c => c.componentName == typeName);
                    if (compData == null) continue;

                    int order = saveable is ISaveRestoreOrder ordered ? ordered.RestoreOrder : 0;
                    jobs.Add((order, saveable, compData.stateJson, $"{typeName} บน {owner.name}", owner));
                }
            }
        }
        jobs.Sort((a, b) => a.order.CompareTo(b.order));

        SaveRestoreScope.IsRestoring = true;
        try
        {
            foreach (var job in jobs)
            {
                try
                {
                    job.saveable.RestoreState(job.json); // คืนความทรงจำ
                }
                catch (Exception e)
                {
                    // ของที่ถูกซ่อนตั้งแต่เริ่มฉากยังไม่เคยรัน Awake — สคริปต์ที่เตรียมของใน Awake อาจพังตรงนี้
                    Debug.LogError($"[SaveManager] โหลด {job.label} ไม่สำเร็จ — ข้ามไป\n{e}", job.owner);
                }
            }
        }
        finally
        {
            SaveRestoreScope.IsRestoring = false;
        }

        // ใส่คืนครบแล้วค่อยให้ระบบที่คำนวณจากความจำกลางคิดใหม่ทีเดียว
        foreach (var listener in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (listener is ISaveRestoreListener l) l.OnRestoreCompleted();
        }
    }

    private static void RestorePlayerPose(GameSaveData data)
    {
        if (!data.hasPlayerPose) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // ผู้เล่นใช้ Rigidbody — ต้องย้ายทั้ง transform และ rigidbody และล้างความเร็ว ไม่งั้นพุ่งต่อ
        player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
        if (player.TryGetComponent(out Rigidbody rb))
        {
            rb.position = data.playerPosition;
            rb.rotation = data.playerRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    #endregion

    #region Test buttons (คลิกขวาที่คอมโพเนนต์ใน Inspector ตอนกด Play)

    [ContextMenu("TEST SAVE ช่อง 0")]
    public void SaveGame() => TrySaveToSlot(0, SaveReason.Manual, null, null);

    [ContextMenu("TEST SAVE อัตโนมัติ")]
    private void SaveAutoFromMenu() => TryAutoSave();

    [ContextMenu("TEST LOAD ล่าสุด (เล่นต่อ)")]
    private void LoadGameFromMenu() => LoadMostRecent();

    [ContextMenu("TEST แสดงทุกช่องใน Console")]
    private void PrintSlots()
    {
        foreach (SaveSlotInfo s in GetSlotInfos())
        {
            string name = s.IsAuto ? "อัตโนมัติ" : $"ช่อง {s.Slot}";
            Debug.Log(s.Exists ? $"[{name}] {s.SavedAtLocal:g} · เล่นไป {s.PlayTimeText} · {s.LocationKey} · {s.SceneName}" : $"[{name}] ว่าง");
        }
    }

    #endregion

    // ต้อง Include ของที่ถูกซ่อน — PTSDManager สลับโลกด้วยการปิดทั้งก้อน (RealWorld_Env / MemoryWorld_Env)
    // ถ้าไม่ Include ของในโลกที่ปิดอยู่จะถูกข้ามทั้งตอนเซฟและตอนโหลด
    private static SaveableEntity[] FindAllEntities()
    {
        return FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}

/// <summary>
/// ไม่บังคับ — ระบบที่ต้องคำนวณใหม่ "หลังทุกชิ้นใส่ความจำคืนครบ" ให้ implement
/// เช่น QuestService (เควสต้องเห็นธง + ตัวนับ + ของในกระเป๋าครบก่อนตัดสิน)
/// </summary>
public interface ISaveRestoreListener
{
    void OnRestoreCompleted();
}
