using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลังความทรงจำ — จำว่าช่องไหนปลดแล้ว / ยังไม่ได้เปิดดู · เซฟผ่าน ISaveable · ปลดแล้วปลดถาวร
/// วางที่ [STORY] (มี SaveableEntity อยู่แล้ว)
///
/// ช่องที่ตั้ง "Unlock When" → คอยดูความจำกลาง / เควส / ไอเทม แล้วปลดเองเมื่อครบ (แบบเดียวกับ QuestService)
/// ปลดใหม่ → ประกาศ MemoryUnlockedEvent (ป้ายแจ้งเตือน/เสียงไปฟังเอง) · ตอนโหลดเซฟไม่ประกาศ
/// </summary>
public class MemoryArchive : MonoBehaviour, IMemoryArchive, ISaveable, ISaveRestoreOrder, ISaveRestoreListener
{
    // โหลดหลังความจำกลาง (-100) และเควส (-50) — เงื่อนไขปลดอาจพึ่งสองอย่างนั้น
    public int RestoreOrder => -40;

    [Tooltip("เว้นว่าง = หา Resources/MemoryArchiveCatalog เอง")]
    [SerializeField] private MemoryArchiveCatalog catalog;

    private readonly HashSet<string> unlocked = new HashSet<string>();
    private readonly HashSet<string> seen = new HashSet<string>();
    private static readonly MemoryEntryData[] Empty = new MemoryEntryData[0];

    private IStoryFlags flags;
    private IQuestService quests;
    private IKeyItemHolder keyItems;
    private Inventory inventory;

    public event Action OnChanged;

    public IReadOnlyList<MemoryEntryData> Entries => catalog != null ? catalog.Entries : Empty;

    [Serializable]
    private class ArchiveSaveData
    {
        public List<string> unlocked = new List<string>();
        public List<string> seen = new List<string>();
    }

    private void Awake()
    {
        ServiceLocator.Register<IMemoryArchive>(this);
        if (catalog == null) catalog = MemoryArchiveCatalog.LoadDefault();
        if (catalog == null) Debug.LogWarning("[MemoryArchive] ไม่มี MemoryArchiveCatalog — หน้าคลังความทรงจำจะว่าง (สร้างที่ Assets/Resources/MemoryArchiveCatalog)", this);
    }

    private void Start()
    {
        // ฟังทุกแหล่งที่ทำให้เงื่อนไข "Unlock When" เปลี่ยนได้
        flags = ServiceLocator.GetOptional<IStoryFlags>();
        quests = ServiceLocator.GetOptional<IQuestService>();
        keyItems = ServiceLocator.GetOptional<IKeyItemHolder>();
        inventory = Inventory.Instance;

        if (flags != null) flags.OnChanged += EvaluateAuto;
        if (quests != null) quests.OnChanged += EvaluateAuto;
        if (keyItems != null) keyItems.OnChanged += EvaluateAuto;
        if (inventory != null) inventory.OnInventoryChanged += EvaluateAuto;

        if (!SaveRestoreScope.IsBusy) EvaluateAuto();
    }

    private void OnDestroy()
    {
        if (flags != null) flags.OnChanged -= EvaluateAuto;
        if (quests != null) quests.OnChanged -= EvaluateAuto;
        if (keyItems != null) keyItems.OnChanged -= EvaluateAuto;
        if (inventory != null) inventory.OnInventoryChanged -= EvaluateAuto;
        ServiceLocator.Unregister<IMemoryArchive>();
    }

    public bool IsUnlocked(MemoryEntryData entry) => entry != null && unlocked.Contains(entry.EntryId);
    public bool IsNew(MemoryEntryData entry) => IsUnlocked(entry) && !seen.Contains(entry.EntryId);

    public void Unlock(MemoryEntryData entry) => UnlockInternal(entry, silent: SaveRestoreScope.IsBusy);

    public void MarkSeen(MemoryEntryData entry)
    {
        if (entry != null && IsUnlocked(entry) && seen.Add(entry.EntryId)) OnChanged?.Invoke();
    }

    private void UnlockInternal(MemoryEntryData entry, bool silent)
    {
        if (entry == null || !unlocked.Add(entry.EntryId)) return;

        if (catalog != null && catalog.Find(entry.EntryId) == null)
        {
            Debug.LogWarning($"[MemoryArchive] ช่อง '{entry.name}' ปลดแล้วแต่ไม่อยู่ใน MemoryArchiveCatalog — จะไม่โชว์ในหน้าสมุด", entry);
        }

        Debug.Log($"<color=#c9a0ff>🗂️ [MemoryArchive] ปลดความทรงจำ: {entry.EntryId}</color>");
        OnChanged?.Invoke();
        if (!silent) GameEventBus.Publish(new MemoryUnlockedEvent(entry));
    }

    private void EvaluateAuto() => EvaluateAuto(silent: false);

    private void EvaluateAuto(bool silent)
    {
        if (catalog == null || (!silent && SaveRestoreScope.IsBusy)) return;

        StoryContext ctx = StoryContext.Create(this);
        foreach (MemoryEntryData e in catalog.Entries)
        {
            if (e == null || e.unlockWhen == null || IsUnlocked(e)) continue;
            if (e.unlockWhen.IsMet(ctx)) UnlockInternal(e, silent);
        }
    }

    public void OnRestoreCompleted() => EvaluateAuto(silent: true);

    #region ISaveable

    public string CaptureState()
    {
        return JsonUtility.ToJson(new ArchiveSaveData
        {
            unlocked = new List<string>(unlocked),
            seen = new List<string>(seen)
        });
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        ArchiveSaveData data = JsonUtility.FromJson<ArchiveSaveData>(stateJson);

        unlocked.Clear();
        seen.Clear();
        if (data?.unlocked != null) unlocked.UnionWith(data.unlocked);
        if (data?.seen != null) seen.UnionWith(data.seen);
        OnChanged?.Invoke();
    }

    #endregion
}
