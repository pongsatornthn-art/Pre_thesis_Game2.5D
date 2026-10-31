using UnityEngine;

/// <summary>
/// แผ่นเงาทรงคน — รูปตัวละครล่องหน (สร้างเงาอย่างเดียว) ที่ก๊อปท่าจากสไปรท์ตัวจริงทุกเฟรม
/// แล้วหมุนหันหน้าเข้าหาไฟดวงที่ส่องตัวละครแรงที่สุด → เงาเป็นทรงคนทุกทิศ
/// (ถ้าใช้สไปรท์ตัวจริงสร้างเงาตรงๆ แผ่นจะหันเข้ากล้องตลอด ไฟที่อยู่ด้านข้างได้เงาเป็นเส้นบางๆ)
/// วางเป็นลูกของตัวละคร 1 แผ่น · SpriteRenderer ใช้วัสดุที่สร้างเงาแบบตัดขอบรูปได้ (เช่น M_SpriteCharacter_Lit)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShadowCard : MonoBehaviour
{
    [Tooltip("สไปรท์ตัวจริงที่จะก๊อปท่า (เช่น Player/GFX)")]
    [SerializeField] private SpriteRenderer source;

    [Tooltip("หมุนหาไฟเร็วแค่ไหน (องศา/วินาที) — ช้าไปเงาจะตามไม่ทันตอนเดินผ่านใต้ไฟ")]
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Tooltip("หาไฟดวงที่แรงสุดใหม่ทุกกี่วินาที")]
    [SerializeField, Min(0.02f)] private float rescanEvery = 0.1f;

    [Tooltip("ความสูงแผ่นเงาเทียบตัวจริง (1 = สูงเท่าตัว) — ไฟในบ้านอยู่ต่ำ เงาเต็มความสูงจะยาวเกินตัว · ลดค่านี้ = เงาสั้นลง (ยังติดเท้าเหมือนเดิม)")]
    [SerializeField, Range(0.3f, 1f)] private float heightScale = 0.75f;

    [Tooltip("พื้นอยู่ต่ำกว่าจุดกลางตัวละคร (ตัวแม่) กี่เมตร — ผู้เล่นยืนที่ y 0.35 บนพื้น y 0 = -0.35")]
    [SerializeField] private float groundOffset = -0.35f;

    [Tooltip("ช่องว่างใสใต้เท้าในรูปสไปรท์ (พิกเซล) — รูปผู้เล่นเว้น 10–30 px")]
    [SerializeField, Min(0f)] private float footPaddingPixels = 15f;

    [Tooltip("เงาจางใต้เท้า (BlobShadow) — ถ้าใส่ จะถูกย้ายไปอยู่ใต้เท้าที่เห็นในรูปทุกเฟรม · เว้นว่างได้")]
    [SerializeField] private Transform footBlob;

    private SpriteRenderer card;
    private Light[] lights;
    private Vector3 toLight = Vector3.back;
    private Vector3 baseScale;
    private float nextScan;

    private void Awake()
    {
        card = GetComponent<SpriteRenderer>();
        card.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        baseScale = transform.localScale;
    }

    private void Start() => lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    private void LateUpdate()
    {
        if (source == null) return;
        card.sprite = source.sprite;
        card.flipX = source.flipX;
        card.flipY = source.flipY;

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + rescanEvery;
            if (TryFindStrongestLight(out Vector3 dir)) toLight = dir;
        }

        // แผ่นตั้งตรง หันหน้าเข้าหาไฟ (หมุนแค่แกนตั้ง)
        Quaternion target = Quaternion.LookRotation(toLight, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * heightScale, baseScale.z);

        // วางให้เท้าในรูปแตะพื้นตรงจุดที่เท้าตัวจริงอยู่ — ตัวจริงเอียงเข้ากล้อง แผ่นเงาตั้งตรง
        // ถ้าเท้าแผ่นเงาลอยเหนือพื้น เงาจะเริ่มห่างจากเท้า (ยิ่งไฟไกลยิ่งห่าง)
        if (card.sprite == null) return;
        Vector3 footLocal = new Vector3(0f, card.sprite.bounds.min.y + footPaddingPixels / card.sprite.pixelsPerUnit, 0f);
        Vector3 foot = source.transform.TransformPoint(footLocal);
        float groundY = transform.parent != null ? transform.parent.position.y + groundOffset : foot.y;
        // เท้าที่วาดในรูปลอยเหนือพื้นนิดหน่อย → ใช้จุดบนพื้นที่ "อยู่ข้างหลังเท้าในสายตากล้อง" แทน เงาจะเริ่มตรงเท้าที่เห็นบนจอพอดี
        Camera cam = Camera.main;
        Vector3 ray = cam != null ? foot - cam.transform.position : Vector3.down;
        foot = ray.y < -0.001f ? foot + ray * ((groundY - foot.y) / ray.y) : new Vector3(foot.x, groundY, foot.z);
        transform.position = foot - transform.rotation * Vector3.Scale(footLocal, transform.lossyScale);
        if (footBlob != null) footBlob.position = new Vector3(foot.x, foot.y + 0.02f, foot.z);
    }

    // ไฟที่สร้างเงาได้ + ส่องถึงตัวละคร → เลือกดวงที่แรงสุดที่ตำแหน่งนี้ (ความแรง / ระยะ² · จางตามขอบรัศมี)
    private bool TryFindStrongestLight(out Vector3 dir)
    {
        dir = Vector3.zero;
        if (lights == null) return false;
        Vector3 p = transform.position;
        float best = 0f;
        foreach (Light l in lights)
        {
            if (l == null || !l.isActiveAndEnabled || l.shadows == LightShadows.None || l.intensity <= 0f) continue;
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            Vector3 d = l.transform.position - p;
            float distSq = d.sqrMagnitude;
            float rangeSq = l.range * l.range;
            if (distSq >= rangeSq) continue;
            float edge = 1f - distSq / rangeSq;
            float strength = l.intensity / Mathf.Max(distSq, 0.25f) * edge * edge;
            d.y = 0f;
            if (strength <= best || d.sqrMagnitude < 0.01f) continue;   // ไฟอยู่ตรงหัวพอดี = ไม่มีทิศ ใช้ทิศเดิม
            best = strength;
            dir = d.normalized;
        }
        return best > 0f;
    }
}
