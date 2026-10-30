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

        // ตัดระยะเรดาร์กันทะลุไปโดนกำแพงหลังตัวละคร
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

                    // เรียกใช้ฟังก์ชันเปลี่ยนโหมดแบบใหม่
                    SetMaterialTransparentSafely(mr.material);

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

            if (!hitsThisFrame.Contains(col) && Mathf.Abs(currentColor.a - fw.originalColor.a) < 0.01f)
            {
                currentColor.a = fw.originalColor.a;
                SetMaterialColor(fw.renderer.material, currentColor);

                // คืนค่าเป็นกำแพงทึบปกติ
                SetMaterialOpaqueSafely(fw.renderer.material);

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

    // ฟังก์ชันสำหรับแปลงกำแพงให้โปร่งใสแบบปลอดภัยใน URP
    private void SetMaterialTransparentSafely(Material mat)
    {
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // 🔥 ไฮไลต์ 1: เปิด ZWrite ทิ้งไว้เสมอ แก้ปัญหากำแพงหลังทะลุมาด้านหน้า
        mat.SetInt("_ZWrite", 1);

        // 🔥 ไฮไลต์ 2: ย้ายคิวไปที่ 3001 ให้สูงกว่า 3000 ของ Shader เบลอ จะได้ไม่โดนกลืนหายไป
        mat.renderQueue = 3001;

        // บังคับให้ระบบแสง URP รู้จักความโปร่งใส (แก้อาการกำแพงดำ)
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    // ฟังก์ชันคืนค่ากำแพงให้ทึบสนิทเหมือนเดิม
    private void SetMaterialOpaqueSafely(Material mat)
    {
        mat.SetFloat("_Surface", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.renderQueue = 2000;

        // ปิด Keyword ระบบโปร่งใส
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }
}