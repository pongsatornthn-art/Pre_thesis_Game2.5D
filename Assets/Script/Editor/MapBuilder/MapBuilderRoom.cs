using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Map Builder] สร้าง "ชุดห้อง" ให้ครบในคลิกเดียว — แบบเดียวกับที่ทำมือในซีน (RoomA_Trigger1 + BOXBlur):
///   Room_ชื่อ
///     ├─ Trigger   (BoxCollider isTrigger + RoomWallFade = ซ่อนกำแพงฝั่งกล้อง + RoomFader = เฟดกล่องเบลอ)
///     └─ BOXBlur   (Cube + Mat_BoxBlur คลุมทั้งห้อง ไม่มี Collider)
/// ขนาดห้องคิดจากของที่เลือก (พื้น/กำแพง/เฟอร์นิเจอร์ของห้องนั้น)
/// </summary>
public static class MapBuilderRoom
{
    public class Settings
    {
        public string roomName = "Room";
        public float inset = 0.3f;            // หด trigger เข้ามาจากขอบห้อง (เดินชิดผนังจะได้ไม่หลุดออกจาก trigger ข้างๆ)
        public float triggerHeight = 2.58f;   // เท่ากับ trigger เดิมในซีน
        public float blurHeight = 3.6f;       // เท่ากับ BOXBlur เดิมในซีน
        public float blurPadding = 0.1f;      // ขยายกล่องเบลอออกนิดนึง กันขอบห้องโผล่
        public bool makeBlur = true;
        public bool autoWalls = true;
        public Material blurMaterial;
        public Transform parent;
    }

    public const string DefaultBlurMat = "Assets/Art/Materials/Mat_BoxBlur.mat";

    /// <summary>ทิศจากห้องไปหากล้องหลัก (บนพื้น XZ) — ไม่มีกล้อง = ฝั่ง -Z</summary>
    public static Vector3 CameraSide(Vector3 roomCenter)
    {
        Camera cam = null;
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.CompareTag("MainCamera")) { cam = c; break; }
        Vector3 d = cam != null ? cam.transform.position - roomCenter : Vector3.back;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return Vector3.back;
        // ปัดเป็นแกนหลัก (ห้องในเกมวางตามแกนโลก)
        return Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
    }

    /// <summary>หากำแพงฝั่งที่หันหากล้อง จาก Renderer ที่เลือก: บางตามทิศกล้อง + ยาวตามขวาง + อยู่ค่อนไปทางกล้อง</summary>
    public static List<MeshRenderer> FindCameraSideWalls(IEnumerable<Transform> ts, Bounds room, Vector3 side)
    {
        var result = new List<MeshRenderer>();
        int ax = Mathf.Abs(side.x) > 0.5f ? 0 : 2;     // แกนที่หันหากล้อง
        int across = ax == 0 ? 2 : 0;
        float halfDepth = room.extents[ax];
        foreach (Transform t in ts)
        {
            foreach (MeshRenderer r in t.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds b = r.bounds;
                bool thin = b.size[ax] < 1.2f;
                bool wide = b.size[across] > 1.0f;
                bool tall = b.size.y > 1.0f;
                float offset = (b.center[ax] - room.center[ax]) * Mathf.Sign(side[ax]);
                bool cameraSide = offset > halfDepth * 0.5f;
                bool looksLikeWall = r.gameObject.layer == LayerMask.NameToLayer("Wall")
                                     || r.name.ToLower().Contains("wall") || (thin && wide && tall);
                if (thin && wide && tall && cameraSide && looksLikeWall && !result.Contains(r)) result.Add(r);
            }
        }
        return result;
    }

    public static GameObject Build(Transform[] selection, Settings s)
    {
        var list = MapBuilderOps.TopLevel(selection);
        if (list.Count == 0) { EditorUtility.DisplayDialog("Map Builder", "เลือกพื้น/กำแพงของห้องก่อน (เลือกหลายชิ้นได้)", "ตกลง"); return null; }
        Bounds room = MapBuilderOps.WorldBounds(list);
        if (room.size.x < 0.5f || room.size.z < 0.5f)
        {
            EditorUtility.DisplayDialog("Map Builder", "ของที่เลือกเล็กเกินไป — เลือกพื้นหรือกำแพงที่ล้อมห้องทั้งห้อง", "ตกลง");
            return null;
        }
        float floorY = room.min.y;
        Vector3 c = new Vector3(room.center.x, floorY, room.center.z);
        string n = string.IsNullOrWhiteSpace(s.roomName) ? "Room" : s.roomName.Trim();

        GameObject root = new GameObject("Room_" + n);
        Undo.RegisterCreatedObjectUndo(root, "Map Builder: Create Room");
        if (s.parent != null) root.transform.SetParent(s.parent, false);
        root.transform.position = c;

        // ── Trigger ──
        GameObject trig = new GameObject("Trigger_" + n);
        Undo.RegisterCreatedObjectUndo(trig, "Map Builder: Create Room");
        trig.transform.SetParent(root.transform, false);
        trig.transform.position = c;
        BoxCollider bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(Mathf.Max(0.2f, room.size.x - s.inset * 2f), s.triggerHeight, Mathf.Max(0.2f, room.size.z - s.inset * 2f));
        bc.center = new Vector3(0f, s.triggerHeight * 0.5f, 0f);

        RoomWallFade fade = trig.AddComponent<RoomWallFade>();
        fade.wallsToHide = new List<MeshRenderer>();
        if (s.autoWalls)
            fade.wallsToHide.AddRange(FindCameraSideWalls(list, room, CameraSide(room.center)));

        // ── กล่องเบลอ ──
        if (s.makeBlur)
        {
            GameObject blur = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(blur, "Map Builder: Create Room");
            blur.name = "BOXBlur_" + n;
            Object.DestroyImmediate(blur.GetComponent<Collider>());
            blur.transform.SetParent(root.transform, false);
            blur.transform.position = c + Vector3.up * (s.blurHeight * 0.5f + 0.07f);
            blur.transform.localScale = new Vector3(room.size.x + s.blurPadding * 2f, s.blurHeight, room.size.z + s.blurPadding * 2f);
            MeshRenderer mr = blur.GetComponent<MeshRenderer>();
            Material mat = s.blurMaterial != null ? s.blurMaterial : AssetDatabase.LoadAssetAtPath<Material>(DefaultBlurMat);
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            RoomFader fader = trig.AddComponent<RoomFader>();
            fader.blurPlanes = new List<MeshRenderer> { mr };
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        return root;
    }

    /// <summary>เพิ่ม Renderer ที่เลือกเข้าไปในรายการกำแพงที่ซ่อนของห้อง</summary>
    public static int AddWalls(RoomWallFade fade, Transform[] selection)
    {
        if (fade == null) return 0;
        Undo.RecordObject(fade, "Map Builder: Add Walls");
        if (fade.wallsToHide == null) fade.wallsToHide = new List<MeshRenderer>();
        int added = 0;
        foreach (Transform t in selection)
            foreach (MeshRenderer r in t.GetComponentsInChildren<MeshRenderer>())
                if (!fade.wallsToHide.Contains(r)) { fade.wallsToHide.Add(r); added++; }
        EditorUtility.SetDirty(fade);
        return added;
    }
}
