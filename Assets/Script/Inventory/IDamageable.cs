using UnityEngine;

namespace Assets.Script.Inventory
{

    // สังเกตว่าเปลี่ยนจาก class เป็น interface
    public interface IDamageable
    {
        // บังคับว่าใครที่มีป้ายนี้ ต้องมีฟังก์ชันโดนตี (รับค่าดาเมจ และแรงกระเด็น)
        void TakeDamage(int damage, float knockback);
    }
}