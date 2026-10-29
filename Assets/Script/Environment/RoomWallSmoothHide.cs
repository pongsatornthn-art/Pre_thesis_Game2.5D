using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomWallSmoothHide : MonoBehaviour
{
    [Header("ใส่ชิ้นส่วนกำแพงทั้งหมดที่อยากให้โปร่งใสและปิดตา")]
    public List<MeshRenderer> wallsToFade;

    [Header("ตั้งค่าการเฟด")]
    [Tooltip("ค่าความจางเป้าหมาย (แนะนำให้ตั้งเป็น 0 เพื่อให้เฟดจนมิดแล้วปิดตากำแพง)")]
    public float targetAlpha = 0f;
    [Tooltip("ความเร็วในการค่อยๆ เฟด")]
    public float fadeSpeed = 5f;

    // เก็บค่าสีดั้งเดิมของกำแพงแต่ละชิ้นไว้
    private Dictionary<MeshRenderer, Color> originalColors = new Dictionary<MeshRenderer, Color>();
    private Coroutine fadeCoroutine;

    private void Start()
    {
        // เริ่มเกมมา ให้จำสีดั้งเดิมของกำแพงทุกชิ้นไว้ก่อน
        foreach (MeshRenderer wall in wallsToFade)
        {
            if (wall != null && wall.material.HasProperty("_Color"))
            {
                originalColors[wall] = wall.material.color;

                // บังคับให้ Material ของกำแพงรองรับความโปร่งใส (Transparent)
                SetMaterialTransparent(wall.material);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // สั่งเริ่มเฟดกำแพงให้จางลงไปที่ค่า targetAlpha
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeWalls(targetAlpha));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // สั่งเริ่มเฟดกำแพงกลับมาให้ทึบแสง (Alpha = 1)
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeWalls(1f));
        }
    }

    private IEnumerator FadeWalls(float endAlpha)
    {
        if (wallsToFade.Count == 0 || wallsToFade[0] == null) yield break;

        // ถ้ายกเลิกการซ่อน (กำลังจะเฟดกลับมาทึบ) ต้องเปิดตา (Enable) กลับมาก่อน
        if (endAlpha > 0.01f)
        {
            foreach (MeshRenderer wall in wallsToFade)
            {
                if (wall != null) wall.enabled = true;
            }
        }

        float currentAlpha = wallsToFade[0].material.color.a;

        // ลูปค่อยๆ เปลี่ยนค่าความโปร่งใส
        while (Mathf.Abs(currentAlpha - endAlpha) > 0.01f)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, endAlpha, fadeSpeed * Time.deltaTime);

            foreach (MeshRenderer wall in wallsToFade)
            {
                if (wall != null && originalColors.ContainsKey(wall))
                {
                    Color fadedColor = originalColors[wall];
                    fadedColor.a = Mathf.Clamp(currentAlpha, 0f, originalColors[wall].a);
                    wall.material.color = fadedColor;
                }
            }
            yield return null;
        }

        // เมื่อจบการเฟด จัดการค่าสุดท้ายให้ตรงเป๊ะ
        foreach (MeshRenderer wall in wallsToFade)
        {
            if (wall != null && originalColors.ContainsKey(wall))
            {
                Color finalColor = originalColors[wall];
                finalColor.a = (endAlpha >= 0.99f) ? originalColors[wall].a : endAlpha;
                wall.material.color = finalColor;

                // ถ้าเฟดจางจนสุดแล้ว (Alpha ใกล้ 0) ให้ทำการ "ปิดตา" ซ่อนกำแพงทิ้งไปเลย
                if (endAlpha <= 0.01f)
                {
                    wall.enabled = false;
                }
            }
        }
    }

    // ฟังก์ชันย่อยสำหรับบังคับ Material ให้รองรับโหมด Transparent อัตโนมัติ
    private void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}