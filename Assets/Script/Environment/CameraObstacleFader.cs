using System.Collections.Generic;
using UnityEngine;

public class CameraObstacleFader : MonoBehaviour
{
    [Header("เป้าหมายที่กล้องจับ (ลาก Player มาใส่)")]
    public Transform player;

    [Header("ตั้งค่าเรดาร์ตรวจจับ")]
    public LayerMask wallLayer;
    public float castRadius = 0.5f;

    [Header("ตั้งค่าการเฟด")]
    public float targetAlpha = 0.2f;
    public float fadeSpeed = 5f;

    private class FadedWall
    {
        public MeshRenderer renderer;
        public Color originalColor;
        public float currentTargetAlpha;
    }

    private Dictionary<Collider, FadedWall> activeWalls = new Dictionary<Collider, FadedWall>();
    private HashSet<Collider> hitsThisFrame = new HashSet<Collider>();

    void Update()
    {
        if (player == null) return;

        hitsThisFrame.Clear();

        // 🌟 ตัดระยะเรดาร์ลง (castRadius + 0.5f) ป้องกันบั๊กเรดาร์ทะลุไปชนกำแพงด้านหลังผู้เล่น
        Vector3 direction = player.position - transform.position;
        float distance = direction.magnitude - (castRadius + 0.5f);
        if (distance < 0) distance = 0.1f;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, castRadius, direction, distance, wallLayer);

        foreach (RaycastHit hit in hits)
        {
            hitsThisFrame.Add(hit.collider);

            if (!activeWalls.ContainsKey(hit.collider))
            {
                MeshRenderer mr = hit.collider.GetComponent<MeshRenderer>();
                if (mr != null && (mr.material.HasProperty("_BaseColor") || mr.material.HasProperty("_Color")))
                {
                    FadedWall fw = new FadedWall
                    {
                        renderer = mr,
                        originalColor = GetMaterialColor(mr.material),
                        currentTargetAlpha = targetAlpha
                    };

                    // 🌟 เมื่อเริ่มโดนบัง สั่งสลับ Material ชิ้นนี้ให้เป็น Transparent ทันที
                    SetMaterialMode(mr.material, true);

                    activeWalls.Add(hit.collider, fw);
                }
            }
            else
            {
                activeWalls[hit.collider].currentTargetAlpha = targetAlpha;
            }
        }

        List<Collider> keysToRemove = new List<Collider>();

        foreach (var kvp in activeWalls)
        {
            Collider col = kvp.Key;
            FadedWall fw = kvp.Value;

            if (!hitsThisFrame.Contains(col))
            {
                fw.currentTargetAlpha = fw.originalColor.a;
            }

            Color currentColor = GetMaterialColor(fw.renderer.material);
            currentColor.a = Mathf.Lerp(currentColor.a, fw.currentTargetAlpha, fadeSpeed * Time.deltaTime);
            SetMaterialColor(fw.renderer.material, currentColor);

            // 🌟 เมื่อกำแพงพ้นจากการบัง และสีเฟดกลับมาทึบ 100% แล้ว
            if (!hitsThisFrame.Contains(col) && Mathf.Abs(currentColor.a - fw.originalColor.a) < 0.01f)
            {
                currentColor.a = fw.originalColor.a;
                SetMaterialColor(fw.renderer.material, currentColor);

                // สลับ Material ชิ้นนี้กลับไปเป็น Opaque เพื่อแก้ปัญหาภาพทะลุกัน
                SetMaterialMode(fw.renderer.material, false);

                keysToRemove.Add(col);
            }
        }

        foreach (var key in keysToRemove)
        {
            activeWalls.Remove(key);
        }
    }

    private Color GetMaterialColor(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.color;
        return Color.white;
    }

    private void SetMaterialColor(Material mat, Color newColor)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", newColor);
        else if (mat.HasProperty("_Color")) mat.color = newColor;
    }

    // ฟังก์ชันสำหรับสลับโหมด Opaque <-> Transparent แบบ Real-time สำหรับ URP
    private void SetMaterialMode(Material mat, bool isTransparent)
    {
        if (isTransparent)
        {
            mat.SetFloat("_Surface", 1); // 1 = โหมด Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);     // ปิดคำนวณความลึกชั่วคราวให้โปร่งใสได้
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.SetFloat("_Surface", 0); // 0 = โหมด Opaque
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);     // เปิดคำนวณความลึกกลับมา (แก้บั๊กภาพทะลุ)
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }
}