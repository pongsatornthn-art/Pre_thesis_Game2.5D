using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ความจำกลางของโลก (World State) — ธงเนื้อเรื่อง + ตัวนับ
/// ทำหน้าที่เป็น Single Source of Truth: ของในซีน/เควส/ทริกเกอร์ ห้ามแอบจำสถานะเอง ให้ถามที่นี่
/// ลงทะเบียน 2 บริการ: IStoryFlags (ธง) · IStoryCounters (ตัวนับ) และเซฟผ่าน ISaveable
///
/// ชื่อคลาสยังเป็น StoryFlagService เพื่อไม่ให้ของที่แปะไว้แล้วหลุด (สเปก: QUEST_SAVE_SPEC.md หัวข้อ 2)
/// </summary>
public class StoryFlagService : MonoBehaviour, IStoryFlags, IStoryCounters, ISaveable
{
    // ใช้ HashSet<string> ตอนรันไทม์เพื่อประสิทธิภาพ O(1) ในการตรวจสอบ Has
    private readonly HashSet<string> activeFlags = new HashSet<string>();
    private readonly Dictionary<string, int> counters = new Dictionary<string, int>();

    [Header("Debug ดูสถานะใน Inspector (อ่านอย่างเดียว)")]
    [SerializeField] private List<string> debugFlagList = new List<string>();
    [SerializeField] private List<string> debugCounterList = new List<string>();

    /// <summary>ยิงเมื่อธงหรือตัวนับเปลี่ยน — ใช้ร่วมกันทั้ง IStoryFlags และ IStoryCounters</summary>
    public event Action OnChanged;

    [Serializable]
    private struct CounterEntry
    {
        public string id;
        public int value;
    }

    [Serializable]
    private struct WorldStateSaveData
    {
        public List<string> savedFlags;
        public List<CounterEntry> savedCounters;
    }

    private void Awake()
    {
        // ลงทะเบียนบริการกลางเพื่อให้ระบบอื่นเรียกใช้ผ่าน ServiceLocator
        ServiceLocator.Register<IStoryFlags>(this);
        ServiceLocator.Register<IStoryCounters>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IStoryFlags>();
        ServiceLocator.Unregister<IStoryCounters>();
    }

    #region Flags

    /// <summary>ตรวจสอบว่าธงนี้ถูกตั้งค่าแล้วหรือไม่</summary>
    public bool Has(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return false;
        return activeFlags.Contains(flag.flagId);
    }

    /// <summary>
    /// ตั้งสถานะธง หากธงถูกตั้งอยู่แล้วจะไม่ยิง OnChanged ซ้ำ
    /// เพื่อป้องกันไม่ให้ระบบเควสหรือทริกเกอร์ทำงานวนรอบโดยไม่จำเป็น
    /// </summary>
    public void Set(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return;

        if (activeFlags.Add(flag.flagId))
        {
            UpdateDebugLists();
            Debug.Log($"<color=cyan>🚩 [WorldState] ตั้งธง: {flag.flagId}</color>");
            OnChanged?.Invoke();
        }
    }

    /// <summary>ล้างสถานะธง</summary>
    public void Clear(StoryFlagId flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.flagId)) return;

        if (activeFlags.Remove(flag.flagId))
        {
            UpdateDebugLists();
            Debug.Log($"<color=yellow>🏳️ [WorldState] ล้างธง: {flag.flagId}</color>");
            OnChanged?.Invoke();
        }
    }

    #endregion

    #region Counters

    public int Get(StoryCounterId counter)
    {
        if (counter == null || string.IsNullOrEmpty(counter.counterId)) return 0;
        return counters.TryGetValue(counter.counterId, out int value) ? value : 0;
    }

    public void Add(StoryCounterId counter, int amount = 1)
    {
        if (amount == 0) return;
        Set(counter, Get(counter) + amount);
    }

    public void Set(StoryCounterId counter, int value)
    {
        if (counter == null || string.IsNullOrEmpty(counter.counterId)) return;

        value = Mathf.Max(0, value);
        if (Get(counter) == value) return;

        counters[counter.counterId] = value;
        UpdateDebugLists();
        Debug.Log($"<color=cyan>🔢 [WorldState] ตัวนับ {counter.counterId} = {value}</color>");
        OnChanged?.Invoke();
    }

    #endregion

    private void UpdateDebugLists()
    {
        // อัปเดตลิสต์ใน Inspector เฉพาะเพื่อการตรวจสอบตอนพัฒนา
        debugFlagList.Clear();
        debugFlagList.AddRange(activeFlags);

        debugCounterList.Clear();
        foreach (KeyValuePair<string, int> pair in counters)
        {
            debugCounterList.Add($"{pair.Key} = {pair.Value}");
        }
    }

    #region ISaveable Implementation

    public string CaptureState()
    {
        WorldStateSaveData data = new WorldStateSaveData
        {
            savedFlags = new List<string>(activeFlags),
            savedCounters = new List<CounterEntry>()
        };

        foreach (KeyValuePair<string, int> pair in counters)
        {
            data.savedCounters.Add(new CounterEntry { id = pair.Key, value = pair.Value });
        }
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;

        WorldStateSaveData data = JsonUtility.FromJson<WorldStateSaveData>(stateJson);
        activeFlags.Clear();
        counters.Clear();

        if (data.savedFlags != null)
        {
            foreach (string id in data.savedFlags)
            {
                if (!string.IsNullOrEmpty(id)) activeFlags.Add(id);
            }
        }

        // เซฟรุ่นแรก (ก่อนมีตัวนับ) ไม่มีช่องนี้ → เป็น null ก็ข้ามไป
        if (data.savedCounters != null)
        {
            foreach (CounterEntry entry in data.savedCounters)
            {
                if (!string.IsNullOrEmpty(entry.id)) counters[entry.id] = Mathf.Max(0, entry.value);
            }
        }

        UpdateDebugLists();
        // ยิงแจ้งเตือนเพื่อให้ระบบเควสและทริกเกอร์อัปเดตสถานะใหม่ทันทีหลังโหลดเซฟ
        OnChanged?.Invoke();
    }

    #endregion
}
