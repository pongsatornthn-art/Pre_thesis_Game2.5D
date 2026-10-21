using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ผู้ควบคุมระบบ Safe Zone ทั้งหมดในซีน (1 ตัวต่อซีน)
/// จัดการสุ่มห้อง จัดการมินิเกมล้มเหลว คูลดาวน์ และแจ้งเตือนสตอล์กเกอร์
/// </summary>
public class SafeZoneDirector : MonoBehaviour
{
    [Header("ระบบมินิเกม (ของปอ)")]
    [Tooltip("ตัวควบคุมมินิเกม PTSD")]
    [SerializeField] private PTSDMinigameController minigame;

    [Tooltip("ตัวควบคุมหลอดชาร์จมินิเกม PTSD")]
    [SerializeField] private PTSDMinigameCharger charger;

    [Header("การตั้งค่า Safe Zone")]
    [Tooltip("รายชื่อห้อง Safe Zone ในซีน (ถ้าเว้นว่างจะค้นหาจากลูกในตัวเองอัตโนมัติ)")]
    [SerializeField] private SafeZone[] zones;

    [Tooltip("เวลานับถอยหลังคูลดาวน์เมื่อเล่นมินิเกมพลาด (วินาที)")]
    [SerializeField, Min(1f)] private float cooldownSeconds = 30f;

    [Tooltip("ระยะสุ่มห้อง Safe Zone ใหม่ขั้นต่ำ (เมตร แนวราบ)")]
    [SerializeField, Min(1f)] private float relocateMinDistance = 10f;

    [Tooltip("ระยะสุ่มห้อง Safe Zone ใหม่สูงสุด (เมตร แนวราบ)")]
    [SerializeField, Min(1f)] private float relocateMaxDistance = 25f;

    [Tooltip("ทำงานเฉพาะในโหมด WorldMode.PtsdSurvival หรือไม่")]
    [SerializeField] private bool onlyInSurvival = true;

    /// <summary>ห้องที่ปลอดภัยล่าสุด</summary>
    public SafeZone CurrentSafeZone { get; private set; }

    /// <summary>ห้องที่ผู้เล่นกำลังยืนอยู่ข้างใน (null ถ้าอยู่นอกห้อง)</summary>
    public SafeZone ZonePlayerIsIn { get; private set; }

    /// <summary>ระบบกำลังทำงานอยู่หรือไม่</summary>
    public bool IsRunning { get; private set; }

    private bool wasPlayerInSafeZone = false;
    private float panicExhaustionTimer = 0f;
    private IWorldModeService worldService;

    private void Awake()
    {
        if (minigame == null)
            Debug.LogWarning($"[{gameObject.name}] ยังไม่ได้ลาก PTSDMinigameController ใส่ — ระบบ Safe Zone จะไม่ทำงาน", this);

        if (charger == null)
            Debug.LogWarning($"[{gameObject.name}] ยังไม่ได้ลาก PTSDMinigameCharger ใส่ — ระบบ Safe Zone จะไม่ทำงาน", this);

        if (zones == null || zones.Length == 0)
            zones = GetComponentsInChildren<SafeZone>(true);

        if (zones.Length == 0)
            Debug.LogWarning($"[{gameObject.name}] ไม่พบคอมโพเนนต์ SafeZone ในระบบ!", this);
        else if (zones.Length < 2)
            Debug.LogWarning($"[{gameObject.name}] มี SafeZone เพียง {zones.Length} ห้อง — จะไม่สามารถสุ่มย้ายห้องได้เมื่อพลาดมินิเกม", this);
    }

    private void OnEnable()
    {
        HookWorldService();

        if (minigame != null)
            minigame.OnMinigameFailed.AddListener(HandleMinigameFailed);
    }

    // บริการโลกอาจลงทะเบียนช้ากว่า OnEnable ของเรา (ลำดับ Awake ข้ามออบเจกต์ไม่แน่นอน) → เรียกซ้ำใน Start ได้
    private void HookWorldService()
    {
        if (worldService != null) return;
        worldService = ServiceLocator.GetOptional<IWorldModeService>();
        if (worldService != null)
            worldService.OnModeChanged += HandleWorldModeChanged;
    }

    private void OnDisable()
    {
        if (worldService != null)
            worldService.OnModeChanged -= HandleWorldModeChanged;
        worldService = null;

        if (minigame != null)
            minigame.OnMinigameFailed.RemoveListener(HandleMinigameFailed);

        StopDirector();
    }

    private void Start()
    {
        HookWorldService();
        if (worldService != null)
            EvaluateMode(worldService.Mode);
        else
            Debug.LogWarning($"[{gameObject.name}] หา IWorldModeService ไม่เจอ — ระบบ Safe Zone จะไม่รู้ว่าเข้า PTSD (มี [CORE_SERVICES] ในซีนไหม?)", this);
    }

    private void HandleWorldModeChanged(WorldMode prev, WorldMode current) => EvaluateMode(current);

    private void EvaluateMode(WorldMode mode)
    {
        bool shouldRun = onlyInSurvival ? (mode == WorldMode.PtsdSurvival) : (mode != WorldMode.Real);
        if (shouldRun && !IsRunning)
            StartDirector();
        else if (!shouldRun && IsRunning)
            StopDirector();
    }

    private void StartDirector()
    {
        if (minigame == null || charger == null || zones == null || zones.Length == 0) return;

        IsRunning = true;
        wasPlayerInSafeZone = false;
        panicExhaustionTimer = 0f;

        for (int i = 0; i < zones.Length; i++)
            zones[i].ResetZone();

        int pick = Random.Range(0, zones.Length);
        CurrentSafeZone = zones[pick];
        CurrentSafeZone.SetState(SafeZoneState.Safe);
    }

    private void StopDirector()
    {
        IsRunning = false;
        wasPlayerInSafeZone = false;

        if (panicExhaustionTimer > 0f)
        {
            panicExhaustionTimer = 0f;
            GameEventBus.Publish(new PanicExhaustionChangedEvent(false, 0f));
        }

        if (zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
                zones[i].ResetZone();
        }

        CurrentSafeZone = null;
        ZonePlayerIsIn = null;

        if (minigame != null)
        {
            minigame.canStartMinigame = false;
            minigame.OnPlayerExitedSafeZone?.Invoke();
        }
        if (charger != null)
            charger.isPlayerInSafeZone = false;
    }

    private void Update()
    {
        if (!IsRunning) return;

        // ติดตามการนับเวลา Panic Exhaustion
        if (panicExhaustionTimer > 0f)
        {
            panicExhaustionTimer -= Time.deltaTime;
            if (panicExhaustionTimer <= 0f)
            {
                panicExhaustionTimer = 0f;
                GameEventBus.Publish(new PanicExhaustionChangedEvent(false, 0f));
            }
        }

        // ค้นหาห้องที่ผู้เล่นกำลังยืนอยู่
        ZonePlayerIsIn = null;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].IsPlayerInside)
            {
                ZonePlayerIsIn = zones[i];
                break;
            }
        }

        bool canPlay = ZonePlayerIsIn != null && ZonePlayerIsIn.CanPlayMinigame;

        // ห้ามตัดสถานะมินิเกมกลางคันขณะที่ผู้เล่นกำลังกดเล่นอยู่
        if (minigame.isMinigameActive) return;

        if (canPlay)
        {
            minigame.canStartMinigame = true;
            charger.isPlayerInSafeZone = true;
            if (!wasPlayerInSafeZone)
            {
                wasPlayerInSafeZone = true;
                minigame.OnPlayerEnteredSafeZone?.Invoke();
            }
        }
        else
        {
            minigame.canStartMinigame = false;
            charger.isPlayerInSafeZone = false;
            if (wasPlayerInSafeZone)
            {
                wasPlayerInSafeZone = false;
                minigame.OnPlayerExitedSafeZone?.Invoke();
            }
        }
    }

    private void HandleMinigameFailed()
    {
        if (!IsRunning || zones == null || zones.Length == 0) return;

        SafeZone oldZone = ZonePlayerIsIn != null ? ZonePlayerIsIn : CurrentSafeZone;
        if (oldZone == null) oldZone = zones[0];

        oldZone.SetState(SafeZoneState.Unsafe);
        oldZone.StartCooldown(cooldownSeconds);

        // ส่งเสียงเตือนสตอล์กเกอร์ให้เดินมายังตำแหน่งผู้เล่น
        StalkerAI stalker = Object.FindFirstObjectByType<StalkerAI>();
        Vector3 playerPos = stalker != null && stalker.PlayerTransform != null
            ? stalker.PlayerTransform.position
            : oldZone.AnchorPosition;

        if (stalker != null)
            stalker.ChangeState(new StalkerStateAlert(playerPos, 100f));

        // สุ่มห้องใหม่
        Vector3 stalkerPos = stalker != null ? stalker.transform.position : playerPos;
        SafeZone newZone = PickRelocatedZone(oldZone, stalkerPos);

        if (newZone != null)
        {
            newZone.SetState(SafeZoneState.Safe);
            CurrentSafeZone = newZone;
        }

        panicExhaustionTimer = cooldownSeconds;
        GameEventBus.Publish(new SafeZoneRelocatedEvent(oldZone, newZone, cooldownSeconds));
        GameEventBus.Publish(new PanicExhaustionChangedEvent(true, cooldownSeconds));
    }

    private SafeZone PickRelocatedZone(SafeZone fromZone, Vector3 stalkerPos)
    {
        List<SafeZone> dormantZones = new List<SafeZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != fromZone && zones[i].State == SafeZoneState.Dormant)
                dormantZones.Add(zones[i]);
        }

        // กรณีมีห้อง Dormant: สุ่มห้องในระยะที่กำหนด
        if (dormantZones.Count > 0)
        {
            List<SafeZone> inRangeZones = new List<SafeZone>();
            for (int i = 0; i < dormantZones.Count; i++)
            {
                float d = HorizontalDistance(fromZone.AnchorPosition, dormantZones[i].AnchorPosition);
                if (d >= relocateMinDistance && d <= relocateMaxDistance)
                    inRangeZones.Add(dormantZones[i]);
            }

            if (inRangeZones.Count > 0)
                return inRangeZones[Random.Range(0, inRangeZones.Count)];

            // ถ้าไม่มีห้องในระยะ: เลือกห้อง Dormant ที่ใกล้ช่วงระยะที่สุด
            SafeZone closest = dormantZones[0];
            float minDelta = RangeDelta(HorizontalDistance(fromZone.AnchorPosition, closest.AnchorPosition));
            for (int i = 1; i < dormantZones.Count; i++)
            {
                float delta = RangeDelta(HorizontalDistance(fromZone.AnchorPosition, dormantZones[i].AnchorPosition));
                if (delta < minDelta)
                {
                    minDelta = delta;
                    closest = dormantZones[i];
                }
            }
            return closest;
        }

        // กรณีไม่มี Dormant เหลือเลย: เลือกห้อง Unsafe ที่ไกลสตอล์กเกอร์ที่สุดเพื่อกันตาย
        SafeZone furthest = null;
        float maxDist = -1f;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == fromZone) continue;
            float d = HorizontalDistance(zones[i].AnchorPosition, stalkerPos);
            if (d > maxDist)
            {
                maxDist = d;
                furthest = zones[i];
            }
        }
        return furthest != null ? furthest : fromZone;
    }

    private float RangeDelta(float dist)
    {
        if (dist < relocateMinDistance) return relocateMinDistance - dist;
        if (dist > relocateMaxDistance) return dist - relocateMaxDistance;
        return 0f;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
