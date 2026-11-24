using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [Map Builder] ตรวจซีนหาปัญหาที่เจอบ่อยตอนจัดแมพ
///   • สคริปต์หาย (Missing Script)      • Prefab ต้นฉบับหาย
///   • วัสดุหาย (Material = None)        • ของซ้อนกันเป๊ะ (กด Ctrl+D แล้วลืม)
///   • ของตกใต้แมพ                       • Collider ที่ขนาด 0
/// </summary>
public static class MapBuilderCheck
{
    public enum Kind { MissingScript, MissingPrefab, MissingMaterial, Duplicate, BelowMap, ZeroCollider }

    public class Issue
    {
        public Kind kind;
        public GameObject go;
        public string message;
        public bool fixable;
    }

    public static string Label(Kind k)
    {
        switch (k)
        {
            case Kind.MissingScript: return "สคริปต์หาย";
            case Kind.MissingPrefab: return "Prefab หาย";
            case Kind.MissingMaterial: return "วัสดุหาย";
            case Kind.Duplicate: return "ซ้อนกัน";
            case Kind.BelowMap: return "ตกใต้แมพ";
            default: return "Collider 0";
        }
    }

    public static List<Issue> Scan(float belowY)
    {
        var issues = new List<Issue>();
        var dupKeys = new Dictionary<string, GameObject>();
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;

                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                {
                    bool inPrefab = PrefabUtility.IsPartOfPrefabInstance(go);
                    issues.Add(new Issue { kind = Kind.MissingScript, go = go, fixable = !inPrefab,
                        message = inPrefab ? $"มีสคริปต์หาย {missing} ตัว (อยู่ใน prefab — เปิด prefab แล้วลบที่นั่น)" : $"มีสคริปต์หาย {missing} ตัว" });
                }

                if (PrefabUtility.IsPrefabAssetMissing(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
                    issues.Add(new Issue { kind = Kind.MissingPrefab, go = go, message = "ไฟล์ prefab ต้นฉบับถูกลบ/ย้าย" });

                Renderer r = go.GetComponent<Renderer>();
                if (r != null && !(r is ParticleSystemRenderer) && !(r is SpriteRenderer))
                {
                    Material[] ms = r.sharedMaterials;
                    if (ms.Length == 0) issues.Add(new Issue { kind = Kind.MissingMaterial, go = go, message = "ไม่มีวัสดุเลย (จะเห็นเป็นสีชมพู)" });
                    else
                        for (int i = 0; i < ms.Length; i++)
                            if (ms[i] == null) { issues.Add(new Issue { kind = Kind.MissingMaterial, go = go, message = $"ช่องวัสดุที่ {i} ว่าง" }); break; }
                }

                if (go.activeInHierarchy && t.position.y < belowY && r != null)
                    issues.Add(new Issue { kind = Kind.BelowMap, go = go, message = $"อยู่ที่ y = {t.position.y:0.00}" });

                BoxCollider box = go.GetComponent<BoxCollider>();
                if (box != null && (box.size.x == 0f || box.size.y == 0f || box.size.z == 0f))
                    issues.Add(new Issue { kind = Kind.ZeroCollider, go = go, message = $"Box Collider ขนาด {box.size}" });

                // ของซ้อน: prefab เดียวกัน ตำแหน่ง+หมุนเดียวกัน (เทียบเฉพาะตัวบนสุดของ prefab)
                if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)
                {
                    Object src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (src != null)
                    {
                        Vector3 p = t.position, e = t.eulerAngles;
                        string key = $"{src.GetInstanceID()}|{Mathf.RoundToInt(p.x * 100)}|{Mathf.RoundToInt(p.y * 100)}|{Mathf.RoundToInt(p.z * 100)}|{Mathf.RoundToInt(e.y)}";
                        if (dupKeys.TryGetValue(key, out GameObject first))
                            issues.Add(new Issue { kind = Kind.Duplicate, go = go, fixable = true, message = $"ซ้อนกับ \"{first.name}\" เป๊ะ" });
                        else dupKeys[key] = go;
                    }
                }
            }
        }
        return issues;
    }

    public static void Fix(Issue i)
    {
        if (i.go == null) return;
        if (i.kind == Kind.MissingScript)
        {
            Undo.RegisterCompleteObjectUndo(i.go, "Map Builder: Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(i.go);
            EditorUtility.SetDirty(i.go);
        }
        else if (i.kind == Kind.Duplicate)
        {
            Undo.DestroyObjectImmediate(i.go);
        }
    }
}
