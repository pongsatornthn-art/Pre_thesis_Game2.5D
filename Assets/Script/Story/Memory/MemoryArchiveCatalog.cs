using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รายชื่อช่องทั้งหมดในคลังความทรงจำ — ลำดับในหน้าสมุด = ลำดับในลิสต์นี้
/// สร้างไว้ที่ Assets/Resources/ ชื่อ "MemoryArchiveCatalog" (ระบบหาเองถ้าไม่ได้ลากใส่) · คลิกขวา "เติมทั้งโปรเจกต์" ได้
/// </summary>
[CreateAssetMenu(fileName = "MemoryArchiveCatalog", menuName = "Story/Memory Archive Catalog", order = 7)]
public class MemoryArchiveCatalog : ScriptableObject
{
    [SerializeField] private List<MemoryEntryData> entries = new List<MemoryEntryData>();

    public IReadOnlyList<MemoryEntryData> Entries => entries;

    public static MemoryArchiveCatalog LoadDefault() => Resources.Load<MemoryArchiveCatalog>("MemoryArchiveCatalog");

    public MemoryEntryData Find(string entryId)
    {
        if (string.IsNullOrEmpty(entryId)) return null;
        foreach (MemoryEntryData e in entries) if (e != null && e.EntryId == entryId) return e;
        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("เติมทั้งโปรเจกต์ (เรียงตามชื่อไฟล์)")]
    private void FillFromProject()
    {
        entries.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:MemoryEntryData"))
        {
            MemoryEntryData e = UnityEditor.AssetDatabase.LoadAssetAtPath<MemoryEntryData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (e != null) entries.Add(e);
        }
        entries.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[MemoryArchiveCatalog] เติมแล้ว {entries.Count} ช่อง — ลากสลับลำดับได้ตามต้องการ");
    }
#endif
}
