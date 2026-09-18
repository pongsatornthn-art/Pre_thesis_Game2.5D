using UnityEngine;

public class AimCameraController : MonoBehaviour
{

    [Header("References")]
    public PlayerCombat playerCombat; 
    public Transform playerTransform;

    [Header("ตั้งค่าการแพนกล้องตามเมาส์")]
    [Range(0f, 1f)]
    public float normalMouseWeight = 0.2f; 
    [Range(0f, 1f)]
    public float aimingMouseWeight = 0.4f; 

    public float maxCameraDistance = 4f;   
    public float smoothSpeed = 10f;         

    void Update()
    {

        HandleMouseTracking();
    }

    void HandleMouseTracking()
    {
        if (playerTransform == null || Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, playerTransform.position);

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 mouseWorldPosition = ray.GetPoint(rayDistance);

            float currentWeight = (playerCombat != null && playerCombat.isAiming) ? aimingMouseWeight : normalMouseWeight;

            Vector3 targetPos = Vector3.Lerp(playerTransform.position, mouseWorldPosition, currentWeight);

            Vector3 direction = targetPos - playerTransform.position;
            if (direction.magnitude > maxCameraDistance)
            {
                targetPos = playerTransform.position + (direction.normalized * maxCameraDistance);
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
        }
    }
}