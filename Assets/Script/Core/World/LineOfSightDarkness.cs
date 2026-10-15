using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// "เพดานดำ" ตามสายตาตัวละคร (แบบ Darkwood / Alien Shooter) — ที่ตัวละครมองไม่เห็นจะมืด เหมือนมีเพดานคลุม
///   ยิงเส้นสายตารอบตัว 360° (บนระนาบพื้น) ชนกำแพง/กรอบประตู/ประตูที่ปิด แล้วหยุด → ได้ "พื้นที่ที่มองเห็น"
///   เฟอร์นิเจอร์ไม่บังสายตา · ประตูเปิด (ตัวชนปิด) = มองทะลุได้
///   วาดลงแผนที่มุมบน (RenderTexture) แล้วค่อยๆ ผสมกับเฟรมก่อน → ความมืดไหลตามนุ่มๆ ไม่ตัดภาพ
///   เชดเดอร์ Hidden/LOS/Darkness (Full Screen Pass ใน PC_Renderer) อ่านแผนที่นี้แล้วทำให้มืด
///   ตำแหน่งในโลกของแต่ละพิกเซลมาจาก depth ที่ LOSDepthPass วาดเอง (_LOSDepthTexture) — นับของโปร่งใสด้วย
///   (depth ปกติของ URP ไม่มีของโปร่งใส เช่น กำแพง/ของที่ RoomWallFade ของปอตั้งเป็นโปร่งใสไว้ → จะมืด/สว่างผิดที่)
///
/// วางในซีน 1 ตัว (ที่ Map/Map_Test) · ทำงานเฉพาะตอนเล่น · ปิด component = ไม่มืดทันที
/// ไม่สร้างของในฉาก — ใช้แค่ RenderTexture/Mesh ในหน่วยความจำ
/// </summary>
public class LineOfSightDarkness : MonoBehaviour
{
    [Header("อะไรบังสายตา")]
    [Tooltip("ตัวแม่ของของที่บังสายตา (ตัวชนข้างใต้ที่ไม่ใช่ Trigger) — กำแพง กรอบประตู ประตู")]
    [SerializeField] private List<Transform> blockerRoots = new List<Transform>();

    [Header("พื้นที่แผนที่ (ครอบทั้งแมพ)")]
    [Tooltip("ใช้ขอบเขตของตัวชนนี้เป็นพื้นที่แผนที่ (เช่น NavFloor)")]
    [SerializeField] private Collider areaSource;
    [SerializeField, Min(0f)] private float areaPadding = 2f;
    [SerializeField] private int maskResolution = 512;

    [Header("สายตา")]
    [SerializeField, Range(90, 1440)] private int rayCount = 540;
    [Tooltip("ระยะมองสูงสุด (เมตร)")]
    [SerializeField, Min(1f)] private float maxDistance = 60f;
    [Tooltip("ระดับสายตา (เมตร) — บังเฉพาะของที่สูงคร่อมระดับนี้ · ของเตี้ยกว่า / คานเหนือประตูที่ลอยสูงกว่า = มองผ่านได้")]
    [SerializeField, Min(0.1f)] private float eyeHeight = 1.2f;
    [Tooltip("เห็นทะลุเข้าไปในกำแพงที่ชน (ให้ขอบบนกำแพงสว่าง) — ปกติเท่าความหนากำแพงพอดี ค่านี้คือเพดานสูงสุด (เมตร)")]
    [SerializeField, Min(0f)] private float wallReveal = 0.3f;

    [Header("เชดเดอร์ (ลากใส่ — กันโดนตัดตอน Build)")]
    [SerializeField] private Shader maskShader;

    [Header("หน้าตา")]
    [Tooltip("ความมืดของที่มองไม่เห็น (1 = ดำสนิท)")]
    [SerializeField, Range(0f, 1f)] private float darkness = 1f;
    [Tooltip("ความไวในการเปลี่ยน (ยิ่งมากยิ่งไว) — เดินผ่านประตูแล้วห้องค่อยๆ เปิดเผย")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 6f;
    [Tooltip("ความนุ่มของขอบความมืด (จำนวนพิกเซลแผนที่)")]
    [SerializeField, Range(0f, 4f)] private float edgeSoftness = 1.5f;
    [Tooltip("ผิวที่ตั้งขึ้น (หน้ากำแพง/ด้านข้างของ/ตัวละคร) ดูที่จุดตรงหน้ามันห่างออกมาเท่านี้ (เมตร) — หน้ากำแพงฝั่งเราจะสว่างทั้งแผ่น")]
    [SerializeField, Range(0f, 1f)] private float facePush = 0.3f;

    [Header("ดีบัก")]
    [Tooltip("ติ๊กตอนเล่น = ระบายสีแทนภาพจริง · ม่วงอ่อน/ขาว = ตัวละครมองเห็น · เหลือง/เขียว = มองไม่เห็น (จะมืด)")]
    [SerializeField] private bool debugView;

    private static readonly int MaskId = Shader.PropertyToID("_LOSMask");
    private static readonly int RectId = Shader.PropertyToID("_LOSMaskRect");
    private static readonly int EnabledId = Shader.PropertyToID("_LOSEnabled");
    private static readonly int DarknessId = Shader.PropertyToID("_LOSDarkness");
    private static readonly int DebugId = Shader.PropertyToID("_LOSDebug");
    private static readonly int FacePushId = Shader.PropertyToID("_LOSFacePush");

    private readonly List<Collider> blockers = new List<Collider>();
    private readonly List<Vector4> segments = new List<Vector4>();   // (ax, az, bx, bz)
    private readonly List<int> segmentQuad = new List<int>();        // ขอบนี้เป็นของกล่องไหน
    private readonly List<Vector2> quadCorners = new List<Vector2>(); // มุม 4 มุมต่อกล่อง
    private readonly List<Vector4> quadBounds = new List<Vector4>();  // กรอบครอบของแต่ละกล่อง (minX, minZ, maxX, maxZ) ไว้กรองเร็ว
    private Transform player;
    private Rect area;
    private RenderTexture current, accumA, accumB;
    private bool flip;
    private Material maskMat;
    private Mesh fan;
    private Vector3[] verts;
    private Vector2[] rayDirs;
    private CommandBuffer cmd;
    private Vector2 lastOrigin;
    private float lastChecksum;
    private bool hasFan;
    private bool primed;
    private float edgePad;   // เมตร ≈ 0.75 พิกเซลแผนที่
    private LOSDepthPass depthPass;
    private Material depthMat;
    private readonly List<MeshRenderer> mapRenderers = new List<MeshRenderer>();          // ของในแมพทั้งหมด (ลูกของ object นี้)
    private readonly List<MeshRenderer> transparentToDraw = new List<MeshRenderer>();     // เฉพาะชิ้นโปร่งใสที่โชว์อยู่เฟรมนี้

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        foreach (Transform root in blockerRoots)
            if (root != null)
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) blockers.Add(c);

        Bounds b = areaSource != null ? areaSource.bounds : new Bounds(transform.position, new Vector3(60, 1, 60));
        area = new Rect(b.min.x - areaPadding, b.min.z - areaPadding, b.size.x + areaPadding * 2f, b.size.z + areaPadding * 2f);
        edgePad = 0.75f * area.width / Mathf.Max(16, maskResolution);

        Shader sh = maskShader != null ? maskShader : Shader.Find("Hidden/LOS/Mask");
        if (sh == null) { Debug.LogError("[LineOfSightDarkness] หาเชดเดอร์ Hidden/LOS/Mask ไม่เจอ", this); enabled = false; return; }
        maskMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        depthMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        mapRenderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));

        current = NewRT();
        accumA = NewRT();
        accumB = NewRT();

        fan = new Mesh { name = "LOS Fan", hideFlags = HideFlags.HideAndDontSave };
        fan.MarkDynamic();
        verts = new Vector3[rayCount + 1];
        rayDirs = new Vector2[rayCount];
        for (int i = 0; i < rayCount; i++) { float a = i * Mathf.PI * 2f / rayCount; rayDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        var tris = new int[rayCount * 3];
        for (int i = 0; i < rayCount; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % rayCount + 1; }
        fan.vertices = verts;
        fan.triangles = tris;
        cmd = new CommandBuffer { name = "LOS Mask" };

        if (blockers.Count == 0) Debug.LogWarning("[LineOfSightDarkness] ไม่มีอะไรบังสายตา (ช่อง Blocker Roots ว่าง) — มองเห็นทั้งแมพ", this);
    }

    private RenderTexture NewRT()
    {
        int h = Mathf.Max(16, Mathf.RoundToInt(maskResolution * area.height / Mathf.Max(area.width, 0.01f)));
        RenderTextureFormat fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
        var rt = new RenderTexture(maskResolution, h, 0, fmt, RenderTextureReadWrite.Linear) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "LOS Mask", hideFlags = HideFlags.HideAndDontSave };
        rt.Create();
        return rt;
    }

    private void OnEnable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        primed = false;
        RenderPipelineManager.beginCameraRendering += EnqueueDepthPass;
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        RenderPipelineManager.beginCameraRendering -= EnqueueDepthPass;
    }

    // ส่ง pass วาด depth ของตัวเองเข้ากล้องทุกเฟรม (ไม่ต้องไปเพิ่มใน PC_Renderer)
    private void EnqueueDepthPass(ScriptableRenderContext context, Camera cam)
    {
        if (depthMat == null) return;   // ยังไม่ผ่าน Start
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null || data.scriptableRenderer == null) return;
        if (depthPass == null) depthPass = new LOSDepthPass(depthMat, transparentToDraw);
        data.scriptableRenderer.EnqueuePass(depthPass);
    }

    private void OnDestroy()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        foreach (var rt in new[] { current, accumA, accumB }) if (rt != null) rt.Release();
        if (maskMat != null) Destroy(maskMat);
        if (depthMat != null) Destroy(depthMat);
        if (fan != null) Destroy(fan);
        cmd?.Release();
    }

    private void LateUpdate()
    {
        if (player == null || maskMat == null) return;

        Vector2 origin = new Vector2(player.position.x, player.position.z);
        float checksum = BuildSegments(origin);
        // ยืนนิ่ง + ไม่มีประตูขยับ → ใช้รูปสายตาเดิม (ประหยัดเครื่อง) · ความมืดยังจางต่อใน RenderMask
        if (!hasFan || (origin - lastOrigin).sqrMagnitude > 0.0001f || checksum != lastChecksum)
        {
            BuildFan(origin);
            lastOrigin = origin;
            lastChecksum = checksum;
            hasFan = true;
        }
        RenderMask();
        CollectTransparentMapRenderers();

        Shader.SetGlobalTexture(MaskId, flip ? accumB : accumA);
        Shader.SetGlobalVector(RectId, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
        Shader.SetGlobalFloat(DarknessId, darkness);
        Shader.SetGlobalFloat(DebugId, debugView ? 1f : 0f);
        Shader.SetGlobalFloat(FacePushId, facePush);
        Shader.SetGlobalFloat(EnabledId, 1f);
    }

    // ชิ้นส่วนแมพที่วัสดุเป็นโปร่งใส (กำแพง/ของที่ RoomWallFade ตั้งไว้ ฯลฯ) และยังโชว์อยู่ → ให้ LOSDepthPass วาด depth ให้
    // กำแพงที่ถูกตัดหน้ากล้อง (ShadowsOnly) / ของที่ซ่อนอยู่ ไม่นับ — มองทะลุไปเห็นของข้างหลังได้ถูก
    private void CollectTransparentMapRenderers()
    {
        transparentToDraw.Clear();
        foreach (MeshRenderer r in mapRenderers)
        {
            if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
            Material m = r.sharedMaterial;
            if (m == null || m.renderQueue <= (int)RenderQueue.GeometryLast) continue;   // ของทึบ — URP วาด DepthOnly ให้แล้ว
            transparentToDraw.Add(r);
        }
    }

    // ตัวชนที่เปิดอยู่ (ประตูเปิด = ตัวชนปิด → ไม่นับ) → ขอบบนระนาบพื้น (เฉพาะด้านที่หันหาตัวละคร)
    // คืนค่า checksum ไว้เช็คว่ามีอะไรขยับ/เปิด-ปิดไหม
    private float BuildSegments(Vector2 origin)
    {
        segments.Clear();
        segmentQuad.Clear();
        quadCorners.Clear();
        quadBounds.Clear();
        float checksum = 0f;
        foreach (Collider c in blockers)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds cb = c.bounds;
            if (cb.max.y < eyeHeight || cb.min.y > eyeHeight) continue;   // ของเตี้ย / คานเหนือประตู ไม่บังสายตา

            Vector3 a, b, d, e;
            if (c is BoxCollider box)
            {
                // กล่องที่วางเอียง (หน้าต่างมุม 45° ฯลฯ) — ใช้มุมจริงของกล่อง ไม่ใช่กล่องครอบ
                Vector3 h = box.size * 0.5f, ctr = box.center;
                Transform t = box.transform;
                a = t.TransformPoint(ctr + new Vector3(-h.x, 0f, -h.z));
                b = t.TransformPoint(ctr + new Vector3(h.x, 0f, -h.z));
                d = t.TransformPoint(ctr + new Vector3(h.x, 0f, h.z));
                e = t.TransformPoint(ctr + new Vector3(-h.x, 0f, h.z));
            }
            else
            {
                a = new Vector3(cb.min.x, 0f, cb.min.z);
                b = new Vector3(cb.max.x, 0f, cb.min.z);
                d = new Vector3(cb.max.x, 0f, cb.max.z);
                e = new Vector3(cb.min.x, 0f, cb.max.z);
            }
            checksum += a.x + a.z * 1.37f + d.x * 0.71f + d.z * 1.93f;
            int quad = quadCorners.Count / 4;
            quadCorners.Add(new Vector2(a.x, a.z));
            quadCorners.Add(new Vector2(b.x, b.z));
            quadCorners.Add(new Vector2(d.x, d.z));
            quadCorners.Add(new Vector2(e.x, e.z));
            quadBounds.Add(new Vector4(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(d.x, e.x)), Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(d.z, e.z)),
                                       Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(d.x, e.x)), Mathf.Max(Mathf.Max(a.z, b.z), Mathf.Max(d.z, e.z))));
            Vector2 mid = new Vector2((a.x + d.x) * 0.5f, (a.z + d.z) * 0.5f);
            AddEdge(a, b, mid, origin, quad);
            AddEdge(b, d, mid, origin, quad);
            AddEdge(d, e, mid, origin, quad);
            AddEdge(e, a, mid, origin, quad);
        }
        return checksum;
    }

    // ด้านหลังกล่อง (ตัวละครกับกลางกล่องอยู่ฝั่งเดียวกัน) ไม่มีวันชนก่อนด้านหน้า → ข้าม
    private void AddEdge(Vector3 p, Vector3 q, Vector2 mid, Vector2 origin, int quad)
    {
        float ex = q.x - p.x, ez = q.z - p.z;
        float sideOrigin = ex * (origin.y - p.z) - ez * (origin.x - p.x);
        float sideMid = ex * (mid.y - p.z) - ez * (mid.x - p.x);
        if (sideOrigin * sideMid >= 0f) return;
        segments.Add(new Vector4(p.x, p.z, q.x, q.z));
        segmentQuad.Add(quad);
    }

    private void BuildFan(Vector2 origin)
    {
        verts[0] = new Vector3(origin.x, origin.y, 0f);
        for (int i = 0; i < rayCount; i++)
        {
            Vector2 dir = rayDirs[i];
            float t = Cast(origin, dir, out int quad);
            // เห็นทะลุ "เท่าความหนากำแพงแผ่นที่ชน" (ขอบบนกำแพงสว่าง) — ไม่เลยไปถึงของที่วางชิดกำแพงฝั่งโน้น
            // + เผื่ออีกนิด (edgePad) เพราะเชดเดอร์ตัดขอบเบลอครึ่งนอกทิ้ง ไม่งั้นขอบบนกำแพงจะมืดไปครึ่งหนึ่ง
            float reveal = quad >= 0 ? Mathf.Min(ThroughWall(origin, dir, quad) - t, wallReveal) + edgePad : 0f;
            Vector2 hit = origin + dir * Mathf.Min(maxDistance, t + Mathf.Max(0f, reveal));
            verts[i + 1] = new Vector3(hit.x, hit.y, 0f);
        }
        fan.vertices = verts;
        fan.RecalculateBounds();
    }

    // ระยะถึงขอบที่ใกล้สุด (ray–segment 2 มิติ) + กล่องที่ชน (-1 = ไม่ชนอะไร)
    private float Cast(Vector2 o, Vector2 d, out int hitQuad)
    {
        float best = maxDistance;
        hitQuad = -1;
        for (int i = 0; i < segments.Count; i++)
        {
            float t = RaySegment(o, d, segments[i]);
            if (t < 0f || t >= best) continue;
            best = t;
            hitQuad = segmentQuad[i];
        }
        return best;
    }

    // ทะลุออกอีกฝั่งของกำแพง — ถ้าออกจากกล่องแรกแล้วเข้ากล่องที่ติดกันทันที (รอยต่อกำแพง/กรอบประตู) ทะลุต่อให้
    // ไม่งั้นตรงรอยต่อจะเห็นทะลุนิดเดียว → เป็นเส้นดำบนหน้ากำแพง
    private float ThroughWall(Vector2 o, Vector2 d, int quad)
    {
        float exit = ExitDistance(o, d, quad);
        for (int step = 0; step < 3; step++)
        {
            int next = QuadContaining(o + d * (exit + 0.01f));
            if (next < 0) break;
            float further = ExitDistance(o, d, next);
            if (further <= exit + 0.001f) break;
            exit = further;
        }
        return exit;
    }

    private int QuadContaining(Vector2 p)
    {
        for (int q = 0; q < quadCorners.Count / 4; q++)
        {
            Vector4 bb = quadBounds[q];
            if (p.x < bb.x || p.x > bb.z || p.y < bb.y || p.y > bb.w) continue;
            int k = q * 4;
            bool inside = true;
            float sign = 0f;
            for (int i = 0; i < 4 && inside; i++)
            {
                Vector2 a = quadCorners[k + i], b = quadCorners[k + (i + 1) % 4];
                float c = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (sign == 0f) sign = Mathf.Sign(c);
                else if (c * sign < 0f) inside = false;
            }
            if (inside) return q;
        }
        return -1;
    }

    // ระยะที่เส้นสายตาทะลุออกอีกฝั่งของกล่อง (ขอบที่ไกลสุดที่ตัดผ่าน)
    private float ExitDistance(Vector2 o, Vector2 d, int quad)
    {
        float exit = 0f;
        int k = quad * 4;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = quadCorners[k + i], q = quadCorners[k + (i + 1) % 4];
            float t = RaySegment(o, d, new Vector4(p.x, p.y, q.x, q.y));
            if (t > exit) exit = t;
        }
        return exit;
    }

    // คืนระยะตามเส้น ถ้าไม่ตัดกัน = -1
    private static float RaySegment(Vector2 o, Vector2 d, Vector4 s)
    {
        float ex = s.z - s.x, ez = s.w - s.y;
        float den = d.x * ez - d.y * ex;
        if (den > -1e-6f && den < 1e-6f) return -1f;
        float wx = s.x - o.x, wz = s.y - o.y;
        float t = (wx * ez - wz * ex) / den;
        if (t < 0f) return -1f;
        float u = (wx * d.y - wz * d.x) / den;
        return u < 0f || u > 1f ? -1f : t;
    }

    private void RenderMask()
    {
        cmd.Clear();
        cmd.SetRenderTarget(current);
        cmd.ClearRenderTarget(false, true, Color.black);
        // ไม่ต้อง GetGPUProjectionMatrix — CommandBuffer พลิกให้ตามแพลตฟอร์มเองแล้ว (พลิกซ้ำ = แผนที่กลับหัว)
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(area.xMin, area.xMax, area.yMin, area.yMax, -1f, 1f));
        cmd.DrawMesh(fan, Matrix4x4.identity, maskMat, 0, 0);
        Graphics.ExecuteCommandBuffer(cmd);

        RenderTexture prev = flip ? accumB : accumA;
        RenderTexture next = flip ? accumA : accumB;
        maskMat.SetTexture("_PrevTex", prev);
        // เฟรมแรก (เพิ่งเริ่ม/เพิ่งเปิด component) ใช้ค่าจริงทันที — RenderTexture ใหม่มีค่าขยะ ห้ามจางจากตรงนั้น
        maskMat.SetFloat("_Blend", primed ? 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime) : 1f);
        primed = true;
        maskMat.SetVector("_BlurTexel", new Vector4(edgeSoftness / current.width, edgeSoftness / current.height, 0, 0));
        Graphics.Blit(current, next, maskMat, 1);
        flip = !flip;
    }

    /// <summary>
    /// วาด depth ของทุกอย่างที่กล้องเห็น "รวมชิ้นส่วนแมพที่โปร่งใส" ลง _LOSDepthTexture
    ///   รอบ 1 ของทึบทั้งหมด: pass DepthOnly ของวัสดุเอง (สไปรท์ตัดขอบถูก)
    ///   รอบ 2 ชิ้นส่วนแมพที่โปร่งใส: วาดด้วย pass "LOSDepth" ของ LOSMask (วัสดุโปร่งใสของ URP ปิด DepthOnly ของตัวเองไว้)
    ///   ไม่วาดของโปร่งใสนอกแมพ (พาร์ติเคิล/เอฟเฟกต์) — ลอยกลางอากาศแล้วจะกลายเป็นจุดดำ
    /// </summary>
    private sealed class LOSDepthPass : ScriptableRenderPass
    {
        private const int OverridePassIndex = 2;
        private static readonly int DepthTexId = Shader.PropertyToID("_LOSDepthTexture");
        private static readonly ShaderTagId DepthOnlyTag = new ShaderTagId("DepthOnly");
        private static readonly List<Material> MatBuffer = new List<Material>();

        private readonly Material overrideMat;
        private readonly List<MeshRenderer> transparentRenderers;

        private class PassData
        {
            public RendererListHandle opaque;
            public List<MeshRenderer> transparent;
            public Material overrideMat;
        }

        public LOSDepthPass(Material overrideMat, List<MeshRenderer> transparentRenderers)
        {
            this.overrideMat = overrideMat;
            this.transparentRenderers = transparentRenderers;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.graphicsFormat = GraphicsFormat.None;
            desc.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            TextureHandle depth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_LOSDepthTexture", true);

            DrawingSettings opaqueDraw = RenderingUtils.CreateDrawingSettings(DepthOnlyTag, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            var opaqueParams = new RendererListParams(renderingData.cullResults, opaqueDraw, new FilteringSettings(RenderQueueRange.opaque));

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("LOS Depth", out PassData data))
            {
                data.opaque = renderGraph.CreateRendererList(opaqueParams);
                data.transparent = transparentRenderers;
                data.overrideMat = overrideMat;
                builder.UseRendererList(data.opaque);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(depth, DepthTexId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(d.opaque);
                    foreach (MeshRenderer r in d.transparent)
                    {
                        if (r == null) continue;
                        r.GetSharedMaterials(MatBuffer);   // ไม่สร้าง array ใหม่ทุกเฟรม
                        for (int s = 0; s < MatBuffer.Count; s++) ctx.cmd.DrawRenderer(r, d.overrideMat, s, OverridePassIndex);
                    }
                });
            }
        }
    }
}
