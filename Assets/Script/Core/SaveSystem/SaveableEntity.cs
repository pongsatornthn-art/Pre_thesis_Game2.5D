using UnityEngine;
using System;

/// <summary>
/// ตัวสร้าง "รหัสบัตรประชาชน (UUID)" ให้กับวัตถุต่างๆ
/// เอาไปแปะที่ GameObject ไหน มันจะสุ่มรหัสที่ไม่ซ้ำกันให้ตอนอยู่ใน Editor อัตโนมัติ
/// </summary>
[ExecuteAlways]
public class SaveableEntity : MonoBehaviour
{
    [SerializeField, Tooltip("ห้ามแก้รหัสนี้เด็ดขาด ระบบจะสุ่มให้เอง")] 
    private string uuid = "";

    public string UUID => uuid;

    private void Awake()
    {
        // ทำงานเฉพาะตอนจัดฉากใน Editor (ไม่ทำงานตอนเล่นเกม เพื่อไม่ให้รหัสเปลี่ยน)
        if (Application.isPlaying) return;
        GenerateUUID();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        GenerateUUID();
    }

    [ContextMenu("บังคับสร้าง UUID ใหม่ (ถ้าพัง)")]
    private void GenerateUUID()
    {
        if (string.IsNullOrEmpty(uuid))
        {
            uuid = Guid.NewGuid().ToString();
            Debug.Log($"[SaveableEntity] ออกรหัสประจำตัวให้ {gameObject.name} : {uuid}");
        }
    }
}
