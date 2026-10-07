using UnityEngine;

/// <summary>
/// ช่อง 1 ช่องใน "คลังความทรงจำ" (หน้า L ของสมุด) — Create > Story > Memory Entry
/// เช่น เศษรูปวาดชิ้นที่ 3 / รูปวาดของภรรยา (เต็ม) / ความทรงจำจากเหตุการณ์
///
/// ยังไม่ปลด = รูปเดียวกันย้อมดำ + "???" · ปลดแล้ว = รูปจริง + ชื่อ + เรื่องย่อ · **ปลดแล้วปลดถาวร**
/// ปลดได้ 3 ทาง: เก็บไอเทม MemoryFragmentData ที่ชี้มาช่องนี้ · ฉากสั่ง "คลังความทรงจำ/ปลดช่อง" · ตั้ง "Unlock When" ด้านล่าง
/// อย่าลืมลากใส่ MemoryArchiveCatalog (ลำดับในหน้า = ลำดับใน Catalog)
/// </summary>
[CreateAssetMenu(fileName = "Memory_", menuName = "Story/Memory Entry", order = 6)]
public class MemoryEntryData : ScriptableObject
{
    [Tooltip("รหัสถาวร (ระบบเซฟใช้) — เว้นว่าง = ใช้ชื่อไฟล์ · กรอกแล้วห้ามแก้")]
    [SerializeField] private string entryId;

    [Header("เนื้อหา (key ใน LocalizationData.csv)")]
    public string titleKey;
    [Tooltip("เรื่องย่อ เช่น 'รูปวาดของภรรยาผู้จากไป...'")]
    public string descriptionKey;

    [Header("รูป")]
    [Tooltip("รูปจริง — ตอนยังไม่ปลดจะใช้รูปเดียวกันย้อมดำเป็นเงา (ไม่ต้องวาดเงาแยก)")]
    public Sprite image;

    [Header("ปลดเองอัตโนมัติ (ไม่บังคับ)")]
    [Tooltip("ปลดเมื่อเงื่อนไขครบ เช่น 'เควส/เควสนี้จบแล้ว' หรือ 'มีธง' — เว้นว่าง = ปลดจากการเก็บไอเทม/คำสั่งในฉากเท่านั้น")]
    [SerializeReference, SubclassPicker] public IStoryCondition unlockWhen;

    public string EntryId => string.IsNullOrEmpty(entryId) ? name : entryId;
}
