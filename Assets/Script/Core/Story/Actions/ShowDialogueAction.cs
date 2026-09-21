using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Action สำหรับแสดงบทสนทนา
/// เชื่อมต่อกับ IDialogueService ส่วนกลาง เพื่อแสดงผลผ่าน WorldSpace หรือ ScreenBox Presenter
/// รองรับทั้งการลาก Asset DialogueData (หลายบรรทัด/มีเสียง/มีรูป) หรือระบุ textKey เดี่ยวๆ
/// </summary>
[Serializable]
public class ShowDialogueAction : IStoryAction
{
    [Tooltip("บทสนทนาแบบเต็ม (DialogueData) — หากใส่ช่องนี้ จะเล่นบทนี้เป็นหลัก")]
    public DialogueData dialogueData;

    [Tooltip("คีย์ข้อความใน LocalizationData.csv (ใช้กรณีต้องการขึ้นประโยคสั้นๆ บรรทัดเดียว)")]
    public string textKey;

    [Tooltip("ระยะเวลาแสดงข้อความค้างไว้ (วินาที) กรณีใช้ textKey เดี่ยว")]
    public float duration = 2.5f;

    public IEnumerator Execute(StoryContext ctx)
    {
        IDialogueService dialogueService = ServiceLocator.Get<IDialogueService>();

        if (dialogueService != null)
        {
            if (dialogueData != null)
            {
                yield return dialogueService.Play(dialogueData);
            }
            else if (!string.IsNullOrEmpty(textKey))
            {
                yield return dialogueService.PlaySimple(textKey, duration);
            }
        }
        else
        {
            // Fallback เผื่อยังไม่ได้แปะ DialogueService ในซีน
            ILocalizationService loc = ServiceLocator.Get<ILocalizationService>();
            string text = loc != null ? loc.GetText(textKey) : textKey;
            Debug.Log($"<color=yellow>💬 [Dialogue Fallback] {text}</color>");

            if (duration > 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
        }
    }
}
