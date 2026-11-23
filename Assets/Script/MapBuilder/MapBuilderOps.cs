using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Map Builder] คำสั่งจัดของในซีน — ใช้ได้ทั้งจากหน้าต่าง Map Builder และเมนู Tools/Map Builder (มีคีย์ลัด)
/// ทุกคำสั่ง Undo ได้ (Ctrl+Z) · เป็นเครื่องมือใน Editor อย่างเดียว ไม่มีผลกับเกม
/// </summary>
public static class MapBuilderOps
{
    // ───────── ค่าที่ใช้ร่วมกัน (จำไว้ใน EditorPrefs) ─────────
    public static float GridSize
    {
        get => EditorPrefs.GetFloat("MapBuilder.Grid", 0.25f);
        set => EditorPrefs.SetFloat("MapBuilder.Grid", Mathf.Max(0.01f, value));
    }
    public static float GroundY
    {
        get => EditorPrefs.GetFloat("MapBuilder.GroundY", 0f);
        set => EditorPrefs.SetFloat("MapBuilder.GroundY", value);
    }

    // ───────── เมนู + คีย์ลัด ─────────
    [MenuItem("Tools/Map Builder/วางลงพื้น (Drop to Floor) _END", false, 100)]
    public static void DropToFloorMenu() => DropToFloor(Selection.transforms);
    [MenuItem("Tools/Map Builder/วางลงพื้น (Drop to Floor) _END", true)]
    private static bool HasSelection() => Selection.transforms.Length > 0;

    [MenuItem("Tools/Map Builder/ชิดกริด (Snap to Grid) &g", false, 101)]
    public static void SnapMenu() => SnapToGrid(Selection.transforms, true, false, true);
    [MenuItem("Tools/Map Builder/ชิดกริด (Snap to Grid) &g", true)]
    private static bool HasSelection2() => Selection.transforms.Length > 0;

    [MenuItem("Tools/Map Builder/หมุน +90° &r", false, 102)]
    public static void Rot90Menu() => RotateY(Selection.transforms, 90f);
    [MenuItem("Tools/Map Builder/หมุน +90° &r", true)]
    private static bool HasSelection3() => Selection.transforms.Length > 0;

    // ───────── Bounds ─────────
    /// <summary>กรอบรวมของ Renderer ทั้งหมดใต้วัตถุ (ไม่มี Renderer = จุดเดียวที่ตำแหน่งวัตถุ)</summary>
    public static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in rs)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public static Bounds WorldBounds(IEnumerable<Transform> ts)
    {
        bool any = false;
        Bounds b = default;
        foreach (Transform t in ts)
        {
            if (t == null) continue;
            Bounds bb = WorldBounds(t.gameObject);
            if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
        }
        return b;
    }

    /// <summary>เอาเฉพาะตัวบนสุดที่เลือก (เลือกพ่อกับลูกพร้อมกัน → ขยับแค่พ่อ ไม่ขยับซ้ำ)</summary>
    public static List<Transform> TopLevel(Transform[] ts)
    {
        var set = new HashSet<Transform>(ts);
        var list = new List<Transform>();
        foreach (Transform t in ts)
        {
            bool childOfSelected = false;
            for (Transform p = t.parent; p != null; p = p.parent)
                if (set.Contains(p)) { childOfSelected = true; break; }
            if (!childOfSelected) list.Add(t);
        }
        return list;
    }

    // ───────── วางลงพื้น ─────────
    public static void DropToFloor(Transform[] ts)
    {
        var list = TopLevel(ts);
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Drop to Floor");
        foreach (Transform t in list)
        {
            Bounds b = WorldBounds(t.gameObject);
            float bottom = b.min.y;
            float floorY = FindFloorBelow(t, new Vector3(b.center.x, bottom + 0.05f, b.center.z), out bool found);
            if (!found) floorY = GroundY;
            t.position += Vector3.up * (floorY - bottom);
        }
    }

    /// <summary>ยิงลงหาพื้นใต้จุดนี้ (ข้ามตัวเอง) — Collider ก่อน ไม่เจอค่อยหาจากโมเดลในซีน</summary>
    public static float FindFloorBelow(Transform self, Vector3 from, out bool found)
    {
        found = false;
        float best = float.MaxValue, y = 0f;
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (self != null && h.transform.IsChildOf(self)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
        }
        return y;
    }

    // ───────── กริด ─────────
    public static float Snap(float v, float grid) => Mathf.Round(v / grid) * grid;

    public static void SnapToGrid(Transform[] ts, bool x, bool y, bool z)
    {
        var list = TopLevel(ts);
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Snap to Grid");
        float g = GridSize;
        foreach (Transform t in list)
        {
            Vector3 p = t.position;
            if (x) p.x = Snap(p.x, g);
            if (y) p.y = Snap(p.y, g);
            if (z) p.z = Snap(p.z, g);
            t.position = p;
        }
    }

    // ───────── หมุน ─────────
    public static void RotateY(Transform[] ts, float deg)
    {
        var list = TopLevel(ts);
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Rotate");
        foreach (Transform t in list) t.Rotate(0f, deg, 0f, Space.World);
    }

    public static void RandomRotateY(Transform[] ts, bool only90)
    {
        var list = TopLevel(ts);
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Random Rotate");
        foreach (Transform t in list)
        {
            float a = only90 ? 90f * Random.Range(0, 4) : Random.Range(0f, 360f);
            Vector3 e = t.eulerAngles;
            t.rotation = Quaternion.Euler(e.x, a, e.z);
        }
    }

    public static void ResetRotation(Transform[] ts)
    {
        var list = TopLevel(ts);
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Reset Rotation");
        foreach (Transform t in list) t.rotation = Quaternion.identity;
    }

    // ───────── จัดแนว / เว้นระยะ ─────────
    public enum AlignMode { Min, Center, Max }

    /// <summary>จัดแนวตามกรอบรวม: axis 0=X 1=Y 2=Z</summary>
    public static void Align(Transform[] ts, int axis, AlignMode mode)
    {
        var list = TopLevel(ts);
        if (list.Count < 2) return;
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Align");
        Bounds all = WorldBounds(list);
        float target = mode == AlignMode.Min ? all.min[axis] : mode == AlignMode.Max ? all.max[axis] : all.center[axis];
        foreach (Transform t in list)
        {
            Bounds b = WorldBounds(t.gameObject);
            float cur = mode == AlignMode.Min ? b.min[axis] : mode == AlignMode.Max ? b.max[axis] : b.center[axis];
            Vector3 d = Vector3.zero; d[axis] = target - cur;
            t.position += d;
        }
    }

    /// <summary>เว้นระยะเท่าๆ กันระหว่างตัวแรกกับตัวสุดท้าย (เรียงตามตำแหน่ง)</summary>
    public static void Distribute(Transform[] ts, int axis)
    {
        var list = TopLevel(ts);
        if (list.Count < 3) return;
        Undo.RecordObjects(list.ConvertAll(t => (Object)t).ToArray(), "Map Builder: Distribute");
        list.Sort((a, b) => WorldBounds(a.gameObject).center[axis].CompareTo(WorldBounds(b.gameObject).center[axis]));
        float a0 = WorldBounds(list[0].gameObject).center[axis];
        float a1 = WorldBounds(list[list.Count - 1].gameObject).center[axis];
        for (int i = 1; i < list.Count - 1; i++)
        {
            float want = Mathf.Lerp(a0, a1, i / (float)(list.Count - 1));
            float cur = WorldBounds(list[i].gameObject).center[axis];
            Vector3 d = Vector3.zero; d[axis] = want - cur;
            list[i].position += d;
        }
    }

    // ───────── ก๊อปเป็นแถว ─────────
    public static void DuplicateArray(Transform[] ts, int count, Vector3 offset)
    {
        var list = TopLevel(ts);
        var created = new List<Object>();
        foreach (Transform t in list)
        {
            for (int i = 1; i <= count; i++)
            {
                GameObject copy = CloneKeepPrefab(t.gameObject);
                copy.transform.SetParent(t.parent, false);
                copy.transform.SetPositionAndRotation(t.position + offset * i, t.rotation);
                copy.transform.localScale = t.localScale;
                copy.transform.SetSiblingIndex(t.GetSiblingIndex() + i);
                Undo.RegisterCreatedObjectUndo(copy, "Map Builder: Duplicate Array");
                created.Add(copy);
            }
        }
        if (created.Count > 0) Selection.objects = created.ToArray();
    }

    /// <summary>ก๊อปวัตถุ — ถ้าเป็น prefab จะยังลิงก์กับ prefab เดิม (แก้ prefab แล้วตามไปด้วย)</summary>
    public static GameObject CloneKeepPrefab(GameObject src)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(src);
        if (root == src)
        {
            Object asset = PrefabUtility.GetCorrespondingObjectFromSource(src);
            if (asset != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, src.scene);
                PrefabUtility.SetPropertyModifications(inst, PrefabUtility.GetPropertyModifications(src));
                inst.name = src.name;
                return inst;
            }
        }
        GameObject c = Object.Instantiate(src);
        c.name = src.name;
        return c;
    }

    // ───────── แทนที่ด้วย prefab อื่น ─────────
    public static void ReplaceWithPrefab(Transform[] ts, GameObject prefab, bool keepScale)
    {
        if (prefab == null) return;
        var list = TopLevel(ts);
        var created = new List<Object>();
        foreach (Transform t in list)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(inst, "Map Builder: Replace");
            Undo.SetTransformParent(inst.transform, t.parent, "Map Builder: Replace");
            inst.transform.SetPositionAndRotation(t.position, t.rotation);
            if (keepScale) inst.transform.localScale = t.localScale;
            inst.transform.SetSiblingIndex(t.GetSiblingIndex());
            created.Add(inst);
            Undo.DestroyObjectImmediate(t.gameObject);
        }
        if (created.Count > 0) Selection.objects = created.ToArray();
    }

    // ───────── จัดกลุ่ม / ตั้งชื่อ ─────────
    public static void Group(Transform[] ts, string name)
    {
        var list = TopLevel(ts);
        if (list.Count == 0) return;
        Bounds b = WorldBounds(list);
        GameObject g = new GameObject(string.IsNullOrWhiteSpace(name) ? "Group" : name);
        Undo.RegisterCreatedObjectUndo(g, "Map Builder: Group");
        Transform parent = list[0].parent;
        foreach (Transform t in list) if (t.parent != parent) { parent = null; break; }
        g.transform.SetParent(parent, false);
        g.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
        g.transform.SetSiblingIndex(list[0].GetSiblingIndex());
        foreach (Transform t in list) Undo.SetTransformParent(t, g.transform, "Map Builder: Group");
        Selection.activeGameObject = g;
        EditorGUIUtility.PingObject(g);
    }

    public static void RenameSequential(Transform[] ts, string baseName, int start)
    {
        var list = TopLevel(ts);
        list.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        Undo.RecordObjects(list.ConvertAll(t => (Object)t.gameObject).ToArray(), "Map Builder: Rename");
        for (int i = 0; i < list.Count; i++) list[i].gameObject.name = $"{baseName}_{(start + i):00}";
    }

    /// <summary>หา/สร้าง GameObject ตามเส้นทาง เช่น "[MAP]/Furniture"</summary>
    public static Transform GetOrCreatePath(string path)
    {
        Transform cur = null;
        foreach (string part in path.Split('/'))
        {
            if (string.IsNullOrEmpty(part)) continue;
            Transform next = null;
            if (cur == null)
            {
                foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (r.name == part) { next = r.transform; break; }
            }
            else next = cur.Find(part);
            if (next == null)
            {
                GameObject g = new GameObject(part);
                Undo.RegisterCreatedObjectUndo(g, "Map Builder: Create Group");
                if (cur != null) g.transform.SetParent(cur, false);
                next = g.transform;
            }
            cur = next;
        }
        return cur;
    }
}
