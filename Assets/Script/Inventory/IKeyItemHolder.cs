using System;
using System.Collections.Generic;

/// <summary>
/// คลังเก็บ "ของสำคัญ" (Key Item) แยกจากกระเป๋าปกติ — มาตรฐานเกม horror (RE / Silent Hill)
/// ของในคลังนี้ไม่กินช่องกระเป๋า ทิ้งไม่ได้ และเก็บได้ไม่จำกัด
/// </summary>
public interface IKeyItemHolder
{
    /// <summary>ยิงทุกครั้งที่คลังเปลี่ยน (เก็บเพิ่ม/ใช้ไป) — ให้ UI มาเกาะเพื่อวาดใหม่</summary>
    event Action OnChanged;

    /// <summary>รายการของสำคัญทั้งหมด (อ่านอย่างเดียว กันคนอื่นมาแก้ List ตรงๆ)</summary>
    IReadOnlyList<KeyItemData> All { get; }

    /// <summary>เก็บของสำคัญเข้าคลัง (ถ้ามีอยู่แล้วจะไม่ซ้ำ)</summary>
    void Add(KeyItemData key);

    /// <summary>มีกุญแจที่เปิดประตูรหัสนี้ไหม (ไม่ใช้ของ แค่ถาม)</summary>
    bool Has(string doorId);

    /// <summary>ใช้กุญแจเปิดประตูรหัสนี้ — ถ้ากุญแจตั้ง consumeOnUse ไว้จะหักออกจากคลังให้เลย</summary>
    /// <returns>true = มีกุญแจและเปิดได้</returns>
    bool Consume(string doorId);
}
