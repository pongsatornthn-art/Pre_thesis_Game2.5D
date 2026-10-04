using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบบริการบทสนทนาส่วนกลาง (Dialogue Service)
/// ควบคุมการเล่นบทพูดทีละบรรทัด มีระบบคิวเพื่อป้องกันไม่ให้บทใหม่พูดทับบทเดิม
/// และเลือก Presenter ด้วยชื่อผ่าน DialoguePresenterRegistry ตามหลัก Open-Closed (OCP)
/// → ไฟล์นี้ไม่รู้จักคลาส Presenter ตัวไหนเลย เพิ่มสไตล์ใหม่ไม่ต้องแก้ไฟล์นี้
/// </summary>
public class DialogueService : MonoBehaviour, IDialogueService
{
    [Header("Presenters")]
    [Tooltip("ใช้เมื่อบทพูดไม่ได้ระบุ Presenter Id หรือหาชื่อที่ระบุไม่เจอ")]
    [SerializeField] private string defaultPresenterId = DialoguePresenterIds.World;

    private readonly Queue<DialogueData> dialogueQueue = new Queue<DialogueData>();
    private Coroutine queueRoutine;
    private IDialoguePresenter activePresenter;

    public bool IsPlaying => queueRoutine != null;

    private void Awake()
    {
        ServiceLocator.Register<IDialogueService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IDialogueService>();
    }

    public IEnumerator Play(DialogueData data)
    {
        if (data == null) yield break;

        dialogueQueue.Enqueue(data);

        // หากกำลังเล่นบทอื่นอยู่ ให้รอจนกว่าคิวของบทนี้จะได้รับการประมวลผลจนเสร็จ
        if (queueRoutine == null)
        {
            queueRoutine = StartCoroutine(ProcessQueueRoutine());
        }

        while (dialogueQueue.Contains(data) || activePresenter != null)
        {
            yield return null;
        }
    }

    public IEnumerator PlaySimple(string textKey, float duration)
    {
        if (string.IsNullOrEmpty(textKey)) yield break;

        DialogueData simpleData = ScriptableObject.CreateInstance<DialogueData>();
        simpleData.presenterId = defaultPresenterId;
        simpleData.lines = new[]
        {
            new DialogueLine
            {
                textKey = textKey,
                holdSeconds = duration
            }
        };

        yield return StartCoroutine(Play(simpleData));

        // asset ชั่วคราวที่สร้างตอนรัน ต้องทำลายเอง ไม่งั้นค้างในหน่วยความจำทุกครั้งที่เรียก
        Destroy(simpleData);
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (dialogueQueue.Count > 0)
        {
            DialogueData currentData = dialogueQueue.Dequeue();
            if (currentData != null && currentData.lines != null)
            {
                // ดึง Presenter ผ่าน Interface IDialoguePresenter
                activePresenter = GetPresenter(currentData.presenterId);

                for (int i = 0; i < currentData.lines.Length; i++)
                {
                    DialogueLine line = currentData.lines[i];
                    if (line == null) continue;

                    // เล่นเสียงพากย์ผ่านบริการกลาง IAudioService หากมีระบุไว้
                    if (line.voiceClip != null)
                    {
                        IAudioService audio = ServiceLocator.GetOptional<IAudioService>();
                        audio?.PlaySFX(line.voiceClip, transform.position, 1f, false);
                    }

                    if (activePresenter != null)
                    {
                        yield return StartCoroutine(activePresenter.Show(line));
                    }
                }

                if (activePresenter != null)
                {
                    activePresenter.HideImmediate();
                    activePresenter = null;
                }
            }
        }

        queueRoutine = null;
    }

    private IDialoguePresenter GetPresenter(string presenterId)
    {
        string id = string.IsNullOrEmpty(presenterId) ? defaultPresenterId : presenterId;
        IDialoguePresenter presenter = DialoguePresenterRegistry.Get(id);

        if (presenter == null && id != defaultPresenterId)
        {
            Debug.LogWarning($"[DialogueService] ไม่พบ Presenter ชื่อ '{id}' ในซีน — ใช้ '{defaultPresenterId}' แทน");
            presenter = DialoguePresenterRegistry.Get(defaultPresenterId);
        }

        if (presenter == null)
        {
            Debug.LogWarning($"[DialogueService] ไม่พบ Presenter ชื่อ '{id}' ในซีน — บทพูดนี้จะไม่แสดง");
        }
        return presenter;
    }

    public void StopCurrent()
    {
        if (activePresenter != null)
        {
            activePresenter.HideImmediate();
            activePresenter = null;
        }

        if (queueRoutine != null)
        {
            StopCoroutine(queueRoutine);
            queueRoutine = null;
        }

        dialogueQueue.Clear();
    }
}
