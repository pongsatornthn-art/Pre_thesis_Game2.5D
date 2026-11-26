using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Map Builder] สร้างพื้นห้อง — สี่เหลี่ยมพื้นพร้อมลายกระเบื้องต่อกันเนียน (UV ตามพิกัดโลก → พื้น 2 ชิ้นติดกันลายไม่เหลื่อม)
///   • ลากวาดในฉาก หรือ สร้างจากกรอบของที่เลือก (เช่น เลือกกำแพงรอบห้อง)
///   • ใส่ Box Collider บางๆ ให้เดินได้ · เก็บ Mesh เป็นไฟล์ใน Assets/Map/Floors (เปิดซีนใหม่ไม่หาย)
/// </summary>
public static class MapBuilderFloor
{
    public const string FloorMatFolder = "Assets/Map/Wall/Wall_Assets/New_Wall/floor";
    public const string MeshFolder = "Assets/Map/Floors";

    public class Settings
    {
        public string floorName = "Floor";
        public Material material;
        [Tooltip("ลาย 1 รอบกว้างกี่เมตร (พื้นเดิมในแมพ ≈ 2)")]
        public float tileSize = 2f;
        public bool collider = true;
        public float yOffset = 0.01f;     // ยกนิดนึง กัน z-fight กับพื้นเดิม
        public float inset = 0f;
        public Transform parent;
    }

    public static List<Material> FindFloorMaterials()
    {
        var list = new List<Material>();
        if (AssetDatabase.IsValidFolder(FloorMatFolder))
            foreach (string g in AssetDatabase.FindAssets("t:Material", new[] { FloorMatFolder }))
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) list.Add(m);
            }
        Material tile = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Lighting/Materials/M_TileFloor_Lit.mat");
        if (tile != null && !list.Contains(tile)) list.Add(tile);
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    /// <summary>สร้างพื้นสี่เหลี่ยมจากมุม a ถึงมุม b (ใช้แค่ XZ) ที่ความสูง y</summary>
    public static GameObject Create(Vector3 a, Vector3 b, float y, Settings s)
    {
        float minX = Mathf.Min(a.x, b.x) + s.inset, maxX = Mathf.Max(a.x, b.x) - s.inset;
        float minZ = Mathf.Min(a.z, b.z) + s.inset, maxZ = Mathf.Max(a.z, b.z) - s.inset;
        if (maxX - minX < 0.05f || maxZ - minZ < 0.05f) return null;

        Vector3 center = new Vector3((minX + maxX) * 0.5f, y + s.yOffset, (minZ + maxZ) * 0.5f);
        float hx = (maxX - minX) * 0.5f, hz = (maxZ - minZ) * 0.5f;
        float tile = Mathf.Max(0.05f, s.tileSize);

        Mesh mesh = new Mesh { name = s.floorName };
        mesh.vertices = new[]
        {
            new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz)
        };
        mesh.uv = new[]
        {
            new Vector2(minX / tile, minZ / tile), new Vector2(minX / tile, maxZ / tile),
            new Vector2(maxX / tile, maxZ / tile), new Vector2(maxX / tile, minZ / tile)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
        mesh.RecalculateBounds();

        MapBuilderLight.EnsureFolder(MeshFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{Safe(s.floorName)}.asset");
        AssetDatabase.CreateAsset(mesh, path);

        GameObject go = new GameObject(s.floorName);
        Undo.RegisterCreatedObjectUndo(go, "Map Builder: Floor");
        Transform parent = s.parent != null ? s.parent : MapBuilderOps.GetOrCreatePath("[MAP]/Floors");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (s.material != null) mr.sharedMaterial = s.material;
        if (s.collider)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(hx * 2f, 0.1f, hz * 2f);
            bc.center = new Vector3(0, -0.05f, 0);
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        Selection.activeGameObject = go;
        return go;
    }

    /// <summary>สร้างพื้นจากกรอบรวมของที่เลือก (เช่น กำแพงรอบห้อง) ที่ระดับฐานของของพวกนั้น</summary>
    public static GameObject CreateFromSelection(Transform[] sel, Settings s)
    {
        var list = MapBuilderOps.TopLevel(sel);
        if (list.Count == 0) return null;
        Bounds b = MapBuilderOps.WorldBounds(list);
        return Create(b.min, b.max, b.min.y, s);
    }

    public static void SetMaterial(Transform[] sel, Material m)
    {
        if (m == null) return;
        foreach (Transform t in sel)
        {
            MeshRenderer r = t.GetComponent<MeshRenderer>();
            if (r == null) continue;
            Undo.RecordObject(r, "Map Builder: Floor Material");
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats.Length > 0 ? mats : new[] { m };
        }
    }

    private static string Safe(string n)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return string.IsNullOrWhiteSpace(n) ? "Floor" : n;
    }
}
