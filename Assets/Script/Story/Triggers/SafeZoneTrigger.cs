using UnityEngine;

public class SafeZoneTrigger : MonoBehaviour
{
    [Header("อ้างอิงถึงระบบมินิเกมหลอดวิ่ง")]
    public PTSDMinigameController minigameController;

    [Header("อ้างอิงถึงระบบหลอดชาร์จกดค้าง")]
    public PTSDMinigameCharger minigameCharger;

    [Header("การจัดการตัวผี (Stalker)")]
    [Tooltip("ลากออบเจกต์ Stalker มาใส่ที่นี่ เพื่อให้ผีหายตัวไปทันทีเมื่อเข้า SafeZone")]
    public GameObject stalkerObject; // 🌟 เพิ่มตัวแปรอ้างอิงผี

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (minigameController != null)
            {
                minigameController.canStartMinigame = true;
                minigameController.OnPlayerEnteredSafeZone?.Invoke();
            }

            if (minigameCharger != null)
            {
                minigameCharger.isPlayerInSafeZone = true;
            }

            // 🌟 เพิ่มคำสั่งปิดผี Stalker ตรงนี้ครับ!
            if (stalkerObject != null)
            {
                stalkerObject.SetActive(false);
                Debug.Log("เข้าสู่ Safe Zone: ปิดการทำงานของผี Stalker เรียบร้อย!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (minigameController != null)
            {
                minigameController.canStartMinigame = false;
                minigameController.OnPlayerExitedSafeZone?.Invoke();
            }

            if (minigameCharger != null)
            {
                minigameCharger.isPlayerInSafeZone = false;
            }
        }
    }
}