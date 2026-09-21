using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวกำกับลำดับเหตุการณ์เนื้อเรื่อง (Story Director)
/// ทำหน้าที่รัน Action ทีละตัวอย่างเป็นระเบียบ มีระบบคิวเพื่อป้องกันไม่ให้ฉากเล่นทับซ้อนกัน
/// และควบคุมการล็อกการเดินของผู้เล่น (isStunned) ตามการตั้งค่าของแต่ละฉาก
/// 
/// ออกแบบตามหลัก Liskov Substitution Principle (LSP):
/// StoryDirector รัน IStoryAction ทุกตัวอย่างเท่าเทียมกันโดยไม่มี if-else เช็คชนิดของ Action
/// </summary>
public class StoryDirector : MonoBehaviour
{
    public static StoryDirector Instance { get; private set; }

    private readonly Queue<StorySequence> sequenceQueue = new Queue<StorySequence>();
    private Coroutine queueRoutine;

    // จำไว้ว่ากำลังล็อกผู้เล่นคนไหนอยู่ เพื่อปลดล็อกให้ได้แม้ coroutine ถูกหยุดกลางคัน
    // (Unity ไม่รัน finally ถ้า coroutine โดน StopCoroutine หรือ GameObject ถูกปิด/ทำลาย)
    private PlayerMovement lockedMovement;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ServiceLocator.Register<StoryDirector>(this);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        // ตาข่ายนิรภัย: ถ้าฉากถูกตัดจบกลางทาง ห้ามปล่อยผู้เล่นค้างอยู่ในสภาพขยับไม่ได้
        ReleasePlayerLock();
        sequenceQueue.Clear();
        IsPlaying = false;
        queueRoutine = null;
    }

    private void ReleasePlayerLock()
    {
        if (lockedMovement != null) lockedMovement.isStunned = false;
        lockedMovement = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<StoryDirector>();
            Instance = null;
        }
    }

    /// <summary>สั่งเล่นลำดับเหตุการณ์ (หากมีฉากอื่นเล่นอยู่ จะเข้าคิวต่อท้ายอัตโนมัติ)</summary>
    public void Play(StorySequence seq)
    {
        if (seq == null) return;

        sequenceQueue.Enqueue(seq);

        if (!IsPlaying)
        {
            queueRoutine = StartCoroutine(ProcessQueueRoutine());
        }
    }

    private IEnumerator ProcessQueueRoutine()
    {
        IsPlaying = true;

        while (sequenceQueue.Count > 0)
        {
            StorySequence currentSeq = sequenceQueue.Dequeue();
            if (currentSeq != null)
            {
                yield return StartCoroutine(PlaySequenceRoutine(currentSeq));
            }
        }

        IsPlaying = false;
        queueRoutine = null;
    }

    private IEnumerator PlaySequenceRoutine(StorySequence seq)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;

        // ล็อกผู้เล่นไม่ให้เดินตามข้อกำหนด 12.1 โดยไม่แตะต้องโค้ดของเพื่อน
        if (seq.lockPlayer && movement != null)
        {
            movement.isStunned = true;
            lockedMovement = movement;
        }

        StoryContext ctx = new StoryContext
        {
            Player = player,
            Flags = ServiceLocator.Get<IStoryFlags>(),
            Quests = ServiceLocator.Get<IQuestService>(),
            Runner = this
        };

        try
        {
            if (seq.actions != null)
            {
                for (int i = 0; i < seq.actions.Count; i++)
                {
                    IStoryAction action = seq.actions[i];
                    if (action != null)
                    {
                        yield return action.Execute(ctx);
                    }
                }
            }
        }
        finally
        {
            // ปลดล็อกผู้เล่นเสมอเมื่อฉากเล่นจบหรือเกิดข้อผิดพลาด
            if (seq.lockPlayer) ReleasePlayerLock();
        }
    }
}
