using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomFogFadeController : MonoBehaviour
{
    [Header("ใส่แผ่นหมอกของห้องนี้ (ต้องมี MeshRenderer)")]
    public List<MeshRenderer> fogPlanes;

    [Header("ตั้งค่าหมอก (Fog Settings)")]
    [Tooltip("ความเร็วในการเฟดหายไปและโผล่มา")]
    public float fadeSpeed = 2f;

    [Tooltip("ความทึบแสงสูงสุด (1 = ดำสนิท, 0.95 = เห็นลางๆ ให้ความรู้สึกเป็นหมอกมากกว่า)")]
    public float maxAlpha = 0.95f;

    [Tooltip("สีของหมอก (แนะนำเทาเข้ม หรือ น้ำเงินเข้มๆ จะดูดีกว่าดำสนิท)")]
    public Color fogColor = new Color(0.1f, 0.1f, 0.12f, 1f);

    [Header("Shader Graph Reference")]
    [Tooltip("ใส่ชื่อ Reference ของตัวแปรสีใน Shader Graph (เช่น _FogColor)")]
    public string shaderColorRef = "_FogColor"; // 🌟 จุดสำคัญ! แก้ชื่อนี้ให้ตรงกับใน Shader Graph

    private Coroutine fadeCoroutine;
    private float currentAlpha; // เก็บค่า Alpha ไว้ในสคริปต์แทนการดึงจาก Material

    private void Start()
    {
        currentAlpha = maxAlpha; // เริ่มต้นให้หมอกทึบที่สุด

        foreach (MeshRenderer fog in fogPlanes)
        {
            if (fog != null)
            {
                // ไม่ต้องใช้ฟังก์ชัน SetMaterialTransparent แล้ว เพราะเราตั้ง Transparent ใน Shader Graph แล้ว
                UpdateFogColor(fog.material, currentAlpha);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            // สั่งเฟดให้หมอกจางหายไปจนเหลือ 0 (มองเห็นห้อง)
            fadeCoroutine = StartCoroutine(FadeFog(0f));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            // สั่งเฟดให้หมอกกลับมาทึบแสงเท่า maxAlpha (มองไม่เห็นห้อง)
            fadeCoroutine = StartCoroutine(FadeFog(maxAlpha));
        }
    }

    private IEnumerator FadeFog(float targetAlpha)
    {
        if (fogPlanes.Count == 0 || fogPlanes[0] == null) yield break;

        // ค่อยๆ เปลี่ยนค่า Alpha ไปเรื่อยๆ จนกว่าจะถึงเป้าหมาย
        while (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            foreach (MeshRenderer fog in fogPlanes)
            {
                if (fog != null)
                {
                    UpdateFogColor(fog.material, currentAlpha);
                }
            }
            yield return null; // รอเฟรมถัดไป
        }

        // ชัวร์ว่าค่าสุดท้ายตรงเป๊ะ
        currentAlpha = targetAlpha;
        foreach (MeshRenderer fog in fogPlanes)
        {
            if (fog != null)
            {
                UpdateFogColor(fog.material, currentAlpha);
            }
        }
    }

    // 🌟 ฟังก์ชันใหม่สำหรับเปลี่ยนสีใน Shader Graph
    private void UpdateFogColor(Material mat, float alpha)
    {
        Color c = fogColor;
        c.a = alpha;

        // เช็คว่ามี Property ชื่อนี้ใน Shader จริงไหม ก่อนสั่งเปลี่ยนค่า
        if (mat.HasProperty(shaderColorRef))
        {
            mat.SetColor(shaderColorRef, c);
        }
        else
        {
            Debug.LogWarning("หาตัวแปรสี: " + shaderColorRef + " ใน Shader ไม่เจอครับ! เช็คชื่อ Reference ใน Shader Graph ด่วน!");
        }
    }
}