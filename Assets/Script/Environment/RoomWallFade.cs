using UnityEngine;
using System.Collections.Generic;

public class RoomWallFade : MonoBehaviour
{
    [Header("ใส่ชิ้นส่วนกำแพงทั้งหมดที่อยากให้โปร่งใส")]
    public List<MeshRenderer> wallsToFade;

    [Header("ตั้งค่าความโปร่งใส")]
    [Tooltip("ค่าความจางตอนเดินเข้าใกล้ (0 = ล่องหน, 1 = ทึบแสง) แนะนำ 0.2 - 0.3")]
    public float fadeAlpha = 0.2f;

    // เก็บค่าสีดั้งเดิมของกำแพงแต่ละชิ้นไว้ (เพื่อดึงกลับมาตอนเดินออก)
    private Dictionary<MeshRenderer, Color> originalColors = new Dictionary<MeshRenderer, Color>();

    private void Start()
    {
        // เริ่มเกมมา ให้จำสีดั้งเดิมของกำแพงทุกชิ้นไว้ก่อน
        foreach (MeshRenderer wall in wallsToFade)
        {
            if (wall != null && wall.material.HasProperty("_Color"))
            {
                originalColors[wall] = wall.material.color;

                // สำคัญ: บังคับให้ Material ของกำแพงรองรับความโปร่งใส (Transparent)
                SetMaterialTransparent(wall.material);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ปรับสีให้จางลง (เปลี่ยนค่า Alpha)
            foreach (MeshRenderer wall in wallsToFade)
            {
                if (wall != null && originalColors.ContainsKey(wall))
                {
                    Color fadedColor = originalColors[wall];
                    fadedColor.a = fadeAlpha;
                    wall.material.color = fadedColor;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // ดึงสีเดิมกลับมา (ทึบแสงเหมือนเดิม)
            foreach (MeshRenderer wall in wallsToFade)
            {
                if (wall != null && originalColors.ContainsKey(wall))
                {
                    wall.material.color = originalColors[wall];
                }
            }
        }
    }

    // ฟังก์ชันย่อยสำหรับบังคับ Material ให้รองรับโหมด Transparent อัตโนมัติ (สำหรับ Standard Shader)
    private void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3); // 3 คือโหมด Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}