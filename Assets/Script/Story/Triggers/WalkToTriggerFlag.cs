using UnityEngine;

public class WalkToTriggerFlag : MonoBehaviour
{
    [Tooltip("ลากไฟล์ Flag Id ที่ต้องการให้สำเร็จเมื่อผู้เล่นเดินมาชนมาใส่ช่องนี้")]
    public StoryFlagId flagToSet;

    private void OnTriggerEnter(Collider other)
    {
        // เช็คว่าคนที่มาเดินชนคือ Player ใช่ไหม
        if (other.CompareTag("Player") && flagToSet != null)
        {
            // เรียกใช้ระบบความจำของเกมที่เพื่อนเขียนไว้
            IStoryFlags flags = ServiceLocator.GetOptional<IStoryFlags>();
            if (flags != null)
            {
                flags.Set(flagToSet); // สั่งตั้งธง!
                Debug.Log("เหยียบจุดทริกเกอร์แล้ว! ตั้งธง: " + flagToSet.flagId);
            }
            else
            {
                Debug.LogWarning("ไม่พบระบบ IStoryFlags ในฉาก (ลืมลากสคริปต์ StoryFlagService ลงฉากหรือเปล่า?)");
            }

            // เหยียบเสร็จก็ทำลายจุดนี้ทิ้งเลย จะได้ไม่ทำงานซ้ำสอง
            Destroy(gameObject);
        }
    }
}