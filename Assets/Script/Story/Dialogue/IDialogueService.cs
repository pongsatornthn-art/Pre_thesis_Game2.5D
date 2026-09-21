using System.Collections;

/// <summary>
/// อินเตอร์เฟสสำหรับระบบจัดการบทสนทนาส่วนกลาง (Dialogue Service)
/// ให้บริการเล่นบทสนทนาแบบมีคิว และเชื่อมโยงกับการเล่นเสียงพากย์และเอฟเฟกต์
/// </summary>
public interface IDialogueService
{
    bool IsPlaying { get; }
    IEnumerator Play(DialogueData data);
    IEnumerator PlaySimple(string textKey, float duration);
    void StopCurrent();
}
