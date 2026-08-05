using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    // ให้หมุด Event ในแอนิเมชัน เรียกฟังก์ชันนี้แทน
    public void TriggerAttackDamage()
    {
        // มันจะส่งคำสั่งทะลุขึ้นไปหาตัว Player แม่ ให้ทำดาเมจ
        GetComponentInParent<PlayerCombat>().PerformStrikeDamage();
    }
}