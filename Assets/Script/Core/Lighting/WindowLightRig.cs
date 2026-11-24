using UnityEngine;

/// <summary>
/// [Map Builder] แสงจากหน้าต่าง (แสงจันทร์ลอดหน้าต่าง) — 1 ตัว = 1 หน้าต่าง
///   • Spot Light ส่องเฉียงลงพื้น + ลายกรอบหน้าต่าง (cookie) → เกิดแสงเป็นรูปช่องหน้าต่างบนพื้น
///   • ลำแสงฟุ้ง (Shaft) แบบโปร่งบวกแสง มองเห็นเป็นลำจากหน้าต่างลงพื้น
///   • เมฆผ่าน (ความสว่างไหวช้าๆ) · โลก PTSD: เหมือนเดิม / แดง / ดับ
///
/// สร้างจาก Tools > Map Builder > แท็บ "แสง" (คลิกที่ผนังตรงหน้าต่าง) — ปรับค่าทีหลังใน Inspector ได้ เห็นผลทันทีใน Scene
/// แกนของตัวนี้: Z (ฟ้า) = ทิศเข้าห้อง · จุดกำเนิด = กลางหน้าต่าง
/// ไม่อยู่ใต้กลุ่ม Lights ของ WorldLighting → ไม่โดนย้อมแดง/กระพริบตามบอส (ตั้งเองที่ "โลก PTSD")
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class WindowLightRig : MonoBehaviour
{
    public enum PtsdLook { Same, Red, Off }
    public enum ShaftStyle { Solid, Rays, Volume }

    [Header("หน้าต่าง")]
    [Tooltip("ขนาดช่องหน้าต่าง กว้าง × สูง (เมตร)")]
    public Vector2 windowSize = new Vector2(1.2f, 1.6f);

    [Header("ทิศแสง")]
    [Tooltip("ก้มลงกี่องศา (0 = ส่องตรงเข้าห้อง · 60 = ตกลงพื้นใกล้ผนัง)")]
    [Range(5f, 80f)] public float downAngle = 40f;
    [Tooltip("เฉียงซ้าย/ขวากี่องศา (จันทร์อยู่ด้านข้าง)")]
    [Range(-60f, 60f)] public float sideAngle = 0f;

    [Header("แสง")]
    public Color color = new Color(0.55f, 0.68f, 1f);
    [Min(0f)] public float intensity = 6f;
    [Tooltip("แสงส่องไปไกลแค่ไหนหลังหน้าต่าง (เมตร)")]
    [Min(0.5f)] public float reach = 8f;
    [Tooltip("ลายกรอบหน้าต่าง (สร้างได้จากแท็บแสง) — เว้นว่าง = แสงเป็นวงเรียบ")]
    public Texture cookie;
    [Tooltip("เปิด = ผนัง/เฟอร์นิเจอร์บังแสงเป็นเงา (ต้องมีรูหน้าต่างจริงในโมเดล ไม่งั้นผนังบังหมด)\nปิด = แสงทะลุผนัง ใช้ลาย cookie เป็นกรอบแทน")]
    public bool castShadows = false;

    [Header("ลำแสงฟุ้ง")]
    public bool shaft = true;
    [Tooltip("Solid = ลำแสงก้อนเดียว · Rays = แสงเป็นเส้นๆ หลายเส้น (ลอดร่อง/ไม้กระดาน)\nVolume = แสงฟุ้งเป็นริ้วตามลายกรอบหน้าต่าง (แบบ volumetric) — ต้องมี Cookie")]
    public ShaftStyle shaftStyle = ShaftStyle.Volume;
    [Tooltip("โหมด Volume: จำนวนแผ่นตัดในลำแสง (มาก = เนียน ริ้วชัด)")]
    [Range(4, 40)] public int volumeSlices = 14;
    [Tooltip("จำนวนเส้นแสง (โหมด Rays)")]
    [Range(1, 40)] public int rayCount = 7;
    [Tooltip("ความกว้างเส้นแสง (เมตร) — สุ่ม ±50% ต่อเส้น")]
    [Range(0.01f, 0.6f)] public float rayWidth = 0.1f;
    [Tooltip("เรียงเส้นแนวตั้ง (เหมือนแสงลอดร่องไม้กระดาน) · ปิด = กระจายสุ่มทั่วหน้าต่าง")]
    public bool raysAsSlits = true;
    [Tooltip("แต่ละเส้นสว่างขึ้นลงไม่พร้อมกัน")]
    [Range(0f, 1f)] public float rayFlicker = 0.35f;
    [Tooltip("เปลี่ยนเลขเพื่อสุ่มตำแหน่งเส้นใหม่")]
    public int raySeed = 1;
    [Tooltip("ความยาวลำแสง (เมตร) — ตอนสร้างจากเครื่องมือจะคำนวณให้ยาวถึงพื้นพอดี")]
    [Min(0.1f)] public float shaftLength = 3f;
    [Range(0f, 1f)] public float shaftOpacity = 0.18f;
    [Tooltip("ขอบลำแสงนุ่มแค่ไหน")]
    [Range(0.01f, 0.5f)] public float shaftSoftness = 0.3f;
    [Tooltip("ปลายลำแสงบานออกต่อเมตร")]
    [Range(0f, 0.3f)] public float shaftSpread = 0.05f;
    public Material shaftMaterial;

    [Header("เมฆผ่าน")]
    public bool clouds = true;
    [Range(0f, 0.8f)] public float cloudAmount = 0.25f;
    [Min(0.01f)] public float cloudSpeed = 0.15f;

    [Header("โลก PTSD")]
    public PtsdLook inPtsd = PtsdLook.Red;
    public Color ptsdRed = new Color(1f, 0.12f, 0.08f);
    [Min(0f)] public float ptsdIntensityMul = 0.8f;
    [Tooltip("วินาทีที่ค่อยๆ เปลี่ยนตอนเข้า/ออก PTSD")]
    [Min(0f)] public float ptsdFade = 1.5f;

    private const float LightBack = 0.6f;   // ไฟอยู่หลังหน้าต่างเท่านี้ (นอกห้อง)

    private Light lightComp;
    private MeshFilter shaftFilter;
    private MeshRenderer shaftRenderer;
    private Mesh shaftMesh;
    private MaterialPropertyBlock mpb;
    private float ptsdT, ptsdTarget;
    private float seed;

    // ───────── ประกอบชิ้นส่วน ─────────
    private void OnEnable()
    {
        seed = Random.value * 100f;
        EnsureParts();
        Rebuild();
        if (Application.isPlaying)
        {
            PTSDManager.OnPTSDStateChanged += HandlePtsd;
            bool inP = PTSDManager.Instance != null && PTSDManager.Instance.currentMode != PTSDMode.None;
            ptsdT = ptsdTarget = inP ? 1f : 0f;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying) PTSDManager.OnPTSDStateChanged -= HandlePtsd;
    }

    private void OnDestroy()
    {
        if (shaftMesh != null)
        {
            if (Application.isPlaying) Destroy(shaftMesh); else DestroyImmediate(shaftMesh);
        }
    }

    private void OnValidate()
    {
        // ห้ามสร้าง GameObject/คอมโพเนนต์ใน OnValidate → อัปเดตเฉพาะเมื่อมีชิ้นส่วนครบแล้ว (OnEnable สร้างให้)
        if (!isActiveAndEnabled || lightComp == null || shaftFilter == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        Rebuild();
    }

    private void HandlePtsd(bool active) => ptsdTarget = active ? 1f : 0f;

    private Transform Child(string n)
    {
        Transform t = transform.Find(n);
        if (t == null)
        {
            t = new GameObject(n).transform;
            t.SetParent(transform, false);
        }
        return t;
    }

    private void EnsureParts()
    {
        Transform lt = Child("Light");
        lightComp = lt.GetComponent<Light>();
        if (lightComp == null) lightComp = lt.gameObject.AddComponent<Light>();

        Transform st = Child("Shaft");
        shaftFilter = st.GetComponent<MeshFilter>();
        if (shaftFilter == null) shaftFilter = st.gameObject.AddComponent<MeshFilter>();
        shaftRenderer = st.GetComponent<MeshRenderer>();
        if (shaftRenderer == null) shaftRenderer = st.gameObject.AddComponent<MeshRenderer>();
        shaftRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shaftRenderer.receiveShadows = false;
        shaftRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        shaftRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (mpb == null) mpb = new MaterialPropertyBlock();
    }

    /// <summary>ทิศแสงในพิกัดของตัวเอง</summary>
    public Vector3 LocalDir => Quaternion.Euler(downAngle, sideAngle, 0f) * Vector3.forward;

    public void Rebuild()
    {
        if (lightComp == null || shaftFilter == null) return;
        Vector3 dir = LocalDir;

        // ── ไฟ: อยู่นอกห้องหลังหน้าต่าง ส่องผ่านกลางหน้าต่าง · มุมกรวยพอดีขอบหน้าต่าง ──
        Transform lt = lightComp.transform;
        lt.localPosition = -dir * LightBack;
        lt.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        lightComp.type = LightType.Spot;
        float half = Mathf.Max(windowSize.x, windowSize.y) * 0.5f;
        float cone = 2f * Mathf.Atan(half / LightBack) * Mathf.Rad2Deg;
        lightComp.spotAngle = Mathf.Clamp(cone * 1.05f, 1f, 179f);
        lightComp.innerSpotAngle = lightComp.spotAngle * 0.8f;
        lightComp.range = reach + LightBack;
        lightComp.cookie = cookie;
        lightComp.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        ApplyLight(1f);

        // ── ลำแสง ──
        shaftRenderer.enabled = shaft && shaftMaterial != null;
        if (shaftMaterial != null && shaftRenderer.sharedMaterial != shaftMaterial) shaftRenderer.sharedMaterial = shaftMaterial;
        BuildShaftMesh(dir);
        ApplyShaft(1f);
    }

    private void BuildShaftMesh(Vector3 dir)
    {
        if (shaftMesh == null)
        {
            shaftMesh = new Mesh { name = "WindowShaft" };
            shaftMesh.hideFlags = HideFlags.DontSave;
        }
        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var uv2 = new System.Collections.Generic.List<Vector2>();   // x = เฟสกระพริบ · y = ความแรงของเส้น
        var tri = new System.Collections.Generic.List<int>();
        float grow = 1f + shaftLength * shaftSpread;

        var uv3 = new System.Collections.Generic.List<Vector2>();  // พิกัดบนลาย cookie (โหมด Volume)
        if (shaftStyle == ShaftStyle.Volume)
        {
            AddVolume(v, uv, uv2, uv3, tri, dir, grow);
        }
        else if (shaftStyle == ShaftStyle.Solid)
        {
            AddBeam(v, uv, uv2, tri, Vector2.zero, windowSize * 0.5f, dir, grow, 0f, 1f);
        }
        else
        {
            foreach (LightRay r in ComputeRays())
                AddBeam(v, uv, uv2, tri, r.center, r.half, dir, grow, r.phase, r.strength);
        }

        shaftMesh.Clear();
        shaftMesh.SetVertices(v);
        shaftMesh.SetUVs(0, uv);
        shaftMesh.SetUVs(1, uv2);
        while (uv3.Count < v.Count) uv3.Add(new Vector2(0.5f, 0.5f));
        shaftMesh.SetUVs(2, uv3);
        shaftMesh.SetTriangles(tri, 0);
        shaftMesh.RecalculateBounds();
        shaftFilter.sharedMesh = shaftMesh;
    }

    /// <summary>
    /// โหมด Volume: แผ่นตัดแนวตั้ง + แนวนอนซ้อนกันในลำแสง แต่ละแผ่นอ่านลาย cookie ตามตำแหน่ง
    /// → ซี่กรอบหน้าต่างกลายเป็น "ริ้วเงา" ยาวไปในอากาศ เหมือนแสง volumetric
    /// </summary>
    private void AddVolume(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv,
                           System.Collections.Generic.List<Vector2> uv2, System.Collections.Generic.List<Vector2> uv3,
                           System.Collections.Generic.List<int> tri, Vector3 dir, float grow)
    {
        float hx = windowSize.x * 0.5f, hy = windowSize.y * 0.5f;
        float m = Mathf.Max(windowSize.x, windowSize.y, 0.01f);
        Vector3 end = dir * shaftLength;
        int n = Mathf.Max(2, volumeSlices / 2);
        for (int axis = 0; axis < 2; axis++)            // 0 = แผ่นตั้ง (ไล่ตาม x) · 1 = แผ่นนอน (ไล่ตาม y)
        {
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                Vector3 p0, p1;
                if (axis == 0) { float x = Mathf.Lerp(-hx, hx, t); p0 = new Vector3(x, -hy, 0); p1 = new Vector3(x, hy, 0); }
                else { float y = Mathf.Lerp(-hy, hy, t); p0 = new Vector3(-hx, y, 0); p1 = new Vector3(hx, y, 0); }
                Vector3 q0 = new Vector3(p0.x * grow, p0.y * grow, 0f) + end;
                Vector3 q1 = new Vector3(p1.x * grow, p1.y * grow, 0f) + end;
                int k = v.Count;
                v.Add(p0); v.Add(p1); v.Add(q1); v.Add(q0);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int j = 0; j < 4; j++) uv2.Add(new Vector2(0f, 1f));
                Vector2 c0 = new Vector2(0.5f + p0.x / m, 0.5f + p0.y / m), c1 = new Vector2(0.5f + p1.x / m, 0.5f + p1.y / m);
                uv3.Add(c0); uv3.Add(c1); uv3.Add(c1); uv3.Add(c0);
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k); tri.Add(k + 2); tri.Add(k + 3);
            }
        }
    }

    /// <summary>ตำแหน่งเส้นแสง 1 เส้น (พิกัดบนหน้าต่าง: กลาง = 0,0 · เมตร)</summary>
    public struct LightRay { public Vector2 center, half; public float phase, strength; }

    /// <summary>คำนวณเส้นแสงทั้งหมดจาก raySeed — ใช้ทั้งสร้างลำแสงและสร้างลาย cookie ให้ตรงกัน</summary>
    public LightRay[] ComputeRays()
    {
        var list = new LightRay[Mathf.Max(1, rayCount)];
        var rng = new System.Random(raySeed * 7919 + 17);
        float hx = windowSize.x * 0.5f, hy = windowSize.y * 0.5f;
        for (int i = 0; i < list.Length; i++)
        {
            float w = rayWidth * (0.5f + (float)rng.NextDouble()) * 0.5f;
            Vector2 c, half;
            if (raysAsSlits)
            {
                // ร่องแนวตั้งเรียงซ้าย→ขวา (เหลื่อมนิดหน่อย) ยาวเกือบเต็มความสูงหน้าต่าง
                float t = (i + 0.5f) / list.Length + ((float)rng.NextDouble() - 0.5f) * 0.6f / list.Length;
                c = new Vector2(Mathf.Lerp(-hx + w, hx - w, t), ((float)rng.NextDouble() - 0.5f) * hy * 0.2f);
                half = new Vector2(w, hy * (0.75f + 0.25f * (float)rng.NextDouble()));
            }
            else
            {
                c = new Vector2(((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, hx - w), ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, hy - w));
                half = new Vector2(w, w);
            }
            list[i] = new LightRay { center = c, half = half, strength = 0.45f + 0.55f * (float)rng.NextDouble(), phase = (float)rng.NextDouble() };
        }
        return list;
    }

    /// <summary>ลำแสง 1 ลำ (กล่องเปิดหัวท้าย 4 ด้าน) จากสี่เหลี่ยมบนหน้าต่าง → ตามทิศแสง</summary>
    private void AddBeam(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv,
                         System.Collections.Generic.List<Vector2> uv2, System.Collections.Generic.List<int> tri,
                         Vector2 c, Vector2 half, Vector3 dir, float grow, float phase, float strength)
    {
        Vector3 end = dir * shaftLength;
        Vector3[] a =
        {
            new Vector3(c.x - half.x, c.y - half.y, 0), new Vector3(c.x + half.x, c.y - half.y, 0),
            new Vector3(c.x + half.x, c.y + half.y, 0), new Vector3(c.x - half.x, c.y + half.y, 0)
        };
        for (int s = 0; s < 4; s++)
        {
            Vector3 p0 = a[s], p1 = a[(s + 1) % 4];
            Vector3 q0 = new Vector3(p0.x * grow, p0.y * grow, 0f) + end;
            Vector3 q1 = new Vector3(p1.x * grow, p1.y * grow, 0f) + end;
            int k = v.Count;
            v.Add(p0); v.Add(p1); v.Add(q1); v.Add(q0);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            for (int i = 0; i < 4; i++) uv2.Add(new Vector2(phase, strength));
            tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k); tri.Add(k + 2); tri.Add(k + 3);
        }
    }

    // ───────── ทุกเฟรม: เมฆ + PTSD ─────────
    private void Update()
    {
        if (lightComp == null) return;
        if (Application.isPlaying)
        {
            float step = ptsdFade <= 0f ? 1f : Time.deltaTime / ptsdFade;
            ptsdT = Mathf.MoveTowards(ptsdT, ptsdTarget, step);
        }
        float cloud = 1f;
        if (clouds && Application.isPlaying)
        {
            float n = Mathf.PerlinNoise(seed, Time.time * cloudSpeed);          // 0..1
            cloud = 1f - cloudAmount * Mathf.SmoothStep(0f, 1f, n);
        }
        ApplyLight(cloud);
        ApplyShaft(cloud);
    }

    private void CurrentLook(out Color c, out float mul)
    {
        Color target = color;
        float targetMul = 1f;
        if (inPtsd == PtsdLook.Red) { target = ptsdRed; targetMul = ptsdIntensityMul; }
        else if (inPtsd == PtsdLook.Off) { targetMul = 0f; }
        float t = Application.isPlaying ? ptsdT : 0f;
        c = Color.Lerp(color, target, t);
        mul = Mathf.Lerp(1f, targetMul, t);
    }

    private void ApplyLight(float cloud)
    {
        CurrentLook(out Color c, out float mul);
        lightComp.color = c;
        lightComp.intensity = intensity * mul * cloud;
        lightComp.enabled = intensity * mul > 0.001f;
    }

    private void ApplyShaft(float cloud)
    {
        if (shaftRenderer == null || !shaftRenderer.enabled) return;
        CurrentLook(out Color c, out float mul);
        shaftRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", c);
        float styleMul = shaftStyle == ShaftStyle.Rays ? 1.8f
                       : shaftStyle == ShaftStyle.Volume ? 3f / Mathf.Max(2, volumeSlices) : 1f;   // แผ่นซ้อนเยอะ → หารให้สว่างพอๆ กัน
        mpb.SetFloat("_Opacity", shaftOpacity * mul * cloud * styleMul);
        mpb.SetFloat("_Soft", shaftStyle == ShaftStyle.Rays ? Mathf.Max(shaftSoftness, 0.45f) : shaftSoftness);
        mpb.SetFloat("_RayFlicker", shaftStyle == ShaftStyle.Rays ? rayFlicker : 0f);
        bool vol = shaftStyle == ShaftStyle.Volume && cookie != null;
        mpb.SetFloat("_UseCookie", vol ? 1f : 0f);
        if (cookie != null) mpb.SetTexture("_Cookie", cookie);
        shaftRenderer.SetPropertyBlock(mpb);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.6f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(windowSize.x, windowSize.y, 0.02f));
        Gizmos.DrawLine(Vector3.zero, LocalDir * shaftLength);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
