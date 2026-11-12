using UnityEngine;

/// <summary>
/// จุดเซฟในแมพ (แนวเครื่องพิมพ์ดีด Resident Evil) — เดินไปใกล้แล้วกด E
///
/// มีหน้าจอเลือกช่อง (ISaveSlotPicker ลงทะเบียนไว้) → เปิดหน้าจอให้ผู้เล่นเลือกช่อง
/// ยังไม่มี (⏳ รออาร์ต UI) → เซฟลงช่องเริ่มต้นของ SaveManager ให้เลย
/// ในโลก PTSD เซฟไม่ได้ (SaveManager ประกาศ SaveBlockedEvent → ป้ายแจ้งเตือนบอกผู้เล่น)
/// </summary>
public class SavePoint : StoryInteractableBase
{
    [Tooltip("ชื่อห้องที่โชว์ในช่องเซฟ (key แปลภาษา) เช่น LOC_STUDY")]
    [SerializeField] private string locationKey;

    [Tooltip("ตำแหน่งที่ผู้เล่นจะเกิดตอนโหลดเซฟนี้ (เว้นว่าง = ตรงจุดเซฟนี้)\nควรวางห่างจุดเซฟเล็กน้อย ไม่ให้เกิดซ้อนกับโต๊ะ/เครื่องพิมพ์ดีด")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("ฉากที่เล่นหลังเซฟสำเร็จ เช่น เสียงพิมพ์ดีด + บทพูดสั้นๆ (เว้นว่างได้)")]
    [SerializeField] private StorySequence onSaved;

    [Tooltip("ยังไม่มีหน้าจอเลือกช่อง → เซฟลงช่องนี้")]
    [SerializeField, Min(0)] private int fallbackSlot = 0;

    /// <summary>
    /// [งานข้อ 8 · Claude 2026-11-11] เซฟจากที่อื่นที่ไม่ใช่การเดินมากด F — เช่น คลิกสมุดบนโต๊ะในมุมมองบุคคลที่หนึ่ง
    /// ไม่ถ่ายรูปตัวอย่างตอนนี้ (จอเป็นภาพโต๊ะ) — โต๊ะถ่ายไว้ให้แล้วตอนกด E เข้า
    /// onDone: true = เซฟแล้ว · false = ยกเลิก/เซฟไม่ได้
    /// </summary>
    public void SaveFromDesk(System.Action<bool> onDone) => BeginSave(onDone, captureThumbnail: false);
    public void SaveFromDesk() => SaveFromDesk(null);

    protected override void OnInteract(StoryContext ctx) => BeginSave(null, captureThumbnail: true);

    private void BeginSave(System.Action<bool> onDone, bool captureThumbnail)
    {
        if (captureThumbnail) StartCoroutine(CaptureThenSave(onDone));
        else DoSave(onDone);
    }

    private System.Collections.IEnumerator CaptureThenSave(System.Action<bool> onDone)
    {
        yield return SaveThumbnail.Capture();   // ถ่ายฉากก่อนหน้าจอเลือกช่องขึ้น
        DoSave(onDone);
    }

    private void DoSave(System.Action<bool> onDone)
    {
        if (!ServiceLocator.TryGet(out SaveManager save))
        {
            Debug.LogError("[SavePoint] ไม่พบ SaveManager — [CORE_SERVICES] อยู่ในซีนหรือเปล่า", this);
            onDone?.Invoke(false);
            return;
        }

        Transform spawn = spawnPoint != null ? spawnPoint : transform;

        // เช็คก่อนเปิดหน้าจอ — ในโลก PTSD ไม่ต้องเปิดหน้าจอเลือกช่องให้เสียเวลา
        if (!save.CanSave(out string blockKey))
        {
            GameEventBus.Publish(new SaveBlockedEvent(SaveReason.Manual, blockKey));
            onDone?.Invoke(false);
            return;
        }

        if (ServiceLocator.TryGet(out ISaveSlotPicker picker))
        {
            picker.OpenSavePicker(new SaveRequest(spawn, locationKey, saved =>
            {
                if (saved) PlaySequence(onSaved);
                onDone?.Invoke(saved);
            }));
            return;
        }

        bool ok = save.TrySaveToSlot(fallbackSlot, SaveReason.Manual, spawn, locationKey);
        if (ok) PlaySequence(onSaved);
        onDone?.Invoke(ok);
    }
}
