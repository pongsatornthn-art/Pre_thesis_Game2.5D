using System.Collections;
using UnityEngine;

/// <summary>
/// ของก่อสร้างห้อยค้างอยู่ → ผู้เล่นวิ่งลอด → บอสวิ่งตามลอดมา → พอบอสพ้นไป ของถล่มลงมาขวางทาง (แบบคัทซีนที่เล่นเอง)
///
/// ตั้งในซีน:
///   ตัวนี้ (FallingDebris)
///     ├ Hanging (เปิดไว้)  = ชิ้นของในท่า "ห้อยค้าง" — ไม่มี Collider (ลอดผ่านได้)
///     └ Visual  (ปิดไว้)   = ชิ้นของเดียวกันในท่า "กองบนพื้น" + BoxCollider + BreakableObstacle (ตีด้วยท่อแล้วแตก)
///   ชิ้นใน Hanging กับ Visual/Pieces **เรียงลำดับเดียวกัน** → ตอนถล่ม ชิ้นที่ i ใน Hanging ร่วง/หมุนไปทับตำแหน่งชิ้นที่ i ในกอง
///   (จัดท่าห้อย / ท่ากอง ใน Editor ได้อิสระ แค่ลากชิ้นในแต่ละกลุ่ม)
///
/// จังหวะ:
///   ผู้เล่นเข้าใกล้ (Pressure Radius) → ขอให้บอสจี้หลังห่างแค่ Pressure Gap → บอสลอดตามมาติดๆ เสมอ ไม่ว่าผู้เล่นวิ่งเร็วแค่ไหน
///   ผู้เล่นใกล้ (Wobble Radius) → ของสั่นเอียด
///   บอสเข้าระยะ Trigger Radius แล้ววิ่งพ้นไป → ถล่ม (หลังบอส ไม่ขวางหน้าบอส) + กล้องสั่น
///   โดนจับ → ต่อ FirstChaseStalker.onCaught → ResetDebris · หนีรอดก่อนบอสผ่าน → onEscaped → DropNow
///
/// จำสถานะผ่านธงเรื่องราว → โหลดเซฟแล้วกองยังอยู่ (ถ้ายังไม่ได้ตีแตก)
/// [Claude 2026-11-07]
/// </summary>
public class FallingDebris : MonoBehaviour
{
    [Header("ร่วงเมื่อไหร่")]
    [Tooltip("บอสไล่ครั้งแรก — ทำงานเฉพาะตอนมันกำลังไล่อยู่")]
    public FirstChaseStalker chaser;

    [Tooltip("บอสเข้าใกล้จุดนี้ในระยะเท่านี้ (เมตร) แล้ววิ่งพ้นไป = ถล่ม")]
    public float triggerRadius = 2.2f;

    [Header("คุมจังหวะบอส (ให้เหมือนคัทซีน)")]
    [Tooltip("ผู้เล่นเข้าใกล้จุดนี้เท่านี้ (เมตร) → เริ่มสั่งบอสจี้หลัง · 0 = ปิด (บอสวิ่งความเร็วปกติ — เจ้าของเลือกแบบนี้ 2026-11-08)")]
    public float pressureRadius = 0f;
    [Tooltip("บอสจี้ห่างผู้เล่นไม่เกินกี่เมตร")]
    public float pressureGap = 3f;
    [Tooltip("ความเร็วตอนบอสเร่งเข้ามาจี้")]
    public float pressureSprintSpeed = 5.2f;

    [Header("ท่าห้อย (ก่อนถล่ม)")]
    [Tooltip("กลุ่มชิ้นของในท่าห้อยค้าง (ห้ามมี Collider)")]
    public Transform hanging;
    [Tooltip("ผู้เล่นเข้าใกล้เท่านี้ → ของสั่นเอียด")]
    public float wobbleRadius = 6f;
    public float wobbleDegrees = 2.5f;

    [Header("ท่ากอง (หลังถล่ม)")]
    [Tooltip("กองของบนพื้น (ปิดไว้ตอนเริ่ม) — ใส่ BoxCollider + BreakableObstacle ไว้ที่ตัวนี้")]
    public GameObject visual;
    [Tooltip("กลุ่มชิ้นในกอง (ลูกของ Visual) เรียงลำดับเดียวกับ Hanging")]
    public Transform landedPieces;

    [Header("ตอนถล่ม")]
    public float fallTime = 0.45f;
    [Tooltip("แต่ละชิ้นเริ่มร่วงห่างกันสุ่มไม่เกินกี่วินาที (ให้ดูไม่พร้อมกันเป๊ะ)")]
    public float stagger = 0.18f;
    public float cameraShake = 0.35f;
    public AudioClip creakSfx;
    public AudioClip fallSfx;
    [Tooltip("ฝุ่นตอนกระแทกพื้น — วางเป็นลูกของตัวนี้ (ไม่ใช่ลูกของ Visual)")]
    public ParticleSystem landFx;

    [Header("จำสถานะ (เซฟ)")]
    [Tooltip("ธงที่ขึ้นเมื่อร่วงแล้ว — เว้นว่างได้ จะใช้ Save Key แทน")]
    public StoryFlagId fallenFlag;
    [Tooltip("รหัสจำสถานะ (สร้างให้อัตโนมัติ ห้ามซ้ำ)")]
    public string saveKey;

    public bool HasFallen { get; private set; }

    private bool bossWasNear;
    private bool pressuring;
    private bool creaked;
    private Vector3[] hangPos;
    private Quaternion[] hangRot;
    private float wobblePhase;

    private void Reset()
    {
        saveKey = "debris_" + System.Guid.NewGuid().ToString("N").Substring(0, 10);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(saveKey)) Reset();
    }

    private void Awake()
    {
        CacheHangingPose();
        ShowHanging();
        wobblePhase = Random.value * 10f;
    }

    private void OnEnable()
    {
        StartCoroutine(SyncNextFrame());
    }

    private void OnDisable()
    {
        StopPressure();
    }

    private IEnumerator SyncNextFrame()
    {
        yield return null;
        while (SaveRestoreScope.IsBusy) yield return null;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null && HasFlag(flags) && !HasFallen)
        {
            HasFallen = true;
            ShowLanded();   // ถ้าตีแตกไปแล้ว BreakableObstacle บน Visual จะซ่อนตัวเองต่อ
        }
    }

    private void Update()
    {
        if (HasFallen) return;

        bool chasing = chaser != null && chaser.IsChasing;
        Transform player = chasing ? FindPlayer() : null;
        float playerDist = player != null ? FlatDistance(player.position) : float.MaxValue;

        // สั่นเอียดตอนผู้เล่นเข้าใกล้ (เฉพาะตอนไล่ — เวลาปกติของห้อยนิ่งๆ)
        Wobble(chasing && playerDist <= wobbleRadius);

        if (!chasing) { bossWasNear = false; StopPressure(); return; }

        // คุมจังหวะ: ผู้เล่นเข้าใกล้ → บอสจี้หลัง (สั่งซ้ำทุกเฟรม — บอสเริ่มไล่ใหม่จะล้างคำสั่งทิ้ง)
        if (pressureRadius > 0f && playerDist <= pressureRadius)
        {
            pressuring = true;
            chaser.ApplyPressure(pressureGap, pressureSprintSpeed);
        }
        else StopPressure();

        if (!creaked && playerDist <= wobbleRadius) { creaked = true; PlaySfx(creakSfx); }

        bool bossNear = FlatDistance(chaser.transform.position) <= triggerRadius;
        if (bossNear) bossWasNear = true;
        else if (bossWasNear) Drop(animate: true);   // บอสเพิ่งวิ่งพ้นไป → ถล่มข้างหลังบอส
    }

    /// <summary>ร่วงทันทีแบบไม่เล่นอนิเมชัน — ต่อกับ FirstChaseStalker.onEscaped</summary>
    public void DropNow()
    {
        if (!HasFallen) Drop(animate: false);
    }

    /// <summary>กลับเป็นท่าห้อย — ต่อกับ FirstChaseStalker.onCaught (โดนจับ เริ่มวิ่งใหม่)</summary>
    public void ResetDebris()
    {
        StopAllCoroutines();
        StopPressure();
        HasFallen = false;
        bossWasNear = false;
        creaked = false;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null)
        {
            if (fallenFlag != null) flags.Clear(fallenFlag);
            else if (!string.IsNullOrEmpty(saveKey)) flags.ClearKey(saveKey);
        }

        ShowHanging();
    }

    private void Drop(bool animate)
    {
        HasFallen = true;
        StopPressure();

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
        if (flags != null)
        {
            if (fallenFlag != null) flags.Set(fallenFlag);
            else if (!string.IsNullOrEmpty(saveKey)) flags.SetKey(saveKey);
        }

        if (animate && hanging != null && landedPieces != null) StartCoroutine(FallRoutine());
        else ShowLanded();
    }

    private IEnumerator FallRoutine()
    {
        int n = Mathf.Min(hanging.childCount, landedPieces.childCount);
        float[] delay = new float[n];
        Vector3[] fromPos = new Vector3[n];
        Quaternion[] fromRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            delay[i] = Random.Range(0f, stagger);
            fromPos[i] = hanging.GetChild(i).position;
            fromRot[i] = hanging.GetChild(i).rotation;
        }

        float total = fallTime + stagger;
        float t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Clamp01((t - delay[i]) / fallTime);
                float kPos = k * k;                       // เร่งลงแบบของตก
                float kRot = 1f - (1f - k) * (1f - k);    // หมุนเร็วตอนแรก
                Transform h = hanging.GetChild(i);
                Transform l = landedPieces.GetChild(i);
                h.position = Vector3.LerpUnclamped(fromPos[i], l.position, kPos);
                h.rotation = Quaternion.Slerp(fromRot[i], l.rotation, kRot);
            }
            yield return null;
        }

        ShowLanded();
        PlaySfx(fallSfx);
        if (landFx != null) landFx.Play();
        if (cameraShake > 0f && Combat.Gun.GunCameraShake.Instance != null)
            Combat.Gun.GunCameraShake.Instance.Shake(0.3f, cameraShake);
    }

    private void Wobble(bool on)
    {
        if (hanging == null || hangRot == null) return;
        int n = Mathf.Min(hanging.childCount, hangRot.Length);
        for (int i = 0; i < n; i++)
        {
            Transform c = hanging.GetChild(i);
            if (!on) { c.localRotation = hangRot[i]; continue; }
            float a = Mathf.Sin(Time.time * (14f + i * 3f) + wobblePhase + i) * wobbleDegrees;
            c.localRotation = hangRot[i] * Quaternion.Euler(a, 0f, a * 0.6f);
        }
    }

    private void CacheHangingPose()
    {
        if (hanging == null) return;
        int n = hanging.childCount;
        hangPos = new Vector3[n];
        hangRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            hangPos[i] = hanging.GetChild(i).localPosition;
            hangRot[i] = hanging.GetChild(i).localRotation;
        }
    }

    private void ShowHanging()
    {
        if (hanging != null)
        {
            if (hangPos != null)
                for (int i = 0; i < Mathf.Min(hanging.childCount, hangPos.Length); i++)
                {
                    hanging.GetChild(i).localPosition = hangPos[i];
                    hanging.GetChild(i).localRotation = hangRot[i];
                }
            hanging.gameObject.SetActive(true);
        }
        if (visual != null) visual.SetActive(false);
    }

    private void ShowLanded()
    {
        if (hanging != null) hanging.gameObject.SetActive(false);
        if (visual != null) visual.SetActive(true);
    }

    private void StopPressure()
    {
        if (!pressuring) return;
        pressuring = false;
        if (chaser != null) chaser.ReleasePressure();
    }

    private Transform cachedPlayer;
    private Transform FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) cachedPlayer = p.transform;
        }
        return cachedPlayer;
    }

    private float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
        if (audio != null) audio.PlaySFX(clip, transform.position);
    }

    private bool HasFlag(IStoryFlags flags)
    {
        if (fallenFlag != null) return flags.Has(fallenFlag);
        return !string.IsNullOrEmpty(saveKey) && flags.HasKey(saveKey);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, pressureRadius);
    }
}
