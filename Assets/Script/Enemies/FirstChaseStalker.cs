using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// บอสไล่ล่าตอนเข้า PTSD ครั้งแรก (ฉากสอนเล่น) — ไล่ล็อกผู้เล่น 100% จนกว่าผู้เล่นจะเข้าห้องครัว
///
/// ต่างจาก StalkerAI ตัวหลัก (ของปอ — ไม่ได้แตะ):
///   ไม่มีโหมดซุ่ม/สงสัย/เดินหา · ไม่วาร์ปหนี · ไม่มีมินิเกมดิ้นหลุด
///
/// วิธีไล่: "เดินตามรอยเท้า" — จดตำแหน่งผู้เล่นเป็นจุดๆ แล้ววิ่งตามจุดเหล่านั้น
///   → ไม่ต้องใช้ NavMesh · ไม่ติดกำแพง (ผู้เล่นเดินผ่านได้ = บอสผ่านได้) · ผู้เล่นหลบมุมแล้วบอสไม่หลง
///   ถ้าอยู่ใกล้และมองเห็นกันตรงๆ (ไม่มีกำแพงขวาง) → พุ่งตรงเข้าหาเลย ไม่ต้องอ้อมตามรอย
///
/// โดนจับ → จอดำ → ผู้เล่นกลับจุดเริ่มไล่ · บอสกลับจุดเกิด → เริ่มไล่ใหม่ (ไม่เล่นคัทซีนซ้ำ ไม่ออกจากโลก PTSD)
/// ผู้เล่นเข้าครัว (หรือออกจากโลก PTSD ทางอื่น) → บอสค่อยๆ จางหาย แล้วซ่อนตัวกลับไปรอที่จุดเกิด
///
/// วิธีเริ่มไล่: ใส่ช่อง Start After Cutscene = Cutscene_FirstPTSD (คัทซีนจบแล้วไล่เอง ไม่ต้องแก้ Timeline ของปอ)
///   หรือเรียก BeginChase() จาก UnityEvent / Signal ก็ได้
/// ตัว object เปิดไว้ตลอด — ภาพจะซ่อนเองจนกว่าจะเริ่มไล่
/// </summary>
public class FirstChaseStalker : MonoBehaviour
{
    [Header("ความเร็ว")]
    [Tooltip("ความเร็ววิ่งไล่ปกติ — ควรช้ากว่าผู้เล่นนิดเดียว (ผู้เล่น = PlayerMovement ช่อง Base Speed)")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3.2f;
    [Tooltip("ถ้าผู้เล่นทิ้งห่างเกินระยะนี้ (เมตร) บอสจะเร่งขึ้นจนตามทัน — ให้รู้สึกจี้หลังตลอด")]
    [SerializeField, Min(0f)] private float catchUpDistance = 9f;
    [Tooltip("ความเร็วตอนเร่งตามทัน")]
    [SerializeField, Min(0.1f)] private float catchUpSpeed = 4.5f;

    [Header("จับตัว")]
    [Tooltip("เข้าใกล้ผู้เล่นเท่านี้ (เมตร) = จับได้")]
    [SerializeField, Min(0.1f)] private float catchDistance = 1.0f;

    [Header("พุ่งตรงเมื่อเห็นกัน")]
    [Tooltip("ผู้เล่นอยู่ใกล้กว่านี้ และไม่มีกำแพงขวาง → พุ่งตรงเข้าหา ไม่อ้อมตามรอย")]
    [SerializeField, Min(0f)] private float directChaseRange = 4f;
    [Tooltip("Layer ที่นับเป็นกำแพง (บังสายตา)")]
    [SerializeField] private LayerMask wallMask = ~0;

    [Header("รอยเท้า")]
    [Tooltip("จดตำแหน่งผู้เล่นทุกๆ กี่เมตร")]
    [SerializeField, Min(0.1f)] private float footstepSpacing = 0.5f;

    [Header("จุดปลอดภัย (ห้องครัว)")]
    [Tooltip("กล่องชนของห้องครัว (เช่น SafeZone_Area) — ผู้เล่นเข้ากล่องนี้ = บอสหาย")]
    [SerializeField] private Collider kitchenZone;
    [Tooltip("เวลาจางหายตอนผู้เล่นเข้าครัว (วินาที)")]
    [SerializeField, Min(0f)] private float vanishSeconds = 1f;

    [Header("โดนจับแล้วเริ่มใหม่")]
    [Tooltip("ผู้เล่นกลับมาเกิดตรงนี้ (จุดเริ่มไล่ล่า)")]
    [SerializeField] private Transform playerRestartPoint;
    [Tooltip("บอสกลับมาเกิดตรงนี้ (เว้นว่าง = ตำแหน่งตอนเริ่มไล่ครั้งแรก)")]
    [SerializeField] private Transform stalkerRestartPoint;
    [Tooltip("จอดำ (CanvasGroup ภาพดำเต็มจอ alpha 0) — เว้นว่างได้ จะวาร์ปทันทีไม่มีจอดำ")]
    [SerializeField] private CanvasGroup blackScreen;
    [Tooltip("เวลาจอดำค้างก่อนเริ่มไล่ใหม่ (วินาที)")]
    [SerializeField, Min(0f)] private float restartDelay = 1.2f;
    [Tooltip("เวลาค่อยๆ มืด / ค่อยๆ สว่าง (วินาที)")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;

    [Header("เริ่มไล่อัตโนมัติ")]
    [Tooltip("คัทซีนเล่นจบเมื่อไหร่ เริ่มไล่เลย (ลาก Cutscene_FirstPTSD มาใส่) — เว้นว่าง = ต้องเรียก BeginChase() เอง")]
    [SerializeField] private UnityEngine.Playables.PlayableDirector startAfterCutscene;
    [Tooltip("ผีในคัทซีน (Cutscene_FirstPTSD/Stalker) — คัทซีนจบแล้วบอสจะโผล่ตรงที่ผีตัวนั้นหายไปพอดี ต่อกันเนียน\nเว้นว่าง = เริ่มจากตำแหน่งที่วางบอสไว้ในซีน")]
    [SerializeField] private Transform handOffFrom;

    [Header("บังคับเส้นทาง (เปิดเฉพาะตอนไล่ · หนีรอดแล้วคืนค่าเดิม)")]
    [Tooltip("ของที่โผล่เฉพาะตอนไล่ เช่น มอนยืนขวางเส้นแดง (วางในซีนแบบปิดไว้)")]
    [SerializeField] private GameObject[] showDuringChase;
    [Tooltip("สคริปต์ที่ปิดไว้ตอนไล่ เช่น DoorController ของประตูครัวล่าง = กด E เปิดไม่ได้ (ล็อก)")]
    [SerializeField] private Behaviour[] disableDuringChase;
    [Tooltip("ประตูที่เปิดรอไว้ตอนเริ่มไล่ เช่น ประตูครัวบน — ผู้เล่นไม่ต้องหยุดกด E กลางทาง")]
    [SerializeField] private DoorController[] openDoorsOnStart;
    [Tooltip("ของใน Show During Chase โผล่ตอนคัทซีนเล่นถึงวินาทีนี้ — ตั้งให้ตรงช่วงกล้องหันไปจับบอส (ผู้เล่นไม่เห็นตอนโผล่ กล้องกลับมาก็เจอเต็มทาง)")]
    [SerializeField, Min(0f)] private float showBlockersAtTime = 4f;
    [Tooltip("พื้นที่เริ่มวิ่ง (กล่องที่เดินชนแล้วคัทซีนเริ่ม เช่น Trigger_Cinematic_PTSD)\nตอนของกั้นโผล่ ถ้าผู้เล่นทับกำแพง/อยู่นอกพื้นที่นี้ → ย้ายไป Player Restart Point (ตอนกล้องไม่ได้มองอยู่)")]
    [SerializeField] private Collider chaseStartArea;

    [Header("อ้างอิง (เว้นว่าง = หาเอง)")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer sprite;

    [Header("หลังหนีรอด")]
    [Tooltip("ฉากที่เล่นตอนออกจากโลก PTSD (หลังหนีเข้าครัวสำเร็จแล้ว) เช่น ตั้งธง + บทพูด 'ตั้งสติ Rowan...'")]
    [SerializeField] private StorySequence playOnPtsdExit;

    [Header("Events")]
    public UnityEvent onCaught;
    [Tooltip("ผู้เล่นหนีเข้าครัวสำเร็จ — เช่น ตั้งธง / เล่นบทพูด")]
    public UnityEvent onEscaped;

    public bool IsChasing { get; private set; }

    // ===== คุมจังหวะแบบคัทซีน (เช่น FallingDebris ขอให้บอสจี้หลังติดๆ ตอนผู้เล่นวิ่งลอดของที่กำลังจะถล่ม) =====
    private float pressureGap = -1f;
    private float pressureSpeed;

    /// <summary>บังคับให้บอสไล่มาห่างผู้เล่นไม่เกิน gap เมตร (เร่งด้วย sprintSpeed) จนกว่าจะเรียก ReleasePressure</summary>
    public void ApplyPressure(float gap, float sprintSpeed)
    {
        pressureGap = Mathf.Max(catchDistance + 0.5f, gap);
        pressureSpeed = sprintSpeed;
    }

    public void ReleasePressure() => pressureGap = -1f;

    private readonly Queue<Vector3> footsteps = new Queue<Vector3>();
    private Vector3 lastFootstep;
    private Vector3 spawnPosition;
    private bool hasSpawnPosition;
    private Rigidbody playerBody;
    private PlayerMovement playerMovement;
    private Color spriteColor = Color.white;

    // ตัว object เปิดทิ้งไว้ตลอด แต่ซ่อนภาพไว้จนกว่าจะเริ่มไล่ (ปิด object ไม่ได้ เพราะต้องคอยฟังคัทซีนจบ)
    private void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody>();
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        // จำขอบพื้นที่เริ่มวิ่งไว้ตอนนี้ (กล่องจะถูกปิดหลังเดินชน → อ่านขอบไม่ได้แล้ว)
        if (chaseStartArea != null && chaseStartArea.enabled)
        {
            startAreaBounds = chaseStartArea.bounds;
            hasStartArea = true;
        }
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (sprite != null)
        {
            spriteColor = sprite.color;
            sprite.enabled = false;
        }
    }

    private void OnEnable()
    {
        PTSDManager.OnPTSDStateChanged += HandlePtsdChanged;
        if (startAfterCutscene != null)
        {
            startAfterCutscene.stopped += HandleCutsceneStopped;
            startAfterCutscene.played += HandleCutscenePlayed;
        }
    }

    // คัทซีนเริ่ม → ล็อกประตูทันที · ส่วนผี/กำแพงกั้นรอโผล่ตอนกล้องหันไปจับบอส (showBlockersAtTime)
    private void HandleCutscenePlayed(UnityEngine.Playables.PlayableDirector d)
    {
        if (chaseFinished) return;
        SetRouteLocks(true, showBlockers: false);
        StartCoroutine(ShowBlockersMidCutscene(d));
    }

    private IEnumerator ShowBlockersMidCutscene(UnityEngine.Playables.PlayableDirector d)
    {
        // รอจนคัทซีนเล่นถึงเวลา (คัทซีนถูกหยุด/จบก่อน → graph หาย → ออกจากลูป แล้ว BeginChase จะโผล่ให้อยู่ดี)
        while (d != null && d.playableGraph.IsValid() && d.time < showBlockersAtTime)
            yield return null;
        ShowBlockers();
    }

    private bool blockersShown;
    private Bounds startAreaBounds;
    private bool hasStartArea;

    private void ShowBlockers()
    {
        if (blockersShown) return;
        blockersShown = true;
        if (showDuringChase != null)
            foreach (var go in showDuringChase) if (go != null) go.SetActive(true);
        EnsurePlayerOnStartSide();
    }

    // กำแพงกั้นโผล่ทับตัวผู้เล่น (ตัวหลุดทะลุไปอีกฝั่ง) หรือผู้เล่นเดินเลยพื้นที่เริ่มไปแล้ว → ย้ายกลับจุดเริ่มวิ่ง
    private void EnsurePlayerOnStartSide()
    {
        if (player == null || playerRestartPoint == null) return;

        bool misplaced = false;
        if (hasStartArea)
        {
            Vector3 p = player.position;
            misplaced = p.x < startAreaBounds.min.x || p.x > startAreaBounds.max.x || p.z < startAreaBounds.min.z || p.z > startAreaBounds.max.z;
        }

        Collider playerCol = player.GetComponent<Collider>();
        if (!misplaced && playerCol != null && showDuringChase != null)
        {
            foreach (var go in showDuringChase)
            {
                if (go == null) continue;
                foreach (var c in go.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger || !c.enabled) continue;
                    if (Physics.ComputePenetration(playerCol, playerCol.transform.position, playerCol.transform.rotation,
                                                   c, c.transform.position, c.transform.rotation, out _, out _))
                    { misplaced = true; break; }
                }
                if (misplaced) break;
            }
        }

        if (misplaced) WarpPlayer(playerRestartPoint.position);
    }

    private bool chaseFinished;

    private void OnDisable()
    {
        PTSDManager.OnPTSDStateChanged -= HandlePtsdChanged;
        if (startAfterCutscene != null)
        {
            startAfterCutscene.stopped -= HandleCutsceneStopped;
            startAfterCutscene.played -= HandleCutscenePlayed;
        }
    }

    // คัทซีนจบ → เริ่มไล่ เฉพาะตอนอยู่ในโลก PTSD แบบเอาตัวรอด (กันคัทซีนถูกสั่งหยุด/รีเซ็ตตอนอยู่โลกปกติ)
    private void HandleCutsceneStopped(UnityEngine.Playables.PlayableDirector _)
    {
        if (IsChasing) return;
        if (PTSDManager.Instance == null || PTSDManager.Instance.currentMode != PTSDMode.Survival_TypeA) return;

        // รับไม้ต่อจากผีคัทซีน: โผล่ตรงที่มันหายไป (ตำแหน่งนี้ใช้เป็นจุดเกิดตอนโดนจับด้วย ถ้าไม่ได้ใส่ Stalker Restart Point)
        if (handOffFrom != null)
        {
            Vector3 h = handOffFrom.position;
            transform.position = new Vector3(h.x, transform.position.y, h.z);
            spawnPosition = transform.position;
            hasSpawnPosition = true;
        }
        BeginChase();
    }

    // ออกจากโลก PTSD ด้วยทางอื่น (เช่น มินิเกมหายใจสำเร็จ) → เลิกไล่ ซ่อนตัว
    private void HandlePtsdChanged(bool inPtsd)
    {
        if (inPtsd) return;
        if (IsChasing) { StopAndHide(); return; }

        // หนีเข้าครัวได้แล้ว → ตอนนี้ออกจาก PTSD สำเร็จ (มินิเกมหายใจ) → เล่นฉากต่อ ครั้งเดียว
        if (escapedAwaitingExit)
        {
            escapedAwaitingExit = false;
            if (playOnPtsdExit != null && StoryDirector.Instance != null) StoryDirector.Instance.Play(playOnPtsdExit);
        }
    }

    private bool escapedAwaitingExit;

    /// <summary>เริ่มไล่ — เรียกจาก Signal ท้ายคัทซีน หรือ UnityEvent อื่น</summary>
    public void BeginChase()
    {
        if (player == null)
        {
            Debug.LogError("[FirstChaseStalker] หาผู้เล่นไม่เจอ (Tag Player)", this);
            return;
        }

        StopAllCoroutines();
        if (!hasSpawnPosition) { spawnPosition = transform.position; hasSpawnPosition = true; }
        if (sprite != null) { sprite.color = spriteColor; sprite.enabled = true; }

        footsteps.Clear();
        lastFootstep = Flat(player.position);
        footsteps.Enqueue(lastFootstep);
        pressureGap = -1f;
        IsChasing = true;
        SetRouteLocks(true);
    }

    // เปิด/ปิดตัวบังคับเส้นทาง
    private void SetRouteLocks(bool chasing, bool showBlockers = true)
    {
        if (chasing && showBlockers) ShowBlockers();
        if (!chasing)
        {
            blockersShown = false;
            if (showDuringChase != null)
                foreach (var go in showDuringChase) if (go != null) go.SetActive(false);
        }

        if (disableDuringChase != null)
            foreach (var b in disableDuringChase) if (b != null) b.enabled = !chasing;

        // DoorController (ของปอ) ไม่มีคำสั่งเปิดแบบ public → ใช้ SendMessage เรียก ToggleDoor ที่มีอยู่ ไม่ต้องแก้ไฟล์ปอ
        if (chasing && openDoorsOnStart != null)
            foreach (var d in openDoorsOnStart)
                if (d != null && !d.isOpen) { d.isUnlocked = true; d.SendMessage("ToggleDoor", SendMessageOptions.DontRequireReceiver); }
    }

    private void Update()
    {
        if (!IsChasing || player == null) return;

        Vector3 me = Flat(transform.position);
        Vector3 target = Flat(player.position);

        // ถึงครัวแล้ว → รอด
        if (kitchenZone != null && IsInside(kitchenZone, player.position))
        {
            StartCoroutine(VanishRoutine());
            return;
        }

        // จับได้
        if (Vector3.Distance(me, target) <= catchDistance)
        {
            StartCoroutine(CaughtRoutine());
            return;
        }

        RecordFootstep(target);

        float distToPlayer = Vector3.Distance(me, target);
        float speed = distToPlayer > catchUpDistance ? catchUpSpeed : chaseSpeed;
        if (pressureGap > 0f) speed = distToPlayer > pressureGap ? pressureSpeed : chaseSpeed; // ช่วงฉากบังคับจังหวะ: จี้หลังติดๆ
        float step = speed * Time.deltaTime;

        Vector3 goal;
        if (distToPlayer <= directChaseRange && CanSee(target))
        {
            // เห็นกันตรงๆ → พุ่งเข้าหา · รอยเท้าที่อยู่ไกลกว่าตัวผู้เล่นทิ้งไป เหลือแค่ตำแหน่งล่าสุด
            goal = target;
            footsteps.Clear();
            footsteps.Enqueue(lastFootstep);
        }
        else
        {
            // ตามรอยทีละจุด · ถึงจุดแล้วตัดทิ้ง
            while (footsteps.Count > 1 && Vector3.Distance(me, footsteps.Peek()) <= step + 0.05f)
                footsteps.Dequeue();
            goal = footsteps.Count > 0 ? footsteps.Peek() : target;
        }

        Vector3 next = Vector3.MoveTowards(me, goal, step);
        transform.position = new Vector3(next.x, transform.position.y, next.z);
        FaceTowards(goal - me);
    }

    private void RecordFootstep(Vector3 playerFlat)
    {
        if (Vector3.Distance(playerFlat, lastFootstep) < footstepSpacing) return;
        lastFootstep = playerFlat;
        footsteps.Enqueue(playerFlat);
    }

    private bool CanSee(Vector3 targetFlat)
    {
        Vector3 from = Flat(transform.position) + Vector3.up * 1f;
        Vector3 to = targetFlat + Vector3.up * 1f;
        Vector3 dir = to - from;
        if (!Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude, wallMask, QueryTriggerInteraction.Ignore))
            return true;
        return hit.transform == player || hit.transform.IsChildOf(player);
    }

    // หันซ้าย/ขวาตามทิศวิ่ง (สไปรท์ 2D — พลิกภาพ ไม่หมุนตัว เพราะมี Billboard คุมการหันหากล้องอยู่)
    private void FaceTowards(Vector3 dir)
    {
        if (sprite == null || dir.sqrMagnitude < 0.0001f || Camera.main == null) return;
        float side = Vector3.Dot(dir, Camera.main.transform.right);
        if (Mathf.Abs(side) > 0.01f) sprite.flipX = side < 0f;
    }

    private IEnumerator CaughtRoutine()
    {
        IsChasing = false;
        onCaught?.Invoke();

        SetPlayerControl(false);
        yield return FadeBlack(1f);

        // วาร์ปผู้เล่น + บอส กลับจุดเริ่ม
        if (playerRestartPoint != null) WarpPlayer(playerRestartPoint.position);
        Vector3 back = stalkerRestartPoint != null ? stalkerRestartPoint.position : spawnPosition;
        transform.position = new Vector3(back.x, transform.position.y, back.z);

        yield return new WaitForSeconds(restartDelay);
        yield return FadeBlack(0f);
        SetPlayerControl(true);

        BeginChase();
    }

    private IEnumerator VanishRoutine()
    {
        IsChasing = false;
        escapedAwaitingExit = true;
        chaseFinished = true;
        onEscaped?.Invoke();

        if (sprite != null && vanishSeconds > 0f)
        {
            Color c = spriteColor;
            for (float t = 0f; t < vanishSeconds; t += Time.deltaTime)
            {
                c.a = spriteColor.a * (1f - t / vanishSeconds);
                sprite.color = c;
                yield return null;
            }
        }
        StopAndHide();
    }

    private void StopAndHide()
    {
        IsChasing = false;
        StopAllCoroutines();
        SetRouteLocks(false);
        if (blackScreen != null) blackScreen.alpha = 0f;
        SetPlayerControl(true);
        if (sprite != null) { sprite.color = spriteColor; sprite.enabled = false; }
        // กลับไปรอที่จุดเกิด (เผื่อเข้า PTSD ครั้งแรกซ้ำหลังโหลดเซฟ)
        if (hasSpawnPosition) transform.position = new Vector3(spawnPosition.x, transform.position.y, spawnPosition.z);
    }

    private IEnumerator FadeBlack(float targetAlpha)
    {
        if (blackScreen == null) yield break;
        float start = blackScreen.alpha;
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            blackScreen.alpha = Mathf.Lerp(start, targetAlpha, t / fadeSeconds);
            yield return null;
        }
        blackScreen.alpha = targetAlpha;
    }

    private void SetPlayerControl(bool on)
    {
        if (playerMovement != null) playerMovement.enabled = on;
        if (!on && playerBody != null) playerBody.linearVelocity = Vector3.zero;
    }

    private void WarpPlayer(Vector3 pos)
    {
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.position = pos;
        }
        player.position = pos;
    }

    private static bool IsInside(Collider zone, Vector3 point)
    {
        return (zone.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, directChaseRange);
        if (footsteps.Count > 0)
        {
            Gizmos.color = Color.magenta;
            foreach (var p in footsteps) Gizmos.DrawSphere(p + Vector3.up * 0.2f, 0.12f);
        }
    }
}
