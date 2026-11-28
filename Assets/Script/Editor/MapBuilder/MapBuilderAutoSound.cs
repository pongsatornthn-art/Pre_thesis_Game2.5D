using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Claude 2026-11-26] เติมเสียงทั้งแมพในคลิกเดียว — Tools ▸ Map Builder ▸ เติมเสียงทั้งแมพอัตโนมัติ
///
/// ใช้ Trigger ห้องที่มีอยู่แล้ว (RoomWallFade) เป็นขอบเขตห้อง แล้วดูว่าห้องนั้นคือห้องอะไรจากกลุ่ม Furniture_xxx ที่อยู่ข้างใน
///   ทุกห้อง      : เสียงห้องเงียบๆ (room tone) + ไม้ลั่นสุ่ม · โลก PTSD → เสียงหึ่งหลอน
///   โถงทางเดิน   : ลมพัด + เสียงฝีเท้าชั้นบนสุ่ม (โลกจริง) + เสียงร้องไห้เคาะประตู (เฉพาะ PTSD)
///   ครัว/ห้องน้ำ/ห้องคนใช้ : น้ำหยด
///   นั่งเล่น/ห้องสมุด/ห้องทำงาน/ห้องกินข้าว/ห้องเอกสาร : นาฬิกาเดิน
///   ทั้งบ้าน     : ฝนตกกระทบหน้าต่างเบาๆ + ฟ้าร้องไกลๆ สุ่ม
/// กดซ้ำได้: ลบของเก่าที่เครื่องมือนี้สร้าง ([MAP]/Sounds_Auto) แล้วสร้างใหม่ · ของที่วางเองใน [MAP]/Sounds ไม่โดนแตะ · Ctrl+Z ได้
/// </summary>
public static class MapBuilderAutoSound
{
    private const string AutoRoot = "[MAP]/Sounds_Auto";

    private class Room
    {
        public string label;
        public Bounds bounds;
        public bool hall;
    }

    [MenuItem("Tools/Map Builder/เติมเสียงทั้งแมพอัตโนมัติ", false, 200)]
    public static void Run()
    {
        // ── คลิปเสียง ──
        AudioClip roomTone = Clip("Amb_RoomTone_Loop"), wind = Clip("Amb_Wind_Loop"), rain = Clip("Amb_Rain_Window_Loop"),
                  drone = Clip("Amb_PTSD_Drone_Loop"), tick = Clip("SFX_ClockTick_Loop"), vinyl = Clip("Clock&Vinyl_Ambient"),
                  cry = Clip("Womencry&KnocktheDoor");
        AudioClip[] creaks = Clips("SFX_WoodCreak_0"), drips = Clips("SFX_WaterDrip_0"),
                    thunder = Clips("SFX_Thunder_Distant_0"), steps = Clips("SFX_Footsteps_Upstairs_0");
        if (roomTone == null)
        {
            EditorUtility.DisplayDialog("เติมเสียงอัตโนมัติ", "ไม่เจอไฟล์เสียงชุด Ambience (Amb_RoomTone_Loop ฯลฯ)\nวางโฟลเดอร์ Assets/Audio/Ambience ก่อน", "ตกลง");
            return;
        }

        List<Room> rooms = FindRooms();
        if (rooms.Count == 0)
        {
            EditorUtility.DisplayDialog("เติมเสียงอัตโนมัติ", "ไม่เจอ Trigger ห้อง (RoomWallFade) ในซีนนี้", "ตกลง");
            return;
        }
        if (!EditorUtility.DisplayDialog("เติมเสียงอัตโนมัติ",
                $"เจอ {rooms.Count} พื้นที่ ({rooms.FindAll(r => r.hall).Count} โถง)\nจะลบของเดิมใน {AutoRoot} แล้วสร้างใหม่ (Ctrl+Z ได้)", "เติมเสียง", "ยกเลิก"))
            return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // ลบรอบก่อน
        Transform old = MapBuilderOps.GetOrCreatePath("[MAP]");
        Transform prev = old.Find("Sounds_Auto");
        if (prev != null) Undo.DestroyObjectImmediate(prev.gameObject);
        Transform root = MapBuilderOps.GetOrCreatePath(AutoRoot);

        int made = 0;
        Bounds all = rooms[0].bounds;
        foreach (Room r in rooms)
        {
            all.Encapsulate(r.bounds);
            Transform rt = new GameObject("Room_" + r.label).transform;
            Undo.RegisterCreatedObjectUndo(rt.gameObject, "Auto Sound");
            rt.SetParent(root, false);
            rt.position = new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z);

            // บรรยากาศห้อง
            made += Zone(rt, r.bounds, r.hall && wind != null ? wind : roomTone, drone, r.hall ? 0.3f : 0.22f);

            // ไม้ลั่นสุ่ม (ทุกห้อง)
            float roomSize = Mathf.Max(r.bounds.size.x, r.bounds.size.z);
            made += Emitter(rt, r.bounds.center, creaks, SoundEmitter.Mode.Random, 0.45f, 2f, 12f, new Vector2(18f, 45f), roomSize * 0.35f, SoundEmitter.When.Always);

            string l = r.label.ToLower();
            if (r.hall)
            {
                made += Emitter(rt, r.bounds.center, steps, SoundEmitter.Mode.Random, 0.4f, 4f, 20f, new Vector2(40f, 90f), roomSize * 0.4f, SoundEmitter.When.RealOnly);
                if (cry != null)
                    made += Emitter(rt, r.bounds.center, new[] { cry }, SoundEmitter.Mode.Random, 0.5f, 3f, 16f, new Vector2(35f, 70f), roomSize * 0.4f, SoundEmitter.When.PtsdOnly);
            }
            else if (l.Contains("kitchen") || l.Contains("bath") || l.Contains("maid") || l.Contains("laundry"))
            {
                made += Emitter(rt, r.bounds.center, drips, SoundEmitter.Mode.Random, 0.5f, 1.5f, 8f, new Vector2(3f, 10f), roomSize * 0.25f, SoundEmitter.When.Always);
            }
            else if (l.Contains("living") || l.Contains("library") || l.Contains("office") || l.Contains("dining") || l.Contains("doc"))
            {
                AudioClip c = l.Contains("living") && vinyl != null ? vinyl : tick;
                if (c != null) made += Emitter(rt, r.bounds.center, new[] { c }, SoundEmitter.Mode.Loop, 0.35f, 1.5f, 7f, Vector2.one, 0f, SoundEmitter.When.RealOnly);
            }
        }

        // ทั้งบ้าน: ฝน + ฟ้าร้อง
        Transform house = new GameObject("WholeHouse").transform;
        Undo.RegisterCreatedObjectUndo(house.gameObject, "Auto Sound");
        house.SetParent(root, false);
        if (rain != null) made += Zone(house, all, rain, null, 0.12f, keepRealInPtsd: true);
        made += Emitter(house, all.center, thunder, SoundEmitter.Mode.Random, 0.55f, 500f, 600f, new Vector2(45f, 120f), 0f, SoundEmitter.When.Always, pan: 0.1f);

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root.gameObject;
        EditorGUIUtility.PingObject(root.gameObject);
        Debug.Log($"[Auto Sound] สร้างเสียง {made} ชิ้น ใน {rooms.Count} พื้นที่ ← {AutoRoot}");
    }

    // ───────── หาห้อง ─────────
    private static List<Room> FindRooms()
    {
        // ป้ายชื่อห้อง: กลุ่ม Furniture_xxx
        var labels = new List<(string name, Vector3 center)>();
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (g.name.StartsWith("Furniture_") && g.transform.childCount > 0)
                labels.Add((g.name.Substring("Furniture_".Length), MapBuilderOps.WorldBounds(g).center));

        var rooms = new List<Room>();
        foreach (RoomWallFade f in Object.FindObjectsByType<RoomWallFade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            BoxCollider bc = f.GetComponent<BoxCollider>();
            if (bc == null) continue;
            Vector3 c = f.transform.TransformPoint(bc.center);
            Vector3 s = Vector3.Scale(bc.size, f.transform.lossyScale);
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (s.x * s.z < 4f) continue;   // ช่องเล็กๆ (ประตู/ซอก) ไม่ต้องมีเสียงแยก
            var b = new Bounds(new Vector3(c.x, c.y - s.y * 0.5f + 1.5f, c.z), new Vector3(s.x, 3f, s.z));

            // ซ้อนกับห้องที่มีแล้วเกือบทั้งหมด → ข้าม (บางห้องมี Trigger 2 อันซ้อนกัน)
            bool dup = false;
            foreach (Room r in rooms) if (OverlapRatio(r.bounds, b) > 0.6f) { dup = true; break; }
            if (dup) continue;

            bool hall = Mathf.Max(s.x, s.z) / Mathf.Max(0.1f, Mathf.Min(s.x, s.z)) > 3f
                        || (f.transform.parent != null && f.transform.parent.name.Contains("Main"));
            string label = hall ? "Hall" : "Room";
            foreach (var lb in labels)
                if (!hall && Inside(b, lb.center)) { label = lb.name; break; }
            rooms.Add(new Room { label = label + "_" + (rooms.Count + 1).ToString("00"), bounds = b, hall = hall });
        }
        return rooms;
    }

    private static bool Inside(Bounds b, Vector3 p) => p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;

    private static float OverlapRatio(Bounds a, Bounds b)
    {
        float ox = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
        float oz = Mathf.Max(0f, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
        float inter = ox * oz;
        float small = Mathf.Min(a.size.x * a.size.z, b.size.x * b.size.z);
        return small > 0f ? inter / small : 0f;
    }

    // ───────── สร้าง ─────────
    private static int Zone(Transform parent, Bounds b, AudioClip real, AudioClip ptsd, float vol, bool keepRealInPtsd = false)
    {
        if (real == null) return 0;
        var s = new MapBuilderSound.Settings { kind = MapBuilderSound.Kind.Zone, volume = vol, ptsdClip = ptsd, zoneHeight = 3f };
        s.clips.Add(real);
        AmbientZone z = MapBuilderSound.CreateZone(b.min, b.max, b.min.y, s);
        if (z == null) return 0;
        z.keepRealInPtsd = keepRealInPtsd || ptsd == null;
        Undo.SetTransformParent(z.transform, parent, "Auto Sound");
        return 1;
    }

    private static int Emitter(Transform parent, Vector3 pos, AudioClip[] clips, SoundEmitter.Mode mode, float vol, float full, float max,
                               Vector2 interval, float scatter, SoundEmitter.When when, float pan = 0.6f)
    {
        if (clips == null || clips.Length == 0) return 0;
        var s = new MapBuilderSound.Settings
        {
            kind = mode == SoundEmitter.Mode.Loop ? MapBuilderSound.Kind.Loop : MapBuilderSound.Kind.Random,
            volume = vol, fullDistance = full, maxDistance = max, pan = pan, interval = interval, scatter = scatter, when = when
        };
        s.clips.AddRange(clips);
        SoundEmitter e = MapBuilderSound.CreateEmitter(new Vector3(pos.x, pos.y - 1.5f, pos.z), s);
        Undo.SetTransformParent(e.transform, parent, "Auto Sound");
        return 1;
    }

    private static AudioClip Clip(string name)
    {
        foreach (string g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    private static AudioClip[] Clips(string prefix)
    {
        var list = new List<AudioClip>();
        foreach (string g in AssetDatabase.FindAssets(prefix + " t:AudioClip"))
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null && c.name.StartsWith(prefix) && !list.Contains(c)) list.Add(c);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return list.ToArray();
    }
}
