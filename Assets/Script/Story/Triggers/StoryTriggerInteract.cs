using UnityEngine;

/// <summary>
/// ทริกเกอร์สำหรับเริ่มฉากเนื้อเรื่องเมื่อผู้เล่นเดินเข้ามาใกล้แล้วกดปุ่มโต้ตอบ (เช่น กด E สำรวจประตู/ศพ/ป้าย)
/// </summary>
[RequireComponent(typeof(Collider))]
public class StoryTriggerInteract : MonoBehaviour
{
    [Header("ฉากเนื้อเรื่อง")]
    [Tooltip("ลำดับเหตุการณ์ที่จะเล่นเมื่อกดปุ่มโต้ตอบ")]
    [SerializeField] private StorySequence sequence;

    [Header("เงื่อนไขธง (Story Flags)")]
    [Tooltip("ต้องมีธงนี้ก่อนจึงจะสามารถกดโต้ตอบได้ (เว้นว่าง = ไม่ตรวจเช็ค)")]
    [SerializeField] private StoryFlagId requiredFlag;

    [Tooltip("ถ้ามีธงนี้แล้ว 'ห้ามทำงาน' (เช่น เคยสำรวจไปแล้ว)")]
    [SerializeField] private StoryFlagId blockedByFlag;

    [Header("เงื่อนไขขั้นสูง (Advanced Conditions)")]
    [Tooltip("เงื่อนไขขั้นสูงเพิ่มเติม (เช่น HasItem, QuestActive, And, Not) — เว้นว่างได้")]
    [SerializeReference] private IStoryCondition customCondition;

    [Header("การทำงาน")]
    [Tooltip("ปุ่มที่ใช้กดโต้ตอบ")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("เล่นเพียงครั้งเดียวหรือไม่")]
    [SerializeField] private bool onlyOnce = true;

    [Tooltip("UI แจ้งเตือนให้กดปุ่ม เช่น 'กด E เพื่อสำรวจ' (เว้นว่างได้)")]
    [SerializeField] private GameObject interactPrompt;

    private bool isPlayerNear = false;
    private bool hasTriggered = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            UpdatePromptVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            if (interactPrompt != null) interactPrompt.SetActive(false);
        }
    }

    private void UpdatePromptVisibility()
    {
        if (interactPrompt == null) return;
        if (onlyOnce && hasTriggered)
        {
            interactPrompt.SetActive(false);
            return;
        }

        IStoryFlags flags = ServiceLocator.Get<IStoryFlags>();
        if (requiredFlag != null && (flags == null || !flags.Has(requiredFlag)))
        {
            interactPrompt.SetActive(false);
            return;
        }
        if (blockedByFlag != null && flags != null && flags.Has(blockedByFlag))
        {
            interactPrompt.SetActive(false);
            return;
        }

        if (customCondition != null)
        {
            StoryContext ctx = new StoryContext
            {
                Player = GameObject.FindGameObjectWithTag("Player"),
                Flags = flags,
                Quests = ServiceLocator.Get<IQuestService>(),
                Runner = this
            };
            if (!customCondition.IsMet(ctx))
            {
                interactPrompt.SetActive(false);
                return;
            }
        }

        interactPrompt.SetActive(isPlayerNear);
    }

    private void TryInteract()
    {
        if (onlyOnce && hasTriggered) return;
        if (sequence == null) return;

        IStoryFlags flags = ServiceLocator.Get<IStoryFlags>();
        if (requiredFlag != null && (flags == null || !flags.Has(requiredFlag))) return;
        if (blockedByFlag != null && flags != null && flags.Has(blockedByFlag)) return;

        if (customCondition != null)
        {
            StoryContext ctx = new StoryContext
            {
                Player = GameObject.FindGameObjectWithTag("Player"),
                Flags = flags,
                Quests = ServiceLocator.Get<IQuestService>(),
                Runner = this
            };
            if (!customCondition.IsMet(ctx)) return;
        }

        StoryDirector director = StoryDirector.Instance ?? ServiceLocator.Get<StoryDirector>();
        if (director == null) director = FindFirstObjectByType<StoryDirector>();

        if (director != null)
        {
            hasTriggered = true;
            if (interactPrompt != null) interactPrompt.SetActive(false);
            director.Play(sequence);
        }
    }
}
