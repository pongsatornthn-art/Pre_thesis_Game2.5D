using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [ข้อ 6] ไฮไลต์ไอเทม/วัตถุ — ขอบเรืองแสงที่ "วาดทับหน้าทุกอย่างเสมอ" ไม่จมหายใต้โมเดล (โต๊ะ/ตู้/ผนัง)
///
/// แปะที่ไอเทม (ตัวที่มี ItemPickup) หรือวัตถุใดๆ ก็ได้ — หา SpriteRenderer / MeshRenderer ให้เอง
///   • Sprite (ไอเทมทั้งหมดในเกม) → ขอบตามรูปทรงสไปรต์ (คำนวณจาก alpha) + เรืองด้านใน
///   • Mesh (วัตถุ 3D เช่น ของที่ตีแตกได้ของนา) → เรืองทับตัว + ขอบรอบนอก
///
/// โหมด:
///   PickupTarget = สว่างเฉพาะชิ้นที่ป้าย [E] เก็บ กำลังชี้ (ชิ้นที่ใกล้สุดในระยะ interactRange ของ PlayerInteraction) — ค่าเริ่มสำหรับไอเทม
///   PlayerNear   = ผู้เล่นเข้าใกล้ภายใน Near Radius
///   Always       = สว่างตลอดตอนสคริปต์นี้เปิดอยู่ (ปิด GameObject/สคริปต์ = ดับ) — ใช้กับ Highlight ของ close-up
///   Manual       = โค้ดอื่นสั่งเอง: SetHighlighted(true/false)
///
/// ไม่ต้องสร้าง Material — สคริปต์สร้างแผ่นวาดซ้อนเป็นลูกของรูป/โมเดลตอนเล่น ใช้ shader ใน Resources
/// </summary>
[DisallowMultipleComponent]
public class ItemHighlight : MonoBehaviour
{
    public enum Mode { PickupTarget, PlayerNear, Always, Manual }

    [Header("เป้าหมาย")]
    [Tooltip("รูป/โมเดลที่จะไฮไลต์ — เว้นว่าง = หาเอง (SpriteRenderer ที่มีรูป หรือ MeshRenderer ในตัวเอง/ลูก)")]
    public Renderer target;
    public Mode mode = Mode.PickupTarget;
    [Tooltip("โหมด PlayerNear: ระยะ (เมตร)")]
    [Min(0.1f)] public float nearRadius = 3f;
    [Tooltip("ซ่อนตอนหยุดเกม / เปิดสมุด / ฉากเนื้อเรื่อง / มินิเกม (โหมด PickupTarget, PlayerNear)")]
    public bool hideWhenGameplayBlocked = true;

    [Header("หน้าตา")]
    [ColorUsage(true, true)] public Color color = new Color(1f, 0.82f, 0.35f, 1f);
    [Tooltip("วาดทับทุกอย่าง (ไม่จมใต้โมเดล) — ปิด = ถูกของอื่นบังได้ตามปกติ")]
    public bool drawOnTop = true;
    [Tooltip("ความหนาขอบ (หน่วยพิกเซลของรูป) — Sprite")]
    [Range(0f, 16f)] public float spriteOutlineThickness = 3f;
    [Tooltip("เรืองด้านในรูป (0 = ขอบอย่างเดียว)")]
    [Range(0f, 1f)] public float innerGlow = 0.2f;
    [Tooltip("วาดตัวรูปทับหน้าทุกอย่างด้วย (0 = ไม่วาด ให้เห็นแค่ขอบ/เรือง) — Sprite")]
    [Range(0f, 1f)] public float spriteOnTop = 0f;
    [Tooltip("ขอบขยายออกจากตัววัตถุกี่ % — Mesh")]
    [Range(0f, 0.3f)] public float meshOutlineScale = 0.06f;
    [Tooltip("ขอบดันออกตามผิว (เมตร) — Mesh")]
    [Range(0f, 0.1f)] public float meshOutlineWidth = 0.01f;
    [Min(0f)] public float pulseSpeed = 3f;
    [Range(0f, 1f)] public float pulseMin = 0.55f;
    [Tooltip("ความเร็วจางเข้า/ออก")]
    [Min(0.1f)] public float fadeSpeed = 8f;

    [Header("Shader (เว้นว่าง = หาเองจาก Resources)")]
    public Shader spriteShader;
    public Shader meshShader;

    // ───────── สถานะ ─────────
    public bool IsHighlighted => fade > 0.001f;

    private bool manualOn;
    private float fade;
    private bool built;

    private SpriteRenderer sr;
    private MeshFilter targetMeshFilter;

    private GameObject spriteOverlay;
    private MeshRenderer spriteOverlayRenderer;
    private Mesh spriteMesh;
    private Sprite builtSprite;
    private bool builtFlipX, builtFlipY;

    private MeshRenderer meshFill, meshRing;

    private MaterialPropertyBlock mpb;

    private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ID_Color = Shader.PropertyToID("_Color");
    private static readonly int ID_Thickness = Shader.PropertyToID("_Thickness");
    private static readonly int ID_InnerGlow = Shader.PropertyToID("_InnerGlow");
    private static readonly int ID_SpriteOnTop = Shader.PropertyToID("_SpriteOnTop");
    private static readonly int ID_SpriteColor = Shader.PropertyToID("_SpriteColor");
    private static readonly int ID_PulseSpeed = Shader.PropertyToID("_PulseSpeed");
    private static readonly int ID_PulseMin = Shader.PropertyToID("_PulseMin");
    private static readonly int ID_Fade = Shader.PropertyToID("_Fade");
    private static readonly int ID_UVRect = Shader.PropertyToID("_UVRect");
    private static readonly int ID_TexelSz = Shader.PropertyToID("_TexelSz");
    private static readonly int ID_FillAlpha = Shader.PropertyToID("_FillAlpha");
    private static readonly int ID_RimAlpha = Shader.PropertyToID("_RimAlpha");
    private static readonly int ID_Expand = Shader.PropertyToID("_Expand");
    private static readonly int ID_Width = Shader.PropertyToID("_Width");
    private static readonly int ID_Center = Shader.PropertyToID("_Center");

    // ───────── API ─────────
    /// <summary>โหมด Manual: สั่งเปิด/ปิดไฮไลต์ (โหมดอื่นจะสลับเป็น Manual ให้)</summary>
    public void SetHighlighted(bool on)
    {
        mode = Mode.Manual;
        manualOn = on;
    }

    public void SetColor(Color c) => color = c;

    // ───────── ลงทะเบียน (หาไอเทมที่ป้าย [E] ชี้) ─────────
    private static readonly List<ItemHighlight> pickupCandidates = new List<ItemHighlight>();
    private static int resolvedFrame = -1;
    private static ItemHighlight resolvedTarget;
    private static PlayerInteraction cachedInteraction;
    private ItemPickup ownPickup;

    private void Awake()
    {
        ownPickup = GetComponentInParent<ItemPickup>();
        mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (!pickupCandidates.Contains(this)) pickupCandidates.Add(this);
    }

    private void OnDisable()
    {
        pickupCandidates.Remove(this);
        fade = 0f;
        SetOverlayVisible(false);
    }

    private void OnDestroy()
    {
        if (spriteMesh != null) Destroy(spriteMesh);
        if (spriteOverlay != null) Destroy(spriteOverlay);
        if (meshFill != null) Destroy(meshFill.gameObject);
        if (meshRing != null) Destroy(meshRing.gameObject);
    }

    // ───────── loop ─────────
    private void LateUpdate()
    {
        if (!built) Build();
        if (!built) return;

        bool want = ShouldHighlight();
        fade = Mathf.MoveTowards(fade, want ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);

        if (fade <= 0.001f)
        {
            SetOverlayVisible(false);
            return;
        }

        if (sr != null) SyncSprite();
        SetOverlayVisible(true);
        ApplyProperties();
    }

    private bool ShouldHighlight()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        switch (mode)
        {
            case Mode.Always: return true;
            case Mode.Manual: return manualOn;
            case Mode.PlayerNear:
                if (hideWhenGameplayBlocked && IsGameplayBlocked()) return false;
                Transform p = FindPlayer();
                if (p == null) return false;
                Vector3 d = p.position - transform.position; d.y = 0f;
                return d.sqrMagnitude <= nearRadius * nearRadius;
            case Mode.PickupTarget:
                if (hideWhenGameplayBlocked && IsGameplayBlocked()) return false;
                return ResolvePickupTarget() == this;
        }
        return false;
    }

    /// <summary>หาไอเทมที่ใกล้ผู้เล่นสุดในระยะ — กติกาเดียวกับ PlayerInteraction (ระยะแนวราบ ภายใน interactRange) · คำนวณครั้งเดียวต่อเฟรม</summary>
    private static ItemHighlight ResolvePickupTarget()
    {
        if (resolvedFrame == Time.frameCount) return resolvedTarget;
        resolvedFrame = Time.frameCount;
        resolvedTarget = null;

        if (cachedInteraction == null) cachedInteraction = FindFirstObjectByType<PlayerInteraction>();
        PlayerInteraction pi = cachedInteraction;
        if (pi == null || !pi.isActiveAndEnabled) return null;

        // ป้ายกำลังโชว์ของชิ้นอื่นที่ใช้ปุ่ม E (เช่น ประตู) → ไม่ไฮไลต์ไอเทม (กด E ครั้งนี้ไม่ได้เก็บของ)
        IInteractPromptService prompt = ServiceLocator.GetOptional<IInteractPromptService>();
        if (prompt != null && prompt.IsOtherShown(pi, PromptKey.Pickup)) return null;

        Vector3 from = pi.transform.position;
        float range2 = pi.interactRange * pi.interactRange;
        float best = float.MaxValue;
        for (int i = 0; i < pickupCandidates.Count; i++)
        {
            ItemHighlight h = pickupCandidates[i];
            if (h == null || h.mode != Mode.PickupTarget || h.ownPickup == null || !h.ownPickup.isActiveAndEnabled) continue;
            Vector3 d = h.ownPickup.transform.position - from; d.y = 0f;
            float dist = d.sqrMagnitude;
            if (dist <= range2 && dist < best) { best = dist; resolvedTarget = h; }
        }
        return resolvedTarget;
    }

    private static Transform cachedPlayer;
    private static Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) cachedPlayer = p.transform;
        }
        return cachedPlayer;
    }

    private static bool IsGameplayBlocked()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return true;
        if (GameplayInputGate.Blocked || JournalController.IsAnyOpen) return true;
        if (StoryCloseUpView.Active != null) return true;
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return true;
        return false;
    }

    // ───────── สร้างแผ่นวาดซ้อน ─────────
    private void Build()
    {
        if (target == null) target = FindTarget();
        if (target == null) return;

        int topLayer = TopSortingLayerId();

        sr = target as SpriteRenderer;
        if (sr != null)
        {
            if (spriteShader == null) spriteShader = Shader.Find("Custom/HighlightSprite");
            if (spriteShader == null) { Warn("Custom/HighlightSprite"); enabled = false; return; }

            spriteOverlay = new GameObject("HL_SpriteOverlay");
            spriteOverlay.transform.SetParent(target.transform, false);
            spriteOverlay.AddComponent<MeshFilter>().sharedMesh = spriteMesh = new Mesh { name = "HL_SpriteQuad" };
            spriteOverlayRenderer = spriteOverlay.AddComponent<MeshRenderer>();
            Setup(spriteOverlayRenderer, SharedMaterial(spriteShader, false, drawOnTop), topLayer);
            builtSprite = null;
            SyncSprite();
            built = true;
            SetOverlayVisible(false);
            return;
        }

        targetMeshFilter = target.GetComponent<MeshFilter>();
        if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
        {
            if (meshShader == null) meshShader = Shader.Find("Custom/HighlightMesh");
            if (meshShader == null) { Warn("Custom/HighlightMesh"); enabled = false; return; }

            meshFill = MakeMeshLayer("HL_MeshFill", SharedMaterial(meshShader, false, drawOnTop), topLayer);
            meshRing = MakeMeshLayer("HL_MeshRing", SharedMaterial(meshShader, true, drawOnTop), topLayer);
            built = true;
            SetOverlayVisible(false);
            return;
        }

        Debug.LogWarning($"[ItemHighlight] '{name}' ไม่มี SpriteRenderer (ที่มีรูป) หรือ MeshRenderer+MeshFilter ให้ไฮไลต์", this);
        enabled = false;
    }

    private Renderer FindTarget()
    {
        // 1) SpriteRenderer ที่มีรูป (ข้ามเงา) — เลือกชิ้นใหญ่สุด เช่น Fragment ที่ตัวแม่ว่างแต่ลูก "Paper" มีรูป
        SpriteRenderer bestSr = null;
        float bestArea = -1f;
        foreach (SpriteRenderer s in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (s.sprite == null || s.name.ToLowerInvariant().Contains("shadow")) continue;
            Vector3 size = s.sprite.bounds.size;
            float area = size.x * size.y;
            if (area > bestArea) { bestArea = area; bestSr = s; }
        }
        if (bestSr != null) return bestSr;

        // 2) MeshRenderer ตัวเอง → ลูก
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && mr.enabled) return mr;
        foreach (MeshRenderer m in GetComponentsInChildren<MeshRenderer>(true))
            if (m.enabled && !m.name.StartsWith("HL_")) return m;
        return null;
    }

    private MeshRenderer MakeMeshLayer(string layerName, Material mat, int topLayer)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(target.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = targetMeshFilter.sharedMesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        Setup(r, mat, topLayer);
        return r;
    }

    private static void Setup(MeshRenderer r, Material mat, int topLayer)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.sortingLayerID = topLayer;           // ไอเทมเป็นสไปรต์ (คิวโปร่งใส) → ต้องอยู่ sorting layer บนสุดถึงจะวาดหลังทุกอย่าง
        r.sortingOrder = short.MaxValue;
        r.enabled = false;
    }

    private static int TopSortingLayerId()
    {
        SortingLayer[] layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].id : 0;
    }

    // ───────── สไปรต์: แผ่นสี่เหลี่ยมขยายเผื่อขอบ ─────────
    private void SyncSprite()
    {
        Sprite s = sr.sprite;
        if (s == null) return;
        if (s == builtSprite && sr.flipX == builtFlipX && sr.flipY == builtFlipY) return;

        builtSprite = s;
        builtFlipX = sr.flipX;
        builtFlipY = sr.flipY;

        Texture2D tex = s.texture;
        Vector2 texSize = new Vector2(tex.width, tex.height);

        Rect pxRect;
        try { pxRect = s.textureRect; }               // รูปเดี่ยว / atlas แบบสี่เหลี่ยม
        catch { pxRect = UvBoundsAsPixelRect(s, texSize); } // atlas แบบ tight

        Vector2 uvMin = new Vector2(pxRect.xMin / texSize.x, pxRect.yMin / texSize.y);
        Vector2 uvMax = new Vector2(pxRect.xMax / texSize.x, pxRect.yMax / texSize.y);

        // ขนาดรูปในหน่วยโลก (อิงจุด pivot เหมือน SpriteRenderer)
        float ppu = s.pixelsPerUnit;
        Vector2 sizeU = s.rect.size / ppu;
        Vector2 pivotU = s.pivot / ppu;
        Vector2 min = -pivotU;
        Vector2 max = sizeU - pivotU;

        float padPx = spriteOutlineThickness + 2f;
        float padU = padPx / ppu;
        Vector2 padUV = new Vector2(padPx / texSize.x, padPx / texSize.y);

        min -= Vector2.one * padU;
        max += Vector2.one * padU;
        Vector2 u0 = uvMin - padUV;
        Vector2 u1 = uvMax + padUV;

        if (sr.flipX) { float t = min.x; min.x = -max.x; max.x = -t; }
        if (sr.flipY) { float t = min.y; min.y = -max.y; max.y = -t; }
        float ua = sr.flipX ? u1.x : u0.x, ub = sr.flipX ? u0.x : u1.x;
        float va = sr.flipY ? u1.y : u0.y, vb = sr.flipY ? u0.y : u1.y;

        spriteMesh.Clear();
        spriteMesh.vertices = new[]
        {
            new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f),
            new Vector3(min.x, max.y, 0f), new Vector3(max.x, max.y, 0f)
        };
        spriteMesh.uv = new[] { new Vector2(ua, va), new Vector2(ub, va), new Vector2(ua, vb), new Vector2(ub, vb) };
        spriteMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        spriteMesh.RecalculateBounds();

        mpb.Clear();
        spriteOverlayRenderer.GetPropertyBlock(mpb);
        mpb.SetTexture(ID_MainTex, tex);
        mpb.SetVector(ID_UVRect, new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y));
        mpb.SetVector(ID_TexelSz, new Vector4(1f / texSize.x, 1f / texSize.y, 0f, 0f));
        spriteOverlayRenderer.SetPropertyBlock(mpb);
    }

    private static Rect UvBoundsAsPixelRect(Sprite s, Vector2 texSize)
    {
        Vector2 a = new Vector2(float.MaxValue, float.MaxValue), b = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector2 uv in s.uv) { a = Vector2.Min(a, uv); b = Vector2.Max(b, uv); }
        return Rect.MinMaxRect(a.x * texSize.x, a.y * texSize.y, b.x * texSize.x, b.y * texSize.y);
    }

    // ───────── ค่า shader ต่อชิ้น ─────────
    private void ApplyProperties()
    {
        if (spriteOverlayRenderer != null)
        {
            spriteOverlayRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ID_Color, color);
            mpb.SetFloat(ID_Thickness, spriteOutlineThickness);
            mpb.SetFloat(ID_InnerGlow, innerGlow);
            mpb.SetFloat(ID_SpriteOnTop, spriteOnTop);
            mpb.SetColor(ID_SpriteColor, sr != null ? sr.color : Color.white);
            mpb.SetFloat(ID_PulseSpeed, pulseSpeed);
            mpb.SetFloat(ID_PulseMin, pulseMin);
            mpb.SetFloat(ID_Fade, fade);
            spriteOverlayRenderer.SetPropertyBlock(mpb);
        }

        if (meshFill != null)
        {
            Vector3 c = targetMeshFilter != null && targetMeshFilter.sharedMesh != null ? targetMeshFilter.sharedMesh.bounds.center : Vector3.zero;
            SetMeshLayer(meshFill, c, innerGlow, 0.6f * innerGlow / 0.2f, 0f, 0f);
            SetMeshLayer(meshRing, c, 1f, 0f, meshOutlineScale, meshOutlineWidth);
        }
    }

    private void SetMeshLayer(MeshRenderer r, Vector3 center, float fillAlpha, float rimAlpha, float expand, float width)
    {
        mpb.Clear();
        mpb.SetColor(ID_Color, color);
        mpb.SetFloat(ID_FillAlpha, fillAlpha);
        mpb.SetFloat(ID_RimAlpha, Mathf.Clamp01(rimAlpha));
        mpb.SetFloat(ID_Expand, expand);
        mpb.SetFloat(ID_Width, width);
        mpb.SetVector(ID_Center, center);
        mpb.SetFloat(ID_PulseSpeed, pulseSpeed);
        mpb.SetFloat(ID_PulseMin, pulseMin);
        mpb.SetFloat(ID_Fade, fade);
        r.SetPropertyBlock(mpb);
    }

    private void SetOverlayVisible(bool on)
    {
        if (spriteOverlayRenderer != null && spriteOverlayRenderer.enabled != on) spriteOverlayRenderer.enabled = on;
        if (meshFill != null && meshFill.enabled != on) meshFill.enabled = on;
        if (meshRing != null && meshRing.enabled != on) meshRing.enabled = on;
    }

    // ───────── Material ใช้ร่วมกัน (ค่าต่อชิ้นส่งผ่าน MaterialPropertyBlock) ─────────
    private static readonly Dictionary<(Shader, bool, bool), Material> materials = new Dictionary<(Shader, bool, bool), Material>();

    /// <param name="ring">Mesh: false = ชั้น Fill (เขียน stencil, คิว 3100) · true = ชั้นขอบ (อ่าน stencil, คิว 3101)</param>
    private static Material SharedMaterial(Shader shader, bool ring, bool onTop)
    {
        var key = (shader, ring, onTop);
        if (materials.TryGetValue(key, out Material m) && m != null) return m;

        m = new Material(shader) { name = $"HL_{shader.name}_{(ring ? "Ring" : "Fill")}_{(onTop ? "OnTop" : "Depth")}", hideFlags = HideFlags.DontSave };
        m.SetFloat("_ZTest", onTop ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
        if (m.HasProperty("_StencilComp"))
        {
            m.SetFloat("_StencilComp", ring ? (float)CompareFunction.NotEqual : (float)CompareFunction.Always);
            m.SetFloat("_StencilPass", ring ? (float)StencilOp.Keep : (float)StencilOp.Replace);
        }
        m.renderQueue = ring ? 3101 : 3100;
        materials[key] = m;
        return m;
    }

    private void Warn(string shaderName)
    {
        Debug.LogError($"[ItemHighlight] หา shader '{shaderName}' ไม่เจอ — วางไฟล์ .shader ไว้ในโฟลเดอร์ชื่อ Resources (เช่น Assets/Art/Shaders/Resources/)", this);
    }
}
