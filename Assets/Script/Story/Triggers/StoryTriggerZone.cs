using UnityEngine;

/// <summary>
/// พื้นที่ตรวจจับ (Trigger Zone) สำหรับเริ่มเล่นฉากเนื้อเรื่องเมื่อผู้เล่นเดินผ่าน
/// รองรับการตรวจสอบเงื่อนไขธงเนื้อเรื่อง และป้องกันการเล่นซ้ำ
/// 
/// ข้อกำหนด: ต้องมี Collider ที่เปิดใช้งาน 'Is Trigger' ไว้เสมอ
/// </summary>
[RequireComponent(typeof(Collider))]
public class StoryTriggerZone : MonoBehaviour
{
    [Header("ฉากเนื้อเรื่อง")]
    [Tooltip("ลำดับเหตุการณ์ที่จะเล่นเมื่อผู้เล่นเหยียบทริกเกอร์")]
    [SerializeField] private StorySequence sequence;

    [Header("เงื่อนไขธง (Story Flags)")]
    [Tooltip("ต้องมีธงนี้ก่อนจึงจะทำงาน (เว้นว่าง = ไม่ตรวจเช็ค)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("ถ้ามีธงนี้แล้ว 'ห้ามทำงาน' (ใช้สำหรับป้องกันไม่ให้ฉากเดิมเล่นซ้ำ)")]
    [SerializeField] private StoryFlagId blockedByFlag;

    [Header("เงื่อนไขขั้นสูง (Advanced Conditions)")]
    [Tooltip("เงื่อนไขขั้นสูงเพิ่มเติม (เช่น HasItem, QuestActive, And, Not) — เว้นว่างได้")]
    [SerializeReference, SubclassPicker] private IStoryCondition customCondition;

    // [Claude 2026-11-12] เพลย์เทส: เดินผ่านกล่องเฉยๆ บทพูดก็ขึ้น ไม่สมจริง → ต้องหันไปมองสิ่งนั้นก่อน
    [Header("ต้องหันไปมอง (เว้นว่าง = เดินเข้ากล่องแล้วขึ้นเลยแบบเดิม)")]
    [Tooltip("สิ่งที่ผู้เล่นต้องหันไปมอง เช่น ประตูห้องเอกสาร — ตัวละครหันตามเมาส์")]
    [SerializeField] private Transform lookTarget;
    [Tooltip("หันเบี่ยงจากเป้าหมายได้ไม่เกินกี่องศา")]
    [SerializeField, Range(5f, 90f)] private float lookAngle = 35f;
    [Tooltip("ต้องมองค้างไว้กี่วินาที (กันแค่กวาดเมาส์ผ่าน)")]
    [SerializeField, Min(0f)] private float lookSeconds = 0.35f;

    [Header("การทำงาน")]
    [Tooltip("เล่นเพียงครั้งเดียวในรอบการเล่นนี้หรือไม่")]
    [SerializeField] private bool onlyOnce = true;

    private bool hasTriggered = false;
    private GameObject playerInside;
    private Transform playerAim;
    private float lookTimer;

    private void Reset()
    {
        // ช่วยตั้งค่า isTrigger อัตโนมัติเมื่อแปะคอมโพเนนต์
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ตรวจสอบเฉพาะ GameObject ที่มีแท็ก Player เท่านั้น
        if (!other.CompareTag("Player")) return;

        if (lookTarget != null)
        {
            // ต้องหันไปมองก่อน → รอเช็คใน Update ระหว่างอยู่ในกล่อง
            playerInside = other.gameObject;
            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            playerAim = pm != null && pm.aimAnchor != null ? pm.aimAnchor : other.transform;
            lookTimer = 0f;
            return;
        }

        TryPlay(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject == playerInside) playerInside = null;
    }

    private void Update()
    {
        if (playerInside == null || lookTarget == null) return;
        if (onlyOnce && hasTriggered) { playerInside = null; return; }
        if (Time.timeScale == 0f) return;

        Vector3 facing = playerAim.forward; facing.y = 0f;
        Vector3 toTarget = lookTarget.position - playerInside.transform.position; toTarget.y = 0f;
        bool looking = facing.sqrMagnitude > 0.001f && toTarget.sqrMagnitude > 0.001f && Vector3.Angle(facing, toTarget) <= lookAngle;

        lookTimer = looking ? lookTimer + Time.deltaTime : 0f;
        if (lookTimer >= lookSeconds)
        {
            lookTimer = 0f;
            bool before = hasTriggered;
            hasTriggered = false;
            TryPlay(playerInside);
            if (hasTriggered) playerInside = null;   // เล่นแล้ว — ต้องออกแล้วเข้ากล่องใหม่ถึงขึ้นอีก (กรณีเล่นซ้ำได้)
            else hasTriggered = before;
        }
    }

    private void TryPlay(GameObject player)
    {
        if (onlyOnce && hasTriggered) return;
        if (sequence == null) return;

        IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();

        // ตรวจสอบเงื่อนไขว่ามีธงที่จำเป็นครบหรือไม่
        if (requiredFlag != null && (flags == null || !flags.Has(requiredFlag)))
        {
            return;
        }

        // ตรวจสอบว่าติดธงที่บล็อกไม่ให้เล่นหรือไม่ (เช่น เคยเล่นไปแล้ว)
        if (blockedByFlag != null && flags != null && flags.Has(blockedByFlag))
        {
            return;
        }

        // ตรวจสอบเงื่อนไขขั้นสูงเพิ่มเติม (ถ้ามีกำหนดไว้)
        if (customCondition != null)
        {
            StoryContext ctx = new StoryContext
            {
                Player = player,
                Flags = flags,
                Quests = ServiceLocator.GetOptional<IQuestService>(),
                Runner = this
            };
            if (!customCondition.IsMet(ctx)) return;
        }

        StoryDirector director = StoryDirector.Instance ?? ServiceLocator.GetOptional<StoryDirector>();
        if (director == null)
        {
            director = FindFirstObjectByType<StoryDirector>();
        }

        if (director != null)
        {
            hasTriggered = true;
            director.Play(sequence);
        }
        else
        {
            Debug.LogError("[StoryTriggerZone] ไม่พบ StoryDirector ในซีน!");
        }
    }
}
