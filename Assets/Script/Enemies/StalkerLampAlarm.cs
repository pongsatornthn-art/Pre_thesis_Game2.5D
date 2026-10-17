using UnityEngine;

/// <summary>
/// ตัวต่อระหว่างสตอล์กเกอร์ ↔ ตัวรบกวนไฟ — บอก LampDisturber ว่าตอนนี้บอส "โกรธ" อยู่ไหม (ไล่ / กรีดร้อง / จับ)
/// แปะคู่กับ StalkerAI + LampDisturber ในพรีแฟบ Stalker · ไม่แก้โค้ด StalkerAI (อ่านสถานะอย่างเดียว)
/// </summary>
[RequireComponent(typeof(StalkerAI), typeof(LampDisturber))]
public class StalkerLampAlarm : MonoBehaviour
{
    private StalkerAI stalker;
    private LampDisturber disturber;

    private void Awake()
    {
        stalker = GetComponent<StalkerAI>();
        disturber = GetComponent<LampDisturber>();
    }

    private void Update()
    {
        IStalkerState s = stalker.CurrentState;
        disturber.Agitated = s is StalkerStateChase || s is StalkerStateScream || s is StalkerStateKill;
    }
}
