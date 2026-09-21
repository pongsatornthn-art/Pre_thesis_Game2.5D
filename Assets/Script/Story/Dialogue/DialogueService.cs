using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบบริการบทสนทนาส่วนกลาง (Dialogue Service)
/// ควบคุมการเล่นบทพูดทีละบรรทัด มีระบบคิวเพื่อป้องกันไม่ให้บทใหม่พูดทับบทเดิม
/// และเลือก Presenter ผ่าน Interface ตามหลัก Open-Closed (OCP)
/// </summary>
public class DialogueService : MonoBehaviour, IDialogueService
{
    [Header("Presenters")]
    [SerializeField] private WorldSpaceDialoguePresenter worldPresenter;
    [SerializeField] private ScreenBoxDialoguePresenter screenPresenter;

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
        simpleData.style = DialoguePresenterStyle.WorldSpace;
        simpleData.lines = new[]
        {
            new DialogueLine
            {
                textKey = textKey,
                holdSeconds = duration
            }
        };

        yield return StartCoroutine(Play(simpleData));
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (dialogueQueue.Count > 0)
        {
            DialogueData currentData = dialogueQueue.Dequeue();
            if (currentData != null && currentData.lines != null)
            {
                // ดึง Presenter ผ่าน Interface IDialoguePresenter
                activePresenter = GetPresenter(currentData.style);

                for (int i = 0; i < currentData.lines.Length; i++)
                {
                    DialogueLine line = currentData.lines[i];
                    if (line == null) continue;

                    // เล่นเสียงพากย์ผ่านบริการกลาง IAudioService หากมีระบุไว้
                    if (line.voiceClip != null)
                    {
                        IAudioService audio = ServiceLocator.Get<IAudioService>();
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

    private IDialoguePresenter GetPresenter(DialoguePresenterStyle style)
    {
        return style == DialoguePresenterStyle.WorldSpace
            ? (IDialoguePresenter)worldPresenter
            : screenPresenter;
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
