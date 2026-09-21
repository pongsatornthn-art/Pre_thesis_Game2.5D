using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ลำดับเหตุการณ์เนื้อเรื่อง (Cutscene / Sequence) 1 ฉาก
/// จัดการรายการคำสั่งย่อยที่จะถูกรันเรียงตามลำดับโดย StoryDirector
/// 
/// ข้อควรระวัง: ต้องใช้ [SerializeReference] เท่านั้นในการเก็บ List<IStoryAction>
/// หากใช้ [SerializeField] ทั่วไป Unity จะไม่สามารถบันทึก Polymorphic Interfaces ลง Inspector ได้
/// </summary>
[CreateAssetMenu(fileName = "Sequence_", menuName = "Story/Sequence", order = 4)]
public class StorySequence : ScriptableObject
{
    [Tooltip("ล็อกไม่ให้ผู้เล่นเดินตลอดช่วงที่ฉากนี้เล่น")]
    public bool lockPlayer;

    [SerializeReference]
    public List<IStoryAction> actions = new List<IStoryAction>();
}
