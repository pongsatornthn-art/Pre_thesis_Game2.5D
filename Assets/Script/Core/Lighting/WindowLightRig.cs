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
        float w = windowSize.x * 0.5f, h = windowSize.y * 0.5f;
        float grow = 1f + shaftLength * shaftSpread;
        Vector3 end = dir * shaftLength;
        // มุมหน้าต่าง (z = 0) และปลายลำแสง
        Vector3[] a = { new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) };
        Vector3[] b = new Vector3[4];
        for (int i = 0; i < 4; i++) b[i] = new Vector3(a[i].x * grow, a[i].y * grow, 0f) + end;

        // 4 ด้าน (ล่าง ขวา บน ซ้าย) — แต่ละด้าน u = ขวาง 0..1 · v = ตามยาว 0..1
        var v = new Vector3[16];
        var uv = new Vector2[16];
        var tri = new int[24];
        for (int s = 0; s < 4; s++)
        {
            int i0 = s, i1 = (s + 1) % 4, k = s * 4;
            v[k] = a[i0]; v[k + 1] = a[i1]; v[k + 2] = b[i1]; v[k + 3] = b[i0];
            uv[k] = new Vector2(0, 0); uv[k + 1] = new Vector2(1, 0); uv[k + 2] = new Vector2(1, 1); uv[k + 3] = new Vector2(0, 1);
            int t = s * 6;
            tri[t] = k; tri[t + 1] = k + 1; tri[t + 2] = k + 2; tri[t + 3] = k; tri[t + 4] = k + 2; tri[t + 5] = k + 3;
        }
        shaftMesh.Clear();
        shaftMesh.vertices = v;
        shaftMesh.uv = uv;
        shaftMesh.triangles = tri;
        shaftMesh.RecalculateBounds();
        shaftFilter.sharedMesh = shaftMesh;
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
        mpb.SetFloat("_Opacity", shaftOpacity * mul * cloud);
        mpb.SetFloat("_Soft", shaftSoftness);
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
