using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// เล่นคัทซีนที่วางไว้ในซีน (Timeline = StoryCutscene · ภาพทีละหน้า = ComicCutscene) แล้วรอจนจบ
/// ไม่สนว่าเป็นแบบไหน — คุยผ่าน CutsceneBase เท่านั้น
/// </summary>
[Serializable, PickerName("คัทซีน/เล่นคัทซีน")]
public class PlayCutsceneAction : IStoryAction
{
    [Tooltip("ต้องตรงกับ Scene Id ของคัทซีนในซีน")]
    public string cutsceneId = "cutscene_01";

    [Tooltip("✔ = รอคัทซีนจบก่อนทำคำสั่งถัดไป · ✘ = เล่นแล้วไปต่อเลย")]
    public bool waitUntilEnd = true;

    [Tooltip("กันค้าง: รอนานสุดกี่วินาที (ภาพทีละหน้าที่รอผู้เล่นกด ควรตั้งเผื่อเยอะ)")]
    [Min(1f)] public float maxWaitSeconds = 300f;

    public IEnumerator Execute(StoryContext ctx)
    {
        CutsceneBase cutscene = CutsceneBase.Find(cutsceneId);
        if (cutscene == null)
        {
            Debug.LogWarning($"[PlayCutsceneAction] ไม่พบคัทซีน '{cutsceneId}' ในซีน — ลืมแปะ StoryCutscene/ComicCutscene หรือ Scene Id ไม่ตรง");
            yield break;
        }

        cutscene.Play();
        if (!waitUntilEnd) yield break;

        // ข้ามเฟรมแรกก่อน — Timeline บางทียังไม่เป็น Playing ในเฟรมที่สั่งเล่น
        yield return null;

        float waited = 0f;
        while (cutscene != null && cutscene.IsPlaying && waited < maxWaitSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (cutscene != null && cutscene.IsPlaying)
        {
            Debug.LogWarning($"[PlayCutsceneAction] คัทซีน '{cutsceneId}' นานเกิน {maxWaitSeconds} วิ — ข้ามให้");
            cutscene.Skip();
        }
    }
}
