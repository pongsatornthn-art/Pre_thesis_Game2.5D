using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สารบัญไอเทมทั้งหมด — ใช้แปลงรหัสกลับเป็น ItemData ตอนโหลดเซฟ (กระเป๋า / กระสุนในแม็ก)
/// สร้างไว้ที่ Assets/Resources/ ชื่อ "ItemCatalog" แล้วคลิกขวา → "เติมไอเทมทั้งโปรเจกต์อัตโนมัติ"
///
/// รหัสไอเทม = ItemData.ItemId (รหัสถาวรที่สุ่มให้) → **เปลี่ยนชื่อไฟล์ไอเทมได้ เซฟเก่าไม่พัง**
/// หาไม่เจอด้วยรหัส → ลองหาด้วยชื่อไฟล์ (เผื่อเซฟที่เก็บไว้ก่อนไอเทมนั้นมีรหัส)
/// เหตุผลที่ใช้ Catalog แทน Resources.LoadAll: ไอเทมส่วนใหญ่ไม่ได้อยู่ในโฟลเดอร์ Resources (บทเรียนจากระบบสมุด)
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Inventory/Item Catalog", order = 0)]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    private Dictionary<string, ItemData> byId;
    private Dictionary<string, ItemData> byName;

    public static string GetId(ItemData item) => item != null ? item.ItemId : null;

    public static ItemCatalog LoadDefault() => Resources.Load<ItemCatalog>("ItemCatalog");

    public ItemData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        BuildLookup();

        if (byId.TryGetValue(id, out ItemData found)) return found;
        if (byName.TryGetValue(id, out found)) return found;   // เซฟรุ่นเก่าที่เก็บเป็นชื่อไฟล์

        Debug.LogWarning($"[ItemCatalog] ไม่พบไอเทม '{id}' — ลืมลากใส่ ItemCatalog หรือเปล่า (คลิกขวา → เติมอัตโนมัติ)");
        return null;
    }

    private void BuildLookup()
    {
        if (byId != null) return;

        byId = new Dictionary<string, ItemData>();
        byName = new Dictionary<string, ItemData>();
        foreach (ItemData item in items)
        {
            if (item == null) continue;

            if (byId.ContainsKey(item.ItemId))
            {
                Debug.LogWarning($"[ItemCatalog] รหัสไอเทมซ้ำ '{item.name}' กับ '{byId[item.ItemId].name}' (มักเกิดจาก Ctrl+D) — คลิกขวา ItemCatalog → เติมอัตโนมัติ เพื่อแก้", item);
                continue;
            }
            byId.Add(item.ItemId, item);
            if (!byName.ContainsKey(item.name)) byName.Add(item.name, item);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("เติมไอเทมทั้งโปรเจกต์อัตโนมัติ (+ แก้รหัสซ้ำ)")]
    private void FillFromProject()
    {
        items.Clear();
        HashSet<string> seen = new HashSet<string>();
        int fixedCount = 0;

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item == null) continue;

            // ยังไม่มีรหัส หรือรหัสซ้ำกับชิ้นก่อนหน้า (ก๊อปไฟล์มา) → สุ่มใหม่
            item.EnsureItemId(forceNew: false);
            if (!seen.Add(item.ItemId))
            {
                item.EnsureItemId(forceNew: true);
                seen.Add(item.ItemId);
                fixedCount++;
            }
            items.Add(item);
        }

        byId = null;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[ItemCatalog] เติมแล้ว {items.Count} ชิ้น · แก้รหัสซ้ำ {fixedCount} ชิ้น");
    }

    private void OnValidate() => byId = null;
#endif
}
