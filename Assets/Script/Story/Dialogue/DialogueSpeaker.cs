using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ป้ายชื่อ "ตัวละครที่พูดได้" — แปะที่ตัวละครในซีน (ผู้เล่น / NPC / ผี) แล้วตั้ง speakerId
/// บทพูดที่ใส่ speakerId ตรงกัน ข้อความลอย (WorldSpace) จะไปขึ้นข้างตัวนี้
///
/// อยู่ฝั่งซีน ไม่ใช่ใน asset — เพราะ asset ชี้เข้าซีนไม่ได้ (ดูกฎ 12.8 ใน ARCHITECTURE_MAP)
/// บทพูด (asset) จึงอ้างแค่ "ชื่อ" แล้วมาหาตัวจริงที่นี่ตอนเล่น
/// </summary>
public class DialogueSpeaker : MonoBehaviour
{
    [Tooltip("ชื่อเรียกตัวละครนี้ เช่น player / npc_mom — ต้องตรงกับช่อง Speaker Id ในบทพูด")]
    [SerializeField] private string speakerId = "player";

    [Tooltip("จุดที่ข้อความจะลอยอยู่ (เว้นว่าง = ใช้ตำแหน่งตัวนี้ + Offset)")]
    [SerializeField] private Transform anchor;

    [Tooltip("ระยะเลื่อนจากจุดบน — ปกติยกขึ้นเหนือหัว")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

    private static readonly Dictionary<string, DialogueSpeaker> speakers = new Dictionary<string, DialogueSpeaker>();

    public string SpeakerId => speakerId;
    public Vector3 BubblePosition => (anchor != null ? anchor.position : transform.position) + offset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => speakers.Clear();

    // ใช้ OnEnable/OnDisable — ตัวละครที่อยู่ในโลกที่ถูกซ่อน (PTSD) จะหายจากรายชื่อเอง
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(speakerId)) return;

        if (speakers.TryGetValue(speakerId, out DialogueSpeaker existing) && existing != null && existing != this)
        {
            Debug.LogWarning($"[DialogueSpeaker] speakerId '{speakerId}' ซ้ำกันระหว่าง {existing.name} กับ {name}", this);
        }
        speakers[speakerId] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(speakerId)) return;
        if (speakers.TryGetValue(speakerId, out DialogueSpeaker current) && current == this)
        {
            speakers.Remove(speakerId);
        }
    }

    /// <summary>หาตัวละครจากชื่อ — ไม่เจอคืน null</summary>
    public static DialogueSpeaker Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return speakers.TryGetValue(id, out DialogueSpeaker s) && s != null ? s : null;
    }
}
