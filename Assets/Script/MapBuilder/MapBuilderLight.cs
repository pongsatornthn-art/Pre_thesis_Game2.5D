using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Map Builder] แสงจากหน้าต่าง — สร้าง WindowLightRig จากจุดที่คลิกบนผนัง + สร้างลายกรอบหน้าต่าง (cookie) + วัสดุลำแสง
/// </summary>
public static class MapBuilderLight
{
    public const string CookieDir = "Assets/Art/Lighting/Cookies";
    public const string ShaftMatPath = "Assets/Art/Lighting/Materials/M_LightShaft.mat";
    public const string ShaftShaderName = "MapBuilder/LightShaft";

    public class Preset
    {
        public string name;
        public Color color;
        public float intensity, downAngle, opacity;
        public Preset(string n, Color c, float i, float down, float op) { name = n; color = c; intensity = i; downAngle = down; opacity = op; }
    }

    public static readonly Preset[] Presets =
    {
        new Preset("แสงจันทร์ (ฟ้าเย็น)", new Color(0.55f, 0.68f, 1f), 6f, 40f, 0.18f),
        new Preset("จันทร์จางๆ (หลอน)",   new Color(0.42f, 0.5f, 0.75f), 3.5f, 50f, 0.12f),
        new Preset("ไฟถนนส้ม",           new Color(1f, 0.62f, 0.32f), 5f, 30f, 0.14f),
        new Preset("ฟ้าแลบ/ขาวซีด",       new Color(0.85f, 0.9f, 1f), 9f, 35f, 0.22f),
    };

    public class Settings
    {
        public int preset;
        public Vector2 windowSize = new Vector2(1.2f, 1.6f);
        public float sideAngle;
        public int cookieCols = 2, cookieRows = 2;
        public bool useCookie = true;
        public bool castShadows;
        public bool shaft = true;
        public bool clouds = true;
        public WindowLightRig.PtsdLook inPtsd = WindowLightRig.PtsdLook.Red;
        public bool addDust = true;
    }

    public const string DustPrefabPath = "Assets/Art/VFX/RealDust/Real_DustMotes.prefab";

    // ───────── วัสดุลำแสง ─────────
    public static Material GetOrCreateShaftMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ShaftMatPath);
        if (m != null) return m;
        Shader sh = Shader.Find(ShaftShaderName);
        if (sh == null)
        {
            Debug.LogError("[Map Builder] ไม่เจอ shader \"MapBuilder/LightShaft\" — วางไฟล์ LightShaft.shader ไว้ในโปรเจกต์ก่อน");
            return null;
        }
        EnsureFolder(Path.GetDirectoryName(ShaftMatPath).Replace('\\', '/'));
        m = new Material(sh) { name = "M_LightShaft" };
        AssetDatabase.CreateAsset(m, ShaftMatPath);
        AssetDatabase.SaveAssets();
        return m;
    }

    // ───────── ลายกรอบหน้าต่าง (cookie) ─────────
    /// <summary>สร้าง/โหลดรูปลายหน้าต่าง cols × rows ช่อง (ขาว = แสงผ่าน · ดำ = กรอบ)</summary>
    public static Texture2D GetOrCreateCookie(int cols, int rows, Vector2 windowSize)
    {
        cols = Mathf.Clamp(cols, 1, 6);
        rows = Mathf.Clamp(rows, 1, 6);
        // หน้าต่างไม่จัตุรัส → วางช่องหน้าต่างกลางรูปตามสัดส่วน ที่เหลือดำ (กรวยไฟเป็นจัตุรัสเสมอ)
        float m = Mathf.Max(windowSize.x, windowSize.y, 0.01f);
        float fw = Mathf.Clamp01(windowSize.x / m), fh = Mathf.Clamp01(windowSize.y / m);
        int aw = Mathf.RoundToInt(fw * 10f), ah = Mathf.RoundToInt(fh * 10f);
        string path = $"{CookieDir}/WindowCookie_{cols}x{rows}_{aw}-{ah}.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int S = 256;
        float frame = 0.045f;    // ความหนากรอบนอก (สัดส่วน)
        float bar = 0.03f;       // ความหนาซี่กลาง
        float soft = 0.012f;     // ขอบนุ่ม
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = ((x + 0.5f) / S - 0.5f) / fw + 0.5f, v = ((y + 0.5f) / S - 0.5f) / fh + 0.5f;
                // ระยะถึงกรอบนอก
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) - frame;
                // ระยะถึงซี่ในแนวตั้ง/นอน
                for (int c = 1; c < cols; c++) d = Mathf.Min(d, Mathf.Abs(u - c / (float)cols) - bar * 0.5f);
                for (int r = 1; r < rows; r++) d = Mathf.Min(d, Mathf.Abs(v - r / (float)rows) - bar * 0.5f);
                float a = (u < 0f || u > 1f || v < 0f || v > 1f) ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / soft));
                px[y * S + x] = new Color(a, a, a, a);
            }
        tex.SetPixels(px);
        tex.Apply();

        EnsureFolder(CookieDir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Cookie;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────── สร้าง ─────────
    /// <summary>point = จุดบนผนังตรงกลางหน้าต่าง · intoRoom = ทิศเข้าห้อง (normal ของผนังด้านในห้อง)</summary>
    public static WindowLightRig Create(Vector3 point, Vector3 intoRoom, Settings s)
    {
        Vector3 n = new Vector3(intoRoom.x, 0f, intoRoom.z);
        if (n.sqrMagnitude < 0.0001f) n = Vector3.back;
        n.Normalize();
        Preset p = Presets[Mathf.Clamp(s.preset, 0, Presets.Length - 1)];

        Transform parent = MapBuilderOps.GetOrCreatePath("[MAP]/WindowLights");
        GameObject go = new GameObject("WindowLight_" + (parent.childCount + 1).ToString("00"));
        Undo.RegisterCreatedObjectUndo(go, "Map Builder: Window Light");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(point + n * 0.03f, Quaternion.LookRotation(n, Vector3.up));

        WindowLightRig rig = go.AddComponent<WindowLightRig>();
        rig.windowSize = s.windowSize;
        rig.color = p.color;
        rig.intensity = p.intensity;
        rig.downAngle = p.downAngle;
        rig.sideAngle = s.sideAngle;
        rig.shaftOpacity = p.opacity;
        rig.cookie = s.useCookie ? GetOrCreateCookie(s.cookieCols, s.cookieRows, s.windowSize) : null;
        rig.castShadows = s.castShadows;
        rig.shaft = s.shaft;
        rig.shaftMaterial = GetOrCreateShaftMaterial();
        rig.clouds = s.clouds;
        rig.inPtsd = s.inPtsd;
        rig.shaftLength = MeasureToFloor(go.transform, rig);
        rig.reach = rig.shaftLength + 2f;
        rig.Rebuild();

        if (s.addDust)
        {
            GameObject dust = AssetDatabase.LoadAssetAtPath<GameObject>(DustPrefabPath);
            if (dust != null)
            {
                GameObject d = (GameObject)PrefabUtility.InstantiatePrefab(dust, go.scene);
                Undo.RegisterCreatedObjectUndo(d, "Map Builder: Window Light");
                d.transform.SetParent(go.transform, false);
                d.transform.localPosition = rig.LocalDir * (rig.shaftLength * 0.5f);
            }
        }

        Selection.activeGameObject = go;
        return rig;
    }

    /// <summary>ยิงตามทิศแสงจากกลางหน้าต่างไปหาพื้น → ความยาวลำแสง</summary>
    public static float MeasureToFloor(Transform t, WindowLightRig rig)
    {
        Vector3 dir = t.TransformDirection(rig.LocalDir);
        Physics.SyncTransforms();
        foreach (RaycastHit h in Physics.RaycastAll(t.position + dir * 0.1f, dir, 30f, ~0, QueryTriggerInteraction.Ignore))
            if (h.normal.y > 0.5f) return Mathf.Clamp(h.distance + 0.1f, 0.5f, 30f);
        // ไม่มีพื้นที่มี Collider → ใช้ความสูงพื้นสำรอง
        float height = t.position.y - MapBuilderOps.GroundY;
        float sin = Mathf.Max(0.1f, -dir.y);
        return Mathf.Clamp(height / sin, 0.5f, 30f);
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
