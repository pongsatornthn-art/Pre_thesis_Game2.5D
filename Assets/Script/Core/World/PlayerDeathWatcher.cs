using UnityEngine;

/// <summary>ผู้เล่นเลือดหมด — ประกาศครั้งเดียวต่อการตาย 1 ครั้ง</summary>
public readonly struct PlayerDiedEvent
{
    public readonly Vector3 Position;
    public PlayerDiedEvent(Vector3 position) { Position = position; }
}

/// <summary>
/// คอยดูเลือดผู้เล่น แล้วประกาศ PlayerDiedEvent
///
/// ทำไมต้องมี: PlayerMovement.Die() ของเพื่อนแค่พิมพ์ "Game Over" ลง Console ไม่มีอะไรเกิดขึ้นต่อ
/// และเป็น private — ตัวนี้อ่านแค่ CurrentHealth (public) จึง **ไม่ต้องแก้ไฟล์เพื่อน**
///
/// ใช้วิธีเช็คทุกเฟรมแทนการฟัง PlayerDamagedEvent เพราะดาเมจบางทาง (เช่น มินิเกมสตอล์กเกอร์) อาจไม่ผ่าน event
/// </summary>
public class PlayerDeathWatcher : MonoBehaviour
{
    private PlayerMovement player;
    private bool isDead;

    private void Update()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go == null || !go.TryGetComponent(out player)) return;
        }

        bool deadNow = player.CurrentHealth <= 0;

        if (deadNow && !isDead)
        {
            isDead = true;
            Debug.Log("<color=red>💀 [PlayerDeathWatcher] ผู้เล่นตาย</color>");
            GameEventBus.Publish(new PlayerDiedEvent(player.transform.position));
        }
        else if (!deadNow && isDead)
        {
            isDead = false;   // ฟื้นแล้ว (เช่น เริ่มโลก PTSD ใหม่) — พร้อมตรวจการตายครั้งถัดไป
        }
    }
}
