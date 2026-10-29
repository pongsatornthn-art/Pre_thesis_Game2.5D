using UnityEngine;
using System.Collections.Generic;

public class RoomWallFade : MonoBehaviour
{
    [Header("ใส่ชิ้นส่วนกำแพงทั้งหมดที่อยากให้ปิดตา (ซ่อนไปเลย)")]
    public List<MeshRenderer> wallsToHide;

    private void OnTriggerEnter(Collider other)
    {
        // เมื่อ Player เดินชน Trigger
        if (other.CompareTag("Player"))
        {
            foreach (MeshRenderer wall in wallsToHide)
            {
                if (wall != null)
                {
                    // ปิดการแสดงผล (เหมือนกดปิดตา/ติ๊กถูกออก)
                    wall.enabled = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // เมื่อ Player เดินออกจาก Trigger
        if (other.CompareTag("Player"))
        {
            foreach (MeshRenderer wall in wallsToHide)
            {
                if (wall != null)
                {
                    // เปิดการแสดงผลกลับมาเหมือนเดิม
                    wall.enabled = true;
                }
            }
        }
    }
}