using System;
using UnityEngine;

public class PTSDManager : MonoBehaviour
{
    // ตัวแปร Instance เผื่อใครอยากเรียกใช้ดื้อๆ (Singleton)
    public static PTSDManager Instance { get; private set; }

    // Event นี้โคตรสำคัญ ใครอยากรู้ว่าตอนนี้อยู่โลกไหนให้มาเกาะ (Subscribe) อันนี้ไว้
    // Manager จะทำหน้าที่แค่ "ประกาศวิทยุ" เท่านั้น ไม่ไปยุ่งเรื่องการเสกมอนสเตอร์ (Decoupled สุดๆ)
    public static event Action<bool> OnPTSDStateChanged;

    [Header("PTSD Settings")]
    public bool isPTSDActive = false;

    private void Awake()
    {
        // จัดการ Singleton ป้องกันมันเกิดซ้ำซ้อน
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // เอาไว้เทสระบบ กด L เพื่อสลับโลกไปมา
        if (Input.GetKeyDown(KeyCode.L))
        {
            TogglePTSDWorld(!isPTSDActive);
            Debug.Log(isPTSDActive ? "👻 เข้าโลก PTSD แล้ว (กด L)" : "🌍 กลับโลกปกติแล้ว (กด L)");
        }
    }

    /// <summary>
    /// สั่งเปิด/ปิดโลก PTSD จากข้างนอก (เดฟอีกคนเรียกใช้ฟังก์ชันนี้แหละ)
    /// </summary>
    public void TogglePTSDWorld(bool isActive)
    {
        isPTSDActive = isActive;
        
        // ตะโกนบอกทุกคนที่เกาะ Event นี้อยู่ (รวมถึง Spawner และ UI)
        OnPTSDStateChanged?.Invoke(isPTSDActive);
    }
}
