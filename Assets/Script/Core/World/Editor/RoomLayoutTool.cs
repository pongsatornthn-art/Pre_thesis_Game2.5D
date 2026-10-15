using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// เครื่องมือกรอบห้อง (RoomArea) — เมนู Tools › ห้อง (เงาดำ)
///   • ร่างกรอบห้องจากกำแพง: ซอยพื้นเป็นตาราง 25 ซม. → ช่องที่โดนกำแพง/กรอบประตู/ประตู/หน้าต่าง = ผนัง
///     → พื้นที่ที่ต่อถึงกันโดยไม่ข้ามผนัง = 1 ห้อง → แปลงเป็นกล่องหลายกล่อง วางใต้ Map_Test/Rooms
///     (ทางโล่งที่ไม่มีกำแพงกั้นจะกลายเป็นห้องใหญ่ก้อนเดียว — ต้องแบ่งเองทีหลัง)
///   • เช็คช่องโหว่: พื้นที่เดินได้ (NavMesh) ที่ไม่มีห้องครอบ / ครอบซ้อน 2 ห้อง → จุดแดง/ส้มใน Scene view 30 วิ
/// </summary>
public static class RoomLayoutTool
{
    public const float Cell = 0.25f;
    private static readonly string[] StructureGroups = { "All_Wall", "Door_Frame", "Door", "windows" };

    public class Grid
    {
        public float x0, z0;
        public int w, h;
        public bool[,] blocked;
        public bool[,] inside;   // อยู่ในกรอบบ้าน (ขอบนอกสุดของกำแพง)
        public Vector3 Center(int i, int j) => new Vector3(x0 + (i + 0.5f) * Cell, 0.1f, z0 + (j + 0.5f) * Cell);
    }

    public static Transform MapRoot()
    {
        GameObject m = GameObject.Find("Map/Map_Test");
        return m != null ? m.transform : null;
    }

    /// <summary>ตารางพื้นบ้าน: ช่องไหนเป็นผนัง · ช่องไหนอยู่ในกรอบบ้าน</summary>
    public static Grid BuildGrid()
    {
        Transform map = MapRoot();
        if (map == null) { Debug.LogError("[RoomLayout] หา Map/Map_Test ไม่เจอ"); return null; }
        Collider floor = map.Find("NavFloor") ? map.Find("NavFloor").GetComponent<Collider>() : null;
        if (floor == null) { Debug.LogError("[RoomLayout] หา Map_Test/NavFloor ไม่เจอ"); return null; }

        Bounds f = floor.bounds;
        var g = new Grid { x0 = f.min.x, z0 = f.min.z, w = Mathf.CeilToInt(f.size.x / Cell), h = Mathf.CeilToInt(f.size.z / Cell) };
        g.blocked = new bool[g.w, g.h];
        g.inside = new bool[g.w, g.h];

        bool any = false;
        Bounds walls = new Bounds();
        foreach (string name in StructureGroups)
        {
            Transform t = map.Find(name);
            if (t == null) continue;
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                Mark(g, c.bounds);
                if (name == "All_Wall") { if (any) walls.Encapsulate(c.bounds); else { walls = c.bounds; any = true; } }
            }
            if (name == "Door") foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) Mark(g, r.bounds);
        }
        foreach (DoorController d in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (d.solidCollider != null) Mark(g, d.solidCollider.bounds);
            foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true)) Mark(g, r.bounds);
        }

        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                Vector3 p = g.Center(i, j);
                g.inside[i, j] = any && p.x > walls.min.x && p.x < walls.max.x && p.z > walls.min.z && p.z < walls.max.z;
            }
        return g;
    }

    private static void Mark(Grid g, Bounds b)
    {
        int ax = Mathf.Max(0, Mathf.FloorToInt((b.min.x - g.x0) / Cell)), bx = Mathf.Min(g.w - 1, Mathf.FloorToInt((b.max.x - g.x0) / Cell));
        int az = Mathf.Max(0, Mathf.FloorToInt((b.min.z - g.z0) / Cell)), bz = Mathf.Min(g.h - 1, Mathf.FloorToInt((b.max.z - g.z0) / Cell));
        for (int i = ax; i <= bx; i++) for (int j = az; j <= bz; j++) g.blocked[i, j] = true;
    }

    /// <summary>แยกพื้นที่ที่ต่อถึงกัน (ไม่ข้ามผนัง ไม่ออกนอกบ้าน) · คืน id ต่อช่อง (-1 = ผนัง/นอกบ้าน)</summary>
    public static int[,] Regions(Grid g, out int count)
    {
        var reg = new int[g.w, g.h];
        for (int i = 0; i < g.w; i++) for (int j = 0; j < g.h; j++) reg[i, j] = -1;
        count = 0;
        var q = new Queue<Vector2Int>();
        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                if (g.blocked[i, j] || !g.inside[i, j] || reg[i, j] >= 0) continue;
                reg[i, j] = count;
                q.Enqueue(new Vector2Int(i, j));
                while (q.Count > 0)
                {
                    Vector2Int v = q.Dequeue();
                    for (int k = 0; k < 4; k++)
                    {
                        int a = v.x + di[k], b = v.y + dj[k];
                        if (a < 0 || b < 0 || a >= g.w || b >= g.h || g.blocked[a, b] || !g.inside[a, b] || reg[a, b] >= 0) continue;
                        reg[a, b] = count;
                        q.Enqueue(new Vector2Int(a, b));
                    }
                }
                count++;
            }
        return reg;
    }

    /// <summary>สร้าง 1 ห้องใต้ Map_Test/Rooms จากช่องที่ mask = true (แปลงเป็นกล่องน้อยที่สุดแบบง่าย) · Undo ได้</summary>
    public static RoomArea CreateRoom(Grid g, bool[,] mask, string objectName, string displayName, Color color)
    {
        Transform map = MapRoot();
        Transform rooms = map.Find("Rooms");
        if (rooms == null)
        {
            var go = new GameObject("Rooms");
            Undo.RegisterCreatedObjectUndo(go, "Create Rooms");
            go.transform.SetParent(map, false);
            rooms = go.transform;
        }

        var roomGo = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(roomGo, "Create Room");
        roomGo.transform.SetParent(rooms, false);
        var area = roomGo.AddComponent<RoomArea>();
        area.displayName = displayName;
        area.gizmoColor = color;

        var used = new bool[g.w, g.h];
        int n = 0;
        for (int j = 0; j < g.h; j++)
            for (int i = 0; i < g.w; i++)
            {
                if (!mask[i, j] || used[i, j]) continue;
                int wi = 1;
                while (i + wi < g.w && mask[i + wi, j] && !used[i + wi, j]) wi++;
                int hj = 1;
                while (j + hj < g.h)
                {
                    bool ok = true;
                    for (int a = i; a < i + wi && ok; a++) ok = mask[a, j + hj] && !used[a, j + hj];
                    if (!ok) break;
                    hj++;
                }
                for (int a = i; a < i + wi; a++) for (int b = j; b < j + hj; b++) used[a, b] = true;

                var box = new GameObject("Box" + (++n));
                box.transform.SetParent(roomGo.transform, false);
                box.transform.position = new Vector3(g.x0 + (i + wi * 0.5f) * Cell, 1.5f, g.z0 + (j + hj * 0.5f) * Cell);
                box.transform.localScale = new Vector3(wi * Cell, 3f, hj * Cell);
            }
        return area;
    }

    [MenuItem("Tools/ห้อง (เงาดำ)/ร่างกรอบห้องจากกำแพง (สร้างใหม่ทั้งหมด)")]
    private static void DraftFromWalls()
    {
        if (!EditorUtility.DisplayDialog("ร่างกรอบห้อง",
            "จะสร้างกรอบห้องใหม่ใต้ Map_Test/Rooms จากแนวกำแพง\nทางโล่งที่ไม่มีกำแพงกั้นจะรวมเป็นห้องเดียว — ต้องแบ่งเองทีหลัง\n(Undo ได้ · ของเดิมใต้ Rooms ไม่ถูกลบ)", "สร้าง", "ยกเลิก")) return;
        Grid g = BuildGrid();
        if (g == null) return;
        int[,] reg = Regions(g, out int count);
        var rnd = new System.Random(1);
        for (int k = 0; k < count; k++)
        {
            var mask = new bool[g.w, g.h];
            int cells = 0;
            for (int i = 0; i < g.w; i++) for (int j = 0; j < g.h; j++) if (reg[i, j] == k) { mask[i, j] = true; cells++; }
            if (cells * Cell * Cell < 2f) continue;   // เศษเล็กๆ ระหว่างผนัง
            CreateRoom(g, mask, "Room_" + (k + 1), "ห้อง " + (k + 1), Color.HSVToRGB((float)rnd.NextDouble(), 0.6f, 1f));
        }
        Debug.Log($"[RoomLayout] สร้างกรอบห้อง {count} ห้อง — ตั้งชื่อ / แบ่งห้องใหญ่ / แล้วกดเช็คช่องโหว่");
    }

    // ---------- จุดเชื่อมห้อง ----------

    [MenuItem("Tools/ห้อง (เงาดำ)/หาจุดเชื่อมห้อง (สร้างใหม่ทั้งหมด)")]
    private static void GenerateLinksMenu()
    {
        if (!EditorUtility.DisplayDialog("หาจุดเชื่อมห้อง",
            "จะลบ Map_Test/Rooms/_Links เดิม แล้วสร้างจุดเชื่อมใหม่จากช่องประตู + ทางโล่งระหว่างห้อง\n(Undo ได้)", "สร้าง", "ยกเลิก")) return;
        Debug.Log("[RoomLayout] " + GenerateLinks().Replace("\n", " · "));
    }

    /// <summary>สร้างจุดเชื่อมใหม่ทั้งหมด · คืนรายงาน</summary>
    public static string GenerateLinks()
    {
        Transform map = MapRoot();
        Transform rooms = map != null ? map.Find("Rooms") : null;
        if (rooms == null) return "ยังไม่มี Map_Test/Rooms";
        Grid g = BuildGrid();
        if (g == null) return "สร้างตารางไม่ได้";

        RoomArea[] all = rooms.GetComponentsInChildren<RoomArea>();
        foreach (RoomArea r in all) r.Rebuild();
        var roomOf = new int[g.w, g.h];
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                roomOf[i, j] = -1;
                if (g.blocked[i, j]) continue;
                Vector3 p = g.Center(i, j);
                for (int k = 0; k < all.Length; k++) if (all[k].Contains(p)) { roomOf[i, j] = k; break; }
            }

        // ช่องประตู = กรอบประตู + บานประตู (ไม่รวมกำแพง)
        var doorCell = new bool[g.w, g.h];
        void MarkDoor(Bounds b)
        {
            int ax = Mathf.Max(0, Mathf.FloorToInt((b.min.x - g.x0) / Cell)), bx = Mathf.Min(g.w - 1, Mathf.FloorToInt((b.max.x - g.x0) / Cell));
            int az = Mathf.Max(0, Mathf.FloorToInt((b.min.z - g.z0) / Cell)), bz = Mathf.Min(g.h - 1, Mathf.FloorToInt((b.max.z - g.z0) / Cell));
            for (int i = ax; i <= bx; i++) for (int j = az; j <= bz; j++) doorCell[i, j] = true;
        }
        foreach (string name in new[] { "Door_Frame", "Door" })
        {
            Transform t = map.Find(name);
            if (t == null) continue;
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) MarkDoor(c.bounds);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) MarkDoor(r.bounds);
        }
        var controllers = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (DoorController d in controllers) foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true)) MarkDoor(r.bounds);

        // ทางโล่ง: พื้นห้องต่างกันติดกันโดยไม่มีอะไรคั่น
        var openPairs = new Dictionary<long, List<Vector3>>();
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                int a = roomOf[i, j];
                if (a < 0) continue;
                foreach (Vector2Int n in new[] { new Vector2Int(i + 1, j), new Vector2Int(i, j + 1) })
                {
                    if (n.x >= g.w || n.y >= g.h) continue;
                    int b = roomOf[n.x, n.y];
                    if (b < 0 || b == a) continue;
                    long key = Key(a, b);
                    if (!openPairs.TryGetValue(key, out var list)) openPairs[key] = list = new List<Vector3>();
                    list.Add((g.Center(i, j) + g.Center(n.x, n.y)) * 0.5f);
                }
            }

        // ลบของเดิม แล้วสร้างใหม่
        Transform old = rooms.Find("_Links");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var linksGo = new GameObject("_Links");
        Undo.RegisterCreatedObjectUndo(linksGo, "Create Links");
        linksGo.transform.SetParent(rooms, false);

        var report = new System.Text.StringBuilder();
        int nOpen = 0, nDoor = 0, nFree = 0;
        foreach (var kv in openPairs)
        {
            int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
            Vector3 mid = Vector3.zero;
            foreach (Vector3 p in kv.Value) mid += p;
            mid /= kv.Value.Count;
            MakeLink(linksGo.transform, all[a], all[b], null, mid);
            report.AppendLine($"ทางโล่ง: {Label(all[a])} ↔ {Label(all[b])} (กว้าง ~{kv.Value.Count * Cell:F1} ม.)");
            nOpen++;
        }

        // ก้อนประตู (ต่อกันแบบ 8 ทิศ) ที่มีห้องอยู่ 2 ฝั่ง
        var seen = new bool[g.w, g.h];
        var q = new Queue<Vector2Int>();
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                if (!doorCell[i, j] || seen[i, j]) continue;
                var blob = new List<Vector2Int>();
                seen[i, j] = true;
                q.Enqueue(new Vector2Int(i, j));
                while (q.Count > 0)
                {
                    Vector2Int v = q.Dequeue();
                    blob.Add(v);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int a = v.x + dx, b = v.y + dz;
                            if (a < 0 || b < 0 || a >= g.w || b >= g.h || !doorCell[a, b] || seen[a, b]) continue;
                            seen[a, b] = true;
                            q.Enqueue(new Vector2Int(a, b));
                        }
                }

                var near = new Dictionary<int, int>();
                Bounds bb = new Bounds(g.Center(blob[0].x, blob[0].y), Vector3.zero);
                foreach (Vector2Int v in blob)
                {
                    bb.Encapsulate(g.Center(v.x, v.y));
                    for (int dx = -3; dx <= 3; dx++)
                        for (int dz = -3; dz <= 3; dz++)
                        {
                            int a = v.x + dx, b = v.y + dz;
                            if (a < 0 || b < 0 || a >= g.w || b >= g.h || roomOf[a, b] < 0) continue;
                            near[roomOf[a, b]] = near.TryGetValue(roomOf[a, b], out int c) ? c + 1 : 1;
                        }
                }
                if (near.Count < 2) continue;
                int r1 = -1, r2 = -1;
                foreach (var kv in near)
                {
                    if (r1 < 0 || kv.Value > near[r1]) { r2 = r1; r1 = kv.Key; }
                    else if (r2 < 0 || kv.Value > near[r2]) r2 = kv.Key;
                }
                if (openPairs.ContainsKey(Key(r1, r2))) continue;   // มีทางโล่งเชื่อมอยู่แล้ว

                bb.Expand(new Vector3(0.5f, 10f, 0.5f));
                Collider door = null;
                string kind = "ช่องประตูไม่มีบาน (เปิดตลอด)";
                foreach (DoorController d in controllers)
                {
                    if (d.solidCollider == null) continue;
                    foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true))
                        if (r.bounds.Intersects(bb)) { door = d.solidCollider; kind = $"ประตู {d.name} (DoorController)"; break; }
                    if (door != null) break;
                }
                if (door == null && map.Find("Door") != null)
                    foreach (Transform t in map.Find("Door"))
                    {
                        Renderer r = t.GetComponentInChildren<Renderer>(true);
                        Collider c = null;
                        foreach (Collider cc in t.GetComponentsInChildren<Collider>(true)) if (!cc.isTrigger) { c = cc; break; }
                        Bounds tb = r != null ? r.bounds : c != null ? c.bounds : new Bounds(t.position, Vector3.zero);
                        if (!tb.Intersects(bb)) continue;
                        if (c != null) { door = c; kind = $"ประตู {t.name} (ตัวชน)"; }
                        else kind = $"ประตู {t.name} ไม่มีตัวชน (เปิดตลอด)";
                        break;
                    }
                if (door != null) nDoor++; else nFree++;
                MakeLink(linksGo.transform, all[r1], all[r2], door, new Vector3(bb.center.x, 0f, bb.center.z));
                report.AppendLine($"{kind}: {Label(all[r1])} ↔ {Label(all[r2])}");
            }

        EditorSceneManagerMarkDirty(map);
        return $"จุดเชื่อม {nOpen + nDoor + nFree} จุด (ทางโล่ง {nOpen} · ประตูมีตัวชน {nDoor} · ช่องไม่มีตัวชน {nFree})\n" + report;
    }

    private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    private static string Label(RoomArea r) => string.IsNullOrEmpty(r.displayName) ? r.name : r.displayName;

    private static void MakeLink(Transform parent, RoomArea a, RoomArea b, Collider door, Vector3 pos)
    {
        var go = new GameObject("Link_" + a.name.Replace("Room_", "") + "_" + b.name.Replace("Room_", ""));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, 0f, pos.z);
        var link = go.AddComponent<RoomLink>();
        link.roomA = a;
        link.roomB = b;
        link.door = door;
    }

    private static void EditorSceneManagerMarkDirty(Transform t) => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);

    // ---------- เช็คช่องโหว่ ----------

    private static readonly List<Vector3> holes = new List<Vector3>();
    private static readonly List<Vector3> overlaps = new List<Vector3>();
    private static double showUntil;

    [MenuItem("Tools/ห้อง (เงาดำ)/เช็คช่องโหว่")]
    public static void CheckCoverage()
    {
        Grid g = BuildGrid();
        if (g == null) return;
        RoomArea[] rooms = Object.FindObjectsByType<RoomArea>(FindObjectsSortMode.None);
        foreach (RoomArea r in rooms) r.Rebuild();
        holes.Clear();
        overlaps.Clear();
        int walkable = 0;
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                if (g.blocked[i, j] || !g.inside[i, j]) continue;
                Vector3 p = g.Center(i, j);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                walkable++;
                int n = 0;
                foreach (RoomArea r in rooms) if (r.Contains(p)) n++;
                if (n == 0) holes.Add(p);
                else if (n > 1) overlaps.Add(p);
            }
        showUntil = EditorApplication.timeSinceStartup + 30;
        SceneView.duringSceneGui -= DrawMarks;
        SceneView.duringSceneGui += DrawMarks;
        SceneView.RepaintAll();
        string msg = $"พื้นที่เดินได้ {walkable * Cell * Cell:F0} ตร.ม. · ห้อง {rooms.Length} ห้อง\n" +
                     $"ไม่มีห้องครอบ: {holes.Count * Cell * Cell:F1} ตร.ม. (จุดแดง)\nครอบซ้อน 2 ห้อง: {overlaps.Count * Cell * Cell:F1} ตร.ม. (จุดส้ม)";
        Debug.Log("[RoomLayout] " + msg.Replace("\n", " · "));
        EditorUtility.DisplayDialog("เช็คช่องโหว่", msg + (holes.Count + overlaps.Count == 0 ? "\n\n✔ ผ่าน" : "\n\nดูจุดใน Scene view (30 วิ)"), "โอเค");
    }

    private static void DrawMarks(SceneView view)
    {
        if (EditorApplication.timeSinceStartup > showUntil) { SceneView.duringSceneGui -= DrawMarks; return; }
        Handles.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        foreach (Vector3 p in holes) Handles.DrawSolidDisc(p + Vector3.up * 0.2f, Vector3.up, Cell * 0.45f);
        Handles.color = new Color(1f, 0.6f, 0f, 0.9f);
        foreach (Vector3 p in overlaps) Handles.DrawSolidDisc(p + Vector3.up * 0.2f, Vector3.up, Cell * 0.45f);
    }

    /// <summary>ใช้จากโค้ด: นับช่องโหว่/ซ้อน (ตร.ม.) โดยไม่เปิดหน้าต่าง</summary>
    public static Vector2 CoverageNumbers()
    {
        CheckCoverageQuiet();
        return new Vector2(holes.Count * Cell * Cell, overlaps.Count * Cell * Cell);
    }

    private static void CheckCoverageQuiet()
    {
        Grid g = BuildGrid();
        if (g == null) return;
        RoomArea[] rooms = Object.FindObjectsByType<RoomArea>(FindObjectsSortMode.None);
        foreach (RoomArea r in rooms) r.Rebuild();
        holes.Clear();
        overlaps.Clear();
        for (int i = 0; i < g.w; i++)
            for (int j = 0; j < g.h; j++)
            {
                if (g.blocked[i, j] || !g.inside[i, j]) continue;
                Vector3 p = g.Center(i, j);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                int n = 0;
                foreach (RoomArea r in rooms) if (r.Contains(p)) n++;
                if (n == 0) holes.Add(p);
                else if (n > 1) overlaps.Add(p);
            }
    }
}
