using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomFader : MonoBehaviour
{
    [Header("ใส่แผ่นเบลอของห้องนี้ (ต้องมี MeshRenderer)")]
    public List<MeshRenderer> blurPlanes;

    [Header("ตั้งค่าเบลอ (Blur Settings)")]
    [Tooltip("ความเร็วในการเฟดหายไปและโผล่มา")]
    public float fadeSpeed = 2f;

    [Tooltip("ความเข้มสูงสุดของเบลอ (1 = เบลอเต็มที่, 0 = ใสแจ๋ว)")]
    [Range(0f, 1f)]
    public float maxAlpha = 1f;

    [Header("Shader Reference")]
    [Tooltip("ชื่อ Reference ของตัวแปร Alpha ในชเดอร์")]
    public string shaderAlphaRef = "_BlurAlpha";

    private Coroutine fadeCoroutine;
    private float currentAlpha;
    private readonly List<Material> materials = new List<Material>();

    private void Start()
    {
        currentAlpha = maxAlpha; // เริ่มต้นให้เบลอเต็มที่

        // เก็บ material instance ของแต่ละแผ่นไว้ (ห้องไหนเฟดก็ไม่กระทบห้องอื่น)
        foreach (MeshRenderer plane in blurPlanes)
        {
            if (plane == null) continue;

            Material mat = plane.material; // สร้าง instance แยกต่อแผ่น
            if (!mat.HasProperty(shaderAlphaRef))
            {
                Debug.LogWarning("หาตัวแปร: " + shaderAlphaRef + " ใน Shader ของ " + plane.name + " ไม่เจอครับ! เช็คชื่อ Reference ด่วน!");
                continue;
            }

            materials.Add(mat);
            mat.SetFloat(shaderAlphaRef, currentAlpha);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeBlur(0f)); // เข้าห้อง -> เบลอจางหาย
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeBlur(maxAlpha)); // ออกจากห้อง -> เบลอกลับมา
    }

    private IEnumerator FadeBlur(float targetAlpha)
    {
        if (materials.Count == 0) yield break;

        while (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            ApplyAlpha(currentAlpha);
            yield return null;
        }

        currentAlpha = targetAlpha;
        ApplyAlpha(currentAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        foreach (Material mat in materials)
        {
            if (mat != null) mat.SetFloat(shaderAlphaRef, alpha);
        }
    }

    private void OnDestroy()
    {
        // ล้าง material instance ที่สร้างจาก .material กัน memory leak
        foreach (Material mat in materials)
        {
            if (mat != null) Destroy(mat);
        }
    }
}