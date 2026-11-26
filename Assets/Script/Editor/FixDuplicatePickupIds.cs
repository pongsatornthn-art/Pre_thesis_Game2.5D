using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-26] แก้รหัส PickupWatcher ซ้ำ (เกิดจาก Ctrl+D ของในแมพ) ในคลิกเดียว
/// เมนู: Tools ▸ Fix Duplicate Pickup IDs — ชิ้นแรกของรหัสนั้นเก็บรหัสเดิม ที่เหลือได้รหัสใหม่ · Ctrl+Z ได้
/// </summary>
public static class FixDuplicatePickupIds
{
    [MenuItem("Tools/Fix Duplicate Pickup IDs")]
    public static void Run()
    {
        var seen = new Dictionary<string, PickupWatcher>();
        var fixedNames = new List<string>();
        PickupWatcher[] all = Object.FindObjectsByType<PickupWatcher>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        foreach (PickupWatcher w in all)
        {
            var so = new SerializedObject(w);
            SerializedProperty id = so.FindProperty("pickupId");
            if (id == null) continue;
            string v = id.stringValue;
            if (!string.IsNullOrEmpty(v) && !seen.ContainsKey(v)) { seen[v] = w; continue; }

            id.stringValue = System.Guid.NewGuid().ToString("N");
            so.ApplyModifiedProperties();   // บันทึก Undo + ทำให้ Scene เป็น dirty เอง
            seen[id.stringValue] = w;
            fixedNames.Add(w.name);
        }

        string msg = fixedNames.Count == 0
            ? $"ตรวจ {all.Length} ชิ้น — ไม่มีรหัสซ้ำ 👍"
            : $"สุ่มรหัสใหม่ให้ {fixedNames.Count} ชิ้น:\n{string.Join(", ", fixedNames)}\n\nกด Ctrl+S เซฟ Scene";
        Debug.Log("[Fix Pickup IDs] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Fix Duplicate Pickup IDs", msg, "ตกลง");
    }
}
