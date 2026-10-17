using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "ตัวรบกวนไฟ" — แปะที่ตัวอะไรก็ได้ (ตอนนี้ใช้กับสตอล์กเกอร์) → ไฟในระยะรอบตัวมันกระพริบรัว (โลก PTSD เท่านั้น)
/// ยิ่งใกล้ยิ่งถี่ · พอมันเดินผ่านไป ไฟกลับมานิ่งเอง → ผู้เล่นเห็นแนวไฟกระพริบเคลื่อนเข้ามา = บอสกำลังมา
/// ตัวสั่งไฟจริงคือ WorldLighting (อ่านรายชื่อตัวรบกวนจาก Active) — ตัวนี้แค่บอก "อยู่ตรงนี้ · รัศมีเท่านี้"
/// </summary>
public class LampDisturber : MonoBehaviour
{
    /// <summary>ตัวรบกวนที่ทำงานอยู่ทั้งหมดในฉาก</summary>
    public static readonly List<LampDisturber> Active = new List<LampDisturber>();

    [Tooltip("รัศมี (เมตร) ที่ไฟรอบตัวกระพริบ ตอนปกติ")]
    [SerializeField, Min(0f)] private float radius = 8f;

    [Tooltip("รัศมี (เมตร) ตอนโกรธ (ไล่ / กรีดร้อง) — ใหญ่กว่า = รู้ตัวว่ามาแล้วไกลขึ้น")]
    [SerializeField, Min(0f)] private float agitatedRadius = 14f;

    /// <summary>ตั้งจากภายนอก (เช่น StalkerLampAlarm) ว่าตอนนี้กำลังโกรธอยู่ไหม</summary>
    public bool Agitated { get; set; }

    public float Radius => Agitated ? agitatedRadius : radius;

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, agitatedRadius);
    }
#endif
}
