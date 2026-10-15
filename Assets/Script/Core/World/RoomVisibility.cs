using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "เห็นแค่ห้องที่อยู่" แบบเกมกล้องมุมตายตัว (Resident Evil / Signalis) — ต่อยอดโซนห้องของปอ ไม่แก้โค้ดปอ
///
/// ใช้โซนที่มีอยู่แล้ว (RoomFogFadeController + Trigger ของปอ) แล้วจัดภาพตามที่ผู้เล่นอยู่:
///   ห้องที่อยู่            → เห็นครบ (กล่องดำหาย · ของในห้องโผล่)
///   ห้อง "ข้างหลัง/ข้างๆ"  → กล่องดำทึบสูงเต็มห้อง (RoomCover) = ดำสนิทเท่ากันหมด ไม่มีของ/แสงหลุด
///   ห้อง "ระหว่างกล้องกับผู้เล่น" → กล่องดำแบนติดพื้น + โครงบ้านฝั่งนั้นหายทั้งหมด (ไม่บังผู้เล่น)
///   ทางเดิน (โซนแคบ) แนวเดียวกัน → เห็นพร้อมกันทั้งแนว (ทางเดินยาวไม่มีกล่องดำโผล่กลางทาง)
///   ไม่อยู่ในโซนไหน (เช่น โถงที่ไม่มีโซน) → ทุกห้องดำ · ของระหว่างกล้องกับผู้เล่นถูกตัดเหมือนกัน
///   ห้องที่มี "ช่องเปิด" (ประตูที่เปิดอยู่ / ทางโล่งไม่มีกำแพง) ที่ผู้เล่นมองเห็น → เห็นด้วย (ยกเว้นห้องหน้ากล้อง)
/// มืด/สว่างค่อยๆ เฟด (Fade Time) ไม่ตัดภาพ · ของในห้องค่อยๆ มืดตาม แล้วค่อยซ่อน
/// กำแพงที่ถูกตัด = มองไม่เห็นแต่ยังบังแสง/ทำเงา (ไฟห้องไม่รั่ว)
///
/// วางในซีน 1 ตัว (ที่ Map/Map_Test) · ปิด component นี้ = กลับเป็นแบบเดิมทันที
/// </summary>
public class RoomVisibility : MonoBehaviour
{
    [Tooltip("ตัวแม่ของแมพ — ของข้างใต้ที่อยู่ในโซนห้องจะถูกซ่อน/โชว์ตามห้อง")]
    [SerializeField] private Transform contentsRoot;

    [Tooltip("ชื่อกลุ่มลูกของ Contents Root ที่ไม่ใช่ 'ของในห้อง' (พื้น ไฟ กล่องดำ)")]
    [SerializeField] private List<string> neverHide = new List<string> { "Map_Test", "NavFloor", "RoomCovers" };

    [Tooltip("กลุ่มที่เป็น 'โครงบ้าน' (กำแพง ประตู หน้าต่าง) — ถูกตัดออกเมื่ออยู่ระหว่างกล้องกับผู้เล่น")]
    [SerializeField] private List<string> structureGroups = new List<string> { "All_Wall", "Door_Frame", "Door", "windows" };

    [Tooltip("โซนที่ด้านแคบกว่านี้ (เมตร) นับเป็นทางเดิน — ทางเดินแนวเดียวกันเห็นพร้อมกัน")]
    [SerializeField, Min(0f)] private float corridorMaxWidth = 4.5f;

    [Tooltip("✔ = ห้องอื่นดำด้วยกล่องดำ (RoomCover) + ซ่อนของในห้อง · ✘ = ไม่ทำ (เหลือแค่ตัดกำแพงหน้ากล้อง)")]
    [SerializeField] private bool darkenOtherRooms = true;

    [Tooltip("ซ่อนแผ่นหมอกของปอระหว่างที่ระบบนี้ทำงาน (กล่องดำทำหน้าที่แทน — แผ่นหมอกลอยเหนือพื้นดำจะดูแปลก)")]
    [SerializeField] private bool hideFogPlanes = true;

    [Tooltip("✔ = ตัดของที่อยู่ระหว่างกล้องกับผู้เล่นออก · ✘ = ไม่ตัด (ดำอย่างเดียว)")]
    [SerializeField] private bool cutAwayFront = true;

    [Header("เฟด")]
    [Tooltip("เวลาที่ห้องค่อยๆ มืด/สว่าง (วินาที) · 0 = ตัดทันที")]
    [SerializeField, Min(0f)] private float fadeTime = 0.6f;

    [Header("มองผ่านช่องเปิด (ประตูเปิด / ทางโล่ง)")]
    [Tooltip("✔ = ห้องที่มีช่องเปิดซึ่งผู้เล่นมองเห็น จะโชว์ด้วย (ห้องหน้ากล้องยังดำเสมอ)")]
    [SerializeField] private bool revealThroughOpenings = true;
    [Tooltip("ช่องเปิดที่ไกลเกินนี้ (เมตร) ไม่นับ")]
    [SerializeField, Min(1f)] private float revealDistance = 15f;
    [Tooltip("ความสูงสายตาจากพื้น (เมตร) ที่ใช้เช็คว่ามีกำแพง/ประตูขวางไหม")]
    [SerializeField, Min(0.1f)] private float eyeHeight = 1.2f;

    private const float ProbeStep = 0.3f;     // ระยะห่างจุดเช็คตามขอบโซน
    private const float ProbeInset = 0.3f;    // เริ่มจากในโซนเข้าไปเท่านี้
    private const float ProbeReach = 1.0f;    // ยื่นออกนอกโซนเท่านี้ (ข้ามกำแพงที่อยู่ตรงขอบ)
    private const float RecheckInterval = 0.15f;

    private struct Probe
    {
        public Vector3 from, to, edge;   // จากในโซน → นอกโซน · edge = จุดบนขอบ
        public bool open;                 // ไม่มีกำแพง/ประตูปิดขวาง
    }

    private class Zone
    {
        public string name;
        public Bounds bounds;
        public Collider trigger;
        public RoomCover cover;
        public bool isCorridor;
        public bool horizontal;   // ทางเดินแนวนอน (ยาวตามแกน X)
        public bool show = true;
        public bool contentsShown = true;
        public bool wantFlat;     // ทรงกล่องดำที่ควรเป็น (แบน = ห้องหน้ากล้อง) — เปลี่ยนจริงตอนมองไม่เห็นกล่องเท่านั้น
        public readonly List<Renderer> contents = new List<Renderer>();
        public readonly List<Probe> probes = new List<Probe>();
    }

    // หน้าตาของ 1 ชิ้น (กำแพง / ของในห้อง / ของในลิสต์ RoomWallFade ของปอ) — คุมสีผ่าน MaterialPropertyBlock ที่เดียว ไม่ตีกัน
    private class Look
    {
        public Renderer renderer;
        public bool isStructure;
        public bool cut;                    // กำแพงหน้ากล้อง → ค่อยๆ จางหาย แล้วเหลือแค่เงา
        public float shown = 1f;            // ความทึบที่โชว์อยู่ ไล่ตามค่าจริงแบบนุ่มๆ (ค่าจริงเปลี่ยนทันทีก็ไม่กระตุก)
        public float dim;                   // มืดลงตามกล่องดำ 0..1
        public ShadowCastingMode originalShadow;
        public bool written;
        // ระหว่างเฟด: สลับวัสดุ (สำเนาในหน่วยความจำ) เป็นโปร่งใสแบบธรรมดาชั่วคราว
        // แบบ premultiply ของเดิม ความใส 0 แล้วยังเหลือแสงสะท้อน/ขอบเป็นกระจก → ตอนจบเลยหายวับ
        public bool alphaMode;
        public Material[] mats;
        public float[] savedBlend;          // ต่อวัสดุ: src, dst, srcA, dstA
    }

    private readonly List<Zone> zones = new List<Zone>();
    private readonly HashSet<Collider> blockers = new HashSet<Collider>();
    private readonly List<Collider> doorColliders = new List<Collider>();
    private readonly List<bool> doorStates = new List<bool>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly List<Material> matBuffer = new List<Material>();
    private readonly HashSet<Zone> lastVisible = new HashSet<Zone>();
    private readonly Dictionary<Renderer, Look> looks = new Dictionary<Renderer, Look>();
    private readonly List<Look> lookList = new List<Look>();
    private MaterialPropertyBlock block;
    private float recheckTimer;
    private const float StickyDistance = 0.8f;   // ยืนในร่องระหว่างโซน (ช่องประตู) ยังนับเป็นห้องเดิม
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private readonly List<MeshRenderer> structure = new List<MeshRenderer>();
    private readonly List<MeshRenderer> fogPlanes = new List<MeshRenderer>();
    private readonly Dictionary<RoomCover, (Vector3 pos, Vector3 scale)> coverTall = new Dictionary<RoomCover, (Vector3, Vector3)>();
    private Transform player;
    private Zone current;
    private bool hasState;
    private bool initialized;

    /// <summary>โซนที่ผู้เล่นอยู่ตอนนี้ (null = ไม่อยู่ในห้องไหน)</summary>
    public string CurrentZone => current?.name;

    private void Start()
    {
        if (contentsRoot == null) contentsRoot = transform;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        foreach (RoomFogFadeController fog in FindObjectsByType<RoomFogFadeController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Collider trigger = fog.GetComponent<Collider>();
            if (trigger == null) trigger = fog.GetComponentInChildren<Collider>();
            if (trigger == null || !trigger.isTrigger || trigger.bounds.size.sqrMagnitude < 0.01f) continue;

            foreach (MeshRenderer f in fog.fogPlanes) if (f != null) fogPlanes.Add(f);
            Transform top = fog.transform; while (top.parent != null) top = top.parent;
            Bounds b = trigger.bounds;
            zones.Add(new Zone
            {
                name = top.name, bounds = b, trigger = trigger,
                isCorridor = Mathf.Min(b.size.x, b.size.z) < corridorMaxWidth,
                horizontal = b.size.x >= b.size.z
            });
        }

        foreach (Transform group in contentsRoot)
        {
            if (structureGroups.Contains(group.name))
            {
                structure.AddRange(group.GetComponentsInChildren<MeshRenderer>(true));
                foreach (MeshRenderer w in group.GetComponentsInChildren<MeshRenderer>(true)) LookOf(w).isStructure = true;
                foreach (Collider c in group.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) continue;
                    blockers.Add(c);
                    if (group.name == "Door") { doorColliders.Add(c); doorStates.Add(IsBlocking(c)); }   // ประตูเปิด = ตัวชนปิด
                }
                continue;
            }
            if (neverHide.Contains(group.name)) continue;
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
            {
                Zone best = Smallest(r.bounds.center) ?? Nearest(r.bounds.center, 1.5f);   // ของติดผนังที่ตกร่องระหว่างโซน → ห้องที่ใกล้สุด
                if (best == null) continue;
                best.contents.Add(r);
                if (r is MeshRenderer || r is SkinnedMeshRenderer) LookOf(r);
            }
        }

        foreach (RoomCover cover in FindObjectsByType<RoomCover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Zone owner = zones.Find(z => z.trigger == cover.zone);
            if (owner == null) { Debug.LogWarning($"[RoomVisibility] กล่องดำ '{cover.name}' ไม่ได้ผูกกับโซนไหน (ช่อง Zone ว่างหรือโซนถูกปิด)", cover); continue; }
            owner.cover = cover;
            coverTall[cover] = (cover.transform.position, cover.transform.localScale);
        }

        // ประตูทุกบานที่มี DoorController (บางบานไม่ได้อยู่ใต้กลุ่ม Door เช่น Door00) = ตัวบังเมื่อปิด
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (d.solidCollider != null && blockers.Add(d.solidCollider)) { doorColliders.Add(d.solidCollider); doorStates.Add(IsBlocking(d.solidCollider)); }

        // ของในลิสต์ RoomWallFade ของปอ: สคริปต์ปอสลับความใสทันที → ระบบนี้ไล่ตามให้นุ่ม (ไม่แก้โค้ดปอ)
        foreach (RoomWallFade fade in FindObjectsByType<RoomWallFade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (MeshRenderer w in fade.wallsToFade) if (w != null) LookOf(w);

        if (zones.Count == 0) Debug.LogWarning("[RoomVisibility] ไม่เจอโซนห้อง (RoomFogFadeController ของปอ + Trigger) — ไม่มีอะไรถูกซ่อน", this);
        foreach (Zone z in zones) BuildProbes(z);
        UpdateProbeStates();
        initialized = true;
        SetFog(false);
        Refresh(force: true, instant: true);
    }

    // ---------- ช่องเปิดตามขอบโซน ----------

    // จุดเช็คทุก 0.3 ม. รอบขอบโซน: ยิงจากในโซนออกไปนอกโซน ถ้าไม่ชนกำแพง/ประตูปิด = ช่องเปิด
    private void BuildProbes(Zone z)
    {
        Bounds b = z.bounds;
        float y = b.min.y + eyeHeight;
        AddSide(z, new Vector3(b.min.x, y, b.max.z), new Vector3(b.max.x, y, b.max.z), Vector3.forward);
        AddSide(z, new Vector3(b.min.x, y, b.min.z), new Vector3(b.max.x, y, b.min.z), Vector3.back);
        AddSide(z, new Vector3(b.max.x, y, b.min.z), new Vector3(b.max.x, y, b.max.z), Vector3.right);
        AddSide(z, new Vector3(b.min.x, y, b.min.z), new Vector3(b.min.x, y, b.max.z), Vector3.left);
    }

    private static void AddSide(Zone z, Vector3 a, Vector3 b, Vector3 outward)
    {
        float len = Vector3.Distance(a, b);
        for (float s = ProbeStep * 0.5f; s < len; s += ProbeStep)
        {
            Vector3 edge = Vector3.Lerp(a, b, s / len);
            z.probes.Add(new Probe { edge = edge, from = edge - outward * ProbeInset, to = edge + outward * ProbeReach });
        }
    }

    private void UpdateProbeStates()
    {
        foreach (Zone z in zones)
            for (int i = 0; i < z.probes.Count; i++)
            {
                Probe p = z.probes[i];
                p.open = !Blocked(p.from, p.to);
                z.probes[i] = p;
            }
    }

    // มีกำแพง/กรอบประตู/ประตูที่ปิด/หน้าต่าง ขวางระหว่าง 2 จุดไหม (เฟอร์นิเจอร์ไม่นับ)
    private bool Blocked(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float dist = d.magnitude;
        if (dist < 0.001f) return false;
        int n = Physics.RaycastNonAlloc(a, d / dist, hitBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (blockers.Contains(hitBuffer[i].collider) && IsBlocking(hitBuffer[i].collider)) return true;
        return false;
    }

    private static bool IsBlocking(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy;

    // ประตูเปิด/ปิด (ตัวชนประตูเปิด-ปิดตามสถานะ) → คำนวณช่องเปิดใหม่
    private bool DoorsChanged()
    {
        bool changed = false;
        for (int i = 0; i < doorColliders.Count; i++)
        {
            bool now = IsBlocking(doorColliders[i]);
            if (now == doorStates[i]) continue;
            doorStates[i] = now;
            changed = true;
        }
        return changed;
    }

    // ผู้เล่นมองเห็นช่องเปิดของห้องนี้ไหม
    private bool SeesOpening(Zone z)
    {
        Vector3 p = player.position;
        if (Vector3.Distance(z.bounds.ClosestPoint(new Vector3(p.x, z.bounds.center.y, p.z)), new Vector3(p.x, z.bounds.center.y, p.z)) > revealDistance) return false;
        foreach (Probe pr in z.probes)
        {
            if (!pr.open) continue;
            Vector3 eye = new Vector3(p.x, pr.edge.y, p.z);
            if ((pr.edge - eye).sqrMagnitude > revealDistance * revealDistance) continue;
            if (!Blocked(eye, pr.edge)) return true;
        }
        return false;
    }

    private Zone Smallest(Vector3 c)
    {
        Zone best = null;
        foreach (Zone z in zones)
        {
            if (c.x < z.bounds.min.x || c.x > z.bounds.max.x || c.z < z.bounds.min.z || c.z > z.bounds.max.z) continue;
            if (best == null || Area(z.bounds) < Area(best.bounds)) best = z;
        }
        return best;
    }

    private Zone Nearest(Vector3 c, float maxDistance)
    {
        Zone best = null; float bestD = maxDistance;
        foreach (Zone z in zones)
        {
            Vector3 flat = new Vector3(c.x, z.bounds.center.y, c.z);
            float d = Vector3.Distance(z.bounds.ClosestPoint(flat), flat);
            if (d <= bestD) { bestD = d; best = z; }
        }
        return best;
    }

    private static float Area(Bounds b) => b.size.x * b.size.z;

    private void OnEnable() { if (initialized) { SetFog(false); Refresh(force: true, instant: true); } }

    private void SetFog(bool visible)
    {
        if (!hideFogPlanes && !visible) return;
        foreach (MeshRenderer f in fogPlanes) if (f != null) f.forceRenderingOff = !visible;
    }

    private void OnDisable()
    {
        // ปิดตัวนี้ = คืนทุกอย่าง
        if (!initialized) return;
        foreach (Zone z in zones)
        {
            z.show = true;
            SetContents(z, true);
            if (z.cover != null) { SetCoverTall(z.cover); z.cover.SetDark(false); }
        }
        foreach (Look l in lookList)
        {
            l.cut = false;
            l.dim = 0f;
            if (l.renderer == null) continue;
            l.shown = MaterialAlpha(l.renderer);
            if (l.isStructure) l.renderer.shadowCastingMode = l.originalShadow;
            ExitAlphaMode(l);
            ClearBlock(l);
        }
        SetFog(true);
        hasState = false;
        lastVisible.Clear();
    }

    private void Update()
    {
        if (!initialized) return;
        bool doorsChanged = DoorsChanged();
        if (doorsChanged) UpdateProbeStates();
        recheckTimer -= Time.deltaTime;
        bool zoneChanged = player != null && ZoneAt(player.position) != current;   // เปลี่ยนห้อง = เช็คทันที ไม่รอรอบ
        if (doorsChanged || zoneChanged || recheckTimer <= 0f)
        {
            recheckTimer = RecheckInterval;
            Refresh(force: doorsChanged, instant: false);
        }
        TickFades();
        TickLooks(fadeTime > 0f ? Time.deltaTime / fadeTime : 1f);
    }

    // ห้องที่ยืนอยู่ — ยืนในร่องระหว่างโซน (ความหนากำแพง/ช่องประตู) ยังนับเป็นห้องเดิม ไม่สลับไปมา
    private Zone ZoneAt(Vector3 pos)
    {
        Zone z = Smallest(pos);
        if (z != null || current == null) return z;
        Vector3 flat = new Vector3(pos.x, current.bounds.center.y, pos.z);
        return Vector3.Distance(current.bounds.ClosestPoint(flat), flat) <= StickyDistance ? current : null;
    }

    private void Refresh(bool force, bool instant)
    {
        if (player == null) return;
        Zone z = ZoneAt(player.position);
        HashSet<Zone> visible = ComputeVisible(z, out float front, out Vector3 axis);
        if (!force && hasState && z == current && visible.SetEquals(lastVisible)) return;
        current = z;
        hasState = true;
        lastVisible.Clear();
        lastVisible.UnionWith(visible);
        Apply(visible, front, axis, instant);
    }

    // ห้องที่เห็น = ห้องที่อยู่ (+ ทางเดินแนวเดียวกัน) + ห้องที่มองเห็นช่องเปิด (ไม่นับห้องหน้ากล้อง)
    private HashSet<Zone> ComputeVisible(Zone at, out float front, out Vector3 axis)
    {
        var visible = new HashSet<Zone>();
        if (at != null)
        {
            visible.Add(at);
            if (at.isCorridor)
                foreach (Zone z in zones)
                    if (z.isCorridor && SameLine(z, at)) visible.Add(z);
        }

        axis = DepthAxis();
        front = float.MaxValue;
        foreach (Zone z in visible) front = Mathf.Min(front, NearDepth(z.bounds, axis));
        if (visible.Count == 0) front = Vector3.Dot(player.position, axis) - 1f;

        if (revealThroughOpenings)
            foreach (Zone z in zones)
            {
                if (visible.Contains(z) || IsInFront(z, front, axis)) continue;
                if (SeesOpening(z)) visible.Add(z);
            }
        return visible;
    }

    private bool IsInFront(Zone z, float front, Vector3 axis) => cutAwayFront && FarDepth(z.bounds, axis) <= front + 0.3f;

    // ค่อยๆ มืด/สว่าง · ห้องที่กำลังมืด: ของในห้องมืดตาม แล้วซ่อนตอนดำสนิท
    private void TickFades()
    {
        float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
        foreach (Zone z in zones)
        {
            if (z.cover != null)
            {
                // เปลี่ยนทรงกล่อง (แบน ↔ สูง) ตอนมองไม่เห็นกล่องเท่านั้น — ต้องเช็คก่อนเริ่มเฟด
                // ไม่งั้นกล่องจะเฟดในทรงเก่า (กล่องสูงบังทางเดิน) แล้วเด้งเปลี่ยนทรงตอนจบ · ห้องที่ดำสนิทอยู่แล้วเปลี่ยนได้เลย
                if (z.cover.Alpha <= 0.001f || (!z.show && z.cover.Alpha >= 0.999f)) ApplyCoverShape(z);
                z.cover.Tick(step);
            }
            float dark = z.cover != null ? z.cover.Alpha : (z.show ? 0f : 1f);

            if (!z.show && dark >= 0.999f)
            {
                if (z.contentsShown) SetContents(z, false);
                SetDim(z, 0f);
            }
            else SetDim(z, z.contentsShown ? dark : 0f);
        }
    }

    // ทิศ "ลึกเข้าไปในจอ" ตามกล้อง (ตัดแกนตั้งทิ้ง) — ของที่ลึกน้อยกว่าเส้นหน้า = อยู่ระหว่างกล้องกับผู้เล่น
    private static Vector3 DepthAxis()
    {
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : Vector3.forward;
        f.y = 0f;
        return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
    }

    private void Apply(HashSet<Zone> visible, float front, Vector3 axis, bool instant)
    {
        foreach (Zone z in zones)
        {
            bool show = !darkenOtherRooms || visible.Contains(z);
            z.show = show;
            if (show) SetContents(z, true);                 // โชว์ทันที แล้วกล่องดำค่อยๆ จาง
            else if (instant) SetContents(z, false);        // ซ่อน: รอกล่องดำมืดสนิทก่อน (TickFades)
            if (z.cover == null) continue;
            if (instant) z.cover.SetDark(!show); else z.cover.FadeTo(!show);
            z.wantFlat = IsInFront(z, front, axis);
            if (instant) ApplyCoverShape(z);
        }

        // กำแพงหน้ากล้อง: ค่อยๆ จางหาย (ทันทีตอนเริ่มเกม)
        foreach (MeshRenderer w in structure)
        {
            if (w == null) continue;
            Look l = LookOf(w);
            l.cut = cutAwayFront && FarDepth(w.bounds, axis) <= front + 0.35f;
        }
        if (instant) TickLooks(1f);
    }

    // ทางเดินแนวเดียวกัน: แนวนอนด้วยกันที่ช่วง Z ซ้อนกัน หรือแนวตั้งด้วยกันที่ช่วง X ซ้อนกัน
    private static bool SameLine(Zone a, Zone b)
    {
        if (a.horizontal != b.horizontal) return false;
        return a.horizontal
            ? Mathf.Min(a.bounds.max.z, b.bounds.max.z) - Mathf.Max(a.bounds.min.z, b.bounds.min.z) > 0.5f
            : Mathf.Min(a.bounds.max.x, b.bounds.max.x) - Mathf.Max(a.bounds.min.x, b.bounds.min.x) > 0.5f;
    }

    private static float NearDepth(Bounds b, Vector3 axis) =>
        Mathf.Min(Mathf.Min(Vector3.Dot(new Vector3(b.min.x, 0, b.min.z), axis), Vector3.Dot(new Vector3(b.max.x, 0, b.max.z), axis)),
                  Mathf.Min(Vector3.Dot(new Vector3(b.min.x, 0, b.max.z), axis), Vector3.Dot(new Vector3(b.max.x, 0, b.min.z), axis)));

    private static float FarDepth(Bounds b, Vector3 axis) =>
        Mathf.Max(Mathf.Max(Vector3.Dot(new Vector3(b.min.x, 0, b.min.z), axis), Vector3.Dot(new Vector3(b.max.x, 0, b.max.z), axis)),
                  Mathf.Max(Vector3.Dot(new Vector3(b.min.x, 0, b.max.z), axis), Vector3.Dot(new Vector3(b.max.x, 0, b.min.z), axis)));

    private void ApplyCoverShape(Zone z)
    {
        if (z.wantFlat) SetCoverFlat(z.cover); else SetCoverTall(z.cover);
    }

    private void SetCoverTall(RoomCover c)
    {
        if (!coverTall.TryGetValue(c, out var t)) return;
        c.transform.position = t.pos;
        c.transform.localScale = t.scale;
    }

    // ห้องที่อยู่หน้ากล้อง: เหลือแค่แผ่นดำติดพื้น (ไม่บัง)
    private void SetCoverFlat(RoomCover c)
    {
        if (!coverTall.TryGetValue(c, out var t)) return;
        c.transform.position = new Vector3(t.pos.x, 0.03f, t.pos.z);
        c.transform.localScale = new Vector3(t.scale.x, 0.04f, t.scale.z);
    }

    private static void SetContents(Zone z, bool visible)
    {
        z.contentsShown = visible;
        foreach (Renderer r in z.contents) if (r != null) r.forceRenderingOff = !visible;
    }

    // ---------- หน้าตาแต่ละชิ้น (เฟดกำแพง / ทำให้ของมืด / ไล่ตามความใสของ RoomWallFade) ----------

    private Look LookOf(Renderer r)
    {
        if (looks.TryGetValue(r, out Look l)) return l;
        l = new Look { renderer = r, originalShadow = r.shadowCastingMode, shown = MaterialAlpha(r) };
        looks[r] = l;
        lookList.Add(l);
        return l;
    }

    // ของในห้องมืดลงตามกล่องดำ (สไปรท์/พาร์ติเคิลไม่แตะ)
    private void SetDim(Zone z, float dark)
    {
        foreach (Renderer r in z.contents)
            if (r != null && looks.TryGetValue(r, out Look l)) l.dim = dark;
    }

    private static float MaterialAlpha(Renderer r)
    {
        Material m = r.sharedMaterial;
        return m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId).a : 1f;
    }

    // วัสดุโปร่งใส = เฟดความใสได้ · วัสดุทึบ = ตัด/โชว์ทันที (เหมือนเดิม)
    private static bool CanFade(Renderer r) => r.sharedMaterial != null && r.sharedMaterial.renderQueue > (int)RenderQueue.GeometryLast;

    private void TickLooks(float step)
    {
        foreach (Look l in lookList)
        {
            Renderer r = l.renderer;
            if (r == null) continue;
            float actual = MaterialAlpha(r);   // RoomWallFade ของปอเปลี่ยนค่านี้ทันที → ไล่ตามให้นุ่ม
            float target = l.cut ? 0f : actual;
            bool fadeable = CanFade(r);
            l.shown = fadeable ? Mathf.MoveTowards(l.shown, target, step) : target;

            if (l.isStructure)
            {
                // จางหมดแล้วค่อยเหลือแค่เงา (ยังบังแสง ไฟห้องไม่รั่ว)
                ShadowCastingMode want = l.cut && l.shown <= 0.001f ? ShadowCastingMode.ShadowsOnly : l.originalShadow;
                if (r.shadowCastingMode != want) r.shadowCastingMode = want;
            }

            bool fading = fadeable && Mathf.Abs(l.shown - actual) > 0.001f;
            if (fading) EnterAlphaMode(l);
            else if (l.alphaMode && actual >= 0.999f) ExitAlphaMode(l);   // กลับมาทึบเต็มแล้วค่อยคืนวัสดุเดิม (หน้าตาเหมือนกันตอนทึบ)

            bool needBlock = l.dim > 0.001f || fading;
            if (needBlock) WriteBlock(l, fadeable);
            else if (l.written) ClearBlock(l);
        }
    }

    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
    private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");

    private static void EnterAlphaMode(Look l)
    {
        if (l.alphaMode) return;
        l.mats = l.renderer.materials;   // สำเนาของ renderer นี้ (ถ้ามีอยู่แล้ว เช่นที่ RoomWallFade ทำ = ใช้อันเดิม) — ไม่แตะไฟล์วัสดุ
        l.savedBlend = new float[l.mats.Length * 4];
        for (int i = 0; i < l.mats.Length; i++)
        {
            Material m = l.mats[i];
            if (m == null || !m.HasProperty(SrcBlendId)) continue;
            int k = i * 4;
            l.savedBlend[k] = m.GetFloat(SrcBlendId);
            l.savedBlend[k + 1] = m.GetFloat(DstBlendId);
            l.savedBlend[k + 2] = m.HasProperty(SrcBlendAlphaId) ? m.GetFloat(SrcBlendAlphaId) : 1f;
            l.savedBlend[k + 3] = m.HasProperty(DstBlendAlphaId) ? m.GetFloat(DstBlendAlphaId) : (float)BlendMode.OneMinusSrcAlpha;
            // เปลี่ยนแค่วิธีผสมสี (ไม่แตะคีย์เวิร์ด — แตะแล้วต้องคอมไพล์เชดเดอร์รุ่นใหม่ ขึ้นสีฟ้า/อาจหายตอน Build)
            // premultiply + SrcAlpha → สี/แสงสะท้อนคูณความใสอีกรอบ → ความใส 0 = หายจริง
            m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty(SrcBlendAlphaId)) m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            if (m.HasProperty(DstBlendAlphaId)) m.SetFloat(DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
        }
        l.alphaMode = true;
    }

    private static void ExitAlphaMode(Look l)
    {
        if (!l.alphaMode) return;
        for (int i = 0; i < l.mats.Length; i++)
        {
            Material m = l.mats[i];
            if (m == null || !m.HasProperty(SrcBlendId)) continue;
            int k = i * 4;
            m.SetFloat(SrcBlendId, l.savedBlend[k]);
            m.SetFloat(DstBlendId, l.savedBlend[k + 1]);
            if (m.HasProperty(SrcBlendAlphaId)) m.SetFloat(SrcBlendAlphaId, l.savedBlend[k + 2]);
            if (m.HasProperty(DstBlendAlphaId)) m.SetFloat(DstBlendAlphaId, l.savedBlend[k + 3]);
        }
        l.alphaMode = false;
    }

    private void WriteBlock(Look l, bool fadeable)
    {
        block ??= new MaterialPropertyBlock();
        l.renderer.GetSharedMaterials(matBuffer);
        for (int i = 0; i < matBuffer.Count; i++)
        {
            Material m = matBuffer[i];
            if (m == null || !m.HasProperty(BaseColorId)) continue;
            Color c = m.GetColor(BaseColorId);
            float k = 1f - l.dim;
            block.Clear();
            block.SetColor(BaseColorId, new Color(c.r * k, c.g * k, c.b * k, fadeable ? l.shown : c.a));
            l.renderer.SetPropertyBlock(block, i);
        }
        l.written = true;
    }

    private void ClearBlock(Look l)
    {
        if (!l.written || l.renderer == null) return;
        l.renderer.GetSharedMaterials(matBuffer);
        for (int i = 0; i < matBuffer.Count; i++) l.renderer.SetPropertyBlock(null, i);
        l.written = false;
    }
}
