using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// [Map Builder] เสียง: หา/ฟังตัวอย่างคลิป + สร้างจุดเสียง (SoundEmitter) และโซนบรรยากาศ (AmbientZone)
/// </summary>
public static class MapBuilderSound
{
    public const string DefaultSoundFolders = "Assets/Audio";
    public const string MixerPath = "Assets/Audio/MainMixer.mixer";

    public enum Kind { Loop, Random, Zone }
    public static readonly string[] KindNames = { "จุดเสียงวนลูป", "เสียงสุ่มเป็นระยะ", "เสียงบรรยากาศห้อง (โซน)" };
    public static readonly string[] KindHelp =
    {
        "เสียงดังตลอดที่จุดเดียว — นาฬิกา น้ำหยด วิทยุซ่า ไฟกระพริบ",
        "สุ่มเล่นทุกๆ กี่วินาที — ไม้ลั่น เสียงกระซิบ ฝีเท้าชั้นบน ประตูเอี๊ยด (ใส่ได้หลายคลิป)",
        "เดินเข้าห้องแล้วค่อยๆ ดัง ออกแล้วค่อยๆ เบา — ลมพัด ฝนตก เสียงฮัมของบ้าน",
    };

    public class Settings
    {
        public Kind kind = Kind.Loop;
        public List<AudioClip> clips = new List<AudioClip>();
        public AudioClip ptsdClip;
        public float volume = 0.8f;
        public float fullDistance = 2f;
        public float maxDistance = 10f;
        public float pan = 0.6f;
        public Vector2 interval = new Vector2(8f, 20f);
        public float scatter = 1.5f;
        public SoundEmitter.When when = SoundEmitter.When.Always;
        public bool useMixer = true;
        public string mixerGroup = "SFX";
        public float zoneHeight = 3f;
    }

    // ───────── หาคลิป ─────────
    public static List<AudioClip> FindClips(string folder, string search)
    {
        var list = new List<AudioClip>();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        foreach (string g in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
            if (c == null) continue;
            if (!string.IsNullOrEmpty(search) && c.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            list.Add(c);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static AudioMixerGroup FindGroup(string name)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MixerPath))
            if (o is AudioMixerGroup g && g.name == name) return g;
        return null;
    }

    // ───────── ฟังตัวอย่างใน Editor (ไม่ต้องกด Play) ─────────
    private static System.Type AudioUtil => typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    public static void Preview(AudioClip clip)
    {
        StopPreview();
        if (clip == null || AudioUtil == null) return;
        MethodInfo m = AudioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                                           new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (m != null) m.Invoke(null, new object[] { clip, 0, false });
        else Debug.LogWarning("[Map Builder] Unity รุ่นนี้ไม่รองรับฟังตัวอย่าง — ดับเบิลคลิกไฟล์เสียงใน Project แทน");
    }

    public static void StopPreview()
    {
        MethodInfo m = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        if (m != null) m.Invoke(null, null);
    }

    public static bool IsPreviewing()
    {
        MethodInfo m = AudioUtil?.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public);
        return m != null && m.GetParameters().Length == 0 && (bool)m.Invoke(null, null);
    }

    // ───────── สร้าง ─────────
    private static string ShortName(Settings s)
    {
        string n = s.clips.Count > 0 && s.clips[0] != null ? s.clips[0].name : "Sound";
        foreach (char c in "&+ ") n = n.Replace(c, '_');
        return n;
    }

    public static SoundEmitter CreateEmitter(Vector3 pos, Settings s)
    {
        Transform parent = MapBuilderOps.GetOrCreatePath("[MAP]/Sounds");
        GameObject go = new GameObject((s.kind == Kind.Loop ? "SND_Loop_" : "SND_Random_") + ShortName(s));
        Undo.RegisterCreatedObjectUndo(go, "Map Builder: Sound");
        go.transform.SetParent(parent, false);
        go.transform.position = pos + Vector3.up * 1f;   // สูงระดับหู

        SoundEmitter e = go.AddComponent<SoundEmitter>();
        e.mode = s.kind == Kind.Random ? SoundEmitter.Mode.Random : SoundEmitter.Mode.Loop;
        e.clips = s.clips.FindAll(c => c != null).ToArray();
        e.ptsdClip = s.kind == Kind.Loop ? s.ptsdClip : null;
        e.volume = s.volume;
        e.fullVolumeDistance = s.fullDistance;
        e.maxDistance = Mathf.Max(s.fullDistance + 0.1f, s.maxDistance);
        e.pan = s.pan;
        e.interval = s.interval;
        e.scatterRadius = s.kind == Kind.Random ? s.scatter : 0f;
        e.playWhen = s.when;
        if (s.useMixer) e.output = FindGroup(s.mixerGroup);
        Selection.activeGameObject = go;
        return e;
    }

    /// <summary>โซนจากมุม a ถึง b (XZ) ที่ความสูงพื้น y</summary>
    public static AmbientZone CreateZone(Vector3 a, Vector3 b, float y, Settings s)
    {
        float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
        float minZ = Mathf.Min(a.z, b.z), maxZ = Mathf.Max(a.z, b.z);
        if (maxX - minX < 0.2f || maxZ - minZ < 0.2f) return null;

        Transform parent = MapBuilderOps.GetOrCreatePath("[MAP]/Sounds");
        GameObject go = new GameObject("SND_Zone_" + ShortName(s));
        Undo.RegisterCreatedObjectUndo(go, "Map Builder: Ambient Zone");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f);
        go.layer = LayerMask.NameToLayer("Ignore Raycast");

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(maxX - minX, s.zoneHeight, maxZ - minZ);
        bc.center = new Vector3(0f, s.zoneHeight * 0.5f, 0f);

        AmbientZone z = go.AddComponent<AmbientZone>();
        z.realClip = s.clips.Count > 0 ? s.clips[0] : null;
        z.ptsdClip = s.ptsdClip;
        z.volume = s.volume;
        if (s.useMixer) z.output = FindGroup(s.mixerGroup);
        Selection.activeGameObject = go;
        return z;
    }

    public static AmbientZone CreateZoneFromSelection(Transform[] sel, Settings s)
    {
        var list = MapBuilderOps.TopLevel(sel);
        if (list.Count == 0) return null;
        Bounds b = MapBuilderOps.WorldBounds(list);
        return CreateZone(b.min, b.max, b.min.y, s);
    }
}
