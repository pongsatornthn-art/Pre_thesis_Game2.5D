using UnityEngine;

public class SeeThroughWall : MonoBehaviour
{
    [Header("Settings")]
    public Transform player;         // ลาก Player มาใส่ช่องนี้
    public LayerMask wallLayer;      // เลือก Layer "Wall"
    [Range(0f, 1f)]
    public float transparentAlpha = 0.3f; // ความโปร่งใส (0 = ล่องหน, 1 = ทึบ)

    private Transform currentWall;
    private Color originalColor;
    private Material wallMaterial;

    void Update()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        float distance = direction.magnitude;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance, wallLayer))
        {
            if (currentWall != hit.transform)
            {
                ResetWall();
                currentWall = hit.transform;

                wallMaterial = currentWall.GetComponent<MeshRenderer>().material;

                // 🌟 เปลี่ยนมาใช้ GetColor ของ URP
                originalColor = wallMaterial.GetColor("_BaseColor");

                Color newColor = originalColor;
                newColor.a = transparentAlpha;

                // 🌟 เปลี่ยนมาใช้ SetColor ของ URP
                wallMaterial.SetColor("_BaseColor", newColor);
            }
        }
        else
        {
            ResetWall();
        }
    }

    private void ResetWall()
    {
        if (currentWall != null && wallMaterial != null)
        {
            // 🌟 เปลี่ยนมาใช้ SetColor ของ URP
            wallMaterial.SetColor("_BaseColor", originalColor);
            currentWall = null;
            wallMaterial = null;
        }
    }
    }