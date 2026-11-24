using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// [2026-11-24 นา] "จุดสังเกต" ในปริศนา — เช่น รอยลากบนพื้นหน้าชั้นหนังสือ
/// อยู่ในมุมกล้องปริศนา (BookshelfPuzzle) · ชี้เมาส์ = เรืองแสง · คลิก = สังเกตแล้ว
/// BookshelfPuzzle ช่อง Required Clue: ยังไม่ได้คลิกจุดนี้ → ดึงหนังสือเล่มถูกก็ยังเปิดประตูลับไม่ได้ (หนังสือเลื่อนกลับ)
///
/// วิธีใช้: แปะที่โมเดลรอย (ต้องมี Collider) · ใส่ ItemHighlight แบบ Manual ไว้ให้เรืองตอนชี้ (เว้นว่างได้)
/// </summary>
public class PuzzleClue : MonoBehaviour
{
    [Tooltip("เรืองตอนชี้เมาส์ (ItemHighlight โหมด Manual) — เว้นว่างได้")]
    [SerializeField] private ItemHighlight highlight;
    [Tooltip("เสียงตอนคลิกสังเกต (เว้นว่างได้)")]
    [SerializeField] private AudioClip inspectSound;
    [Tooltip("ตอนคลิกสังเกตแล้ว (เช่น ขึ้นบทพูดในใจ)")]
    public UnityEvent onInspected;

    public bool Inspected { get; private set; }

    private bool hovered;

    private void Awake()
    {
        if (highlight == null) highlight = GetComponentInChildren<ItemHighlight>(true);
        if (GetComponentInChildren<Collider>(true) == null)
            Debug.LogWarning($"[PuzzleClue] '{name}' ไม่มี Collider — คลิกไม่ได้", this);
    }

    public void SetHovered(bool on)
    {
        if (hovered == on) return;
        hovered = on;
        if (highlight != null) highlight.SetHighlighted(on && !Inspected);
    }

    /// <summary>คลิกสังเกต — คืน true ถ้าเพิ่งสังเกตครั้งแรก</summary>
    public bool Inspect(AudioSource audio)
    {
        if (Inspected) return false;
        Inspected = true;
        if (highlight != null) highlight.SetHighlighted(false);
        if (audio != null && inspectSound != null) audio.PlayOneShot(inspectSound);
        onInspected?.Invoke();
        return true;
    }

    /// <summary>โหลดเซฟ: เคยสังเกตแล้ว</summary>
    public void SetInspectedInstant()
    {
        Inspected = true;
        if (highlight != null) highlight.SetHighlighted(false);
    }
}
