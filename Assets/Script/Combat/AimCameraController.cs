using UnityEngine;

public class AimCameraController : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject vCamNormal; // ลาก VCam_Player มาใส่ช่องนี้
    public GameObject vCamAiming; // ลาก VCam_Aim มาใส่ช่องนี้

    [Header("References")]
    public PlayerCombat playerCombat; // ลาก Player มาใส่

    void Update()
    {
        if (playerCombat == null || vCamNormal == null || vCamAiming == null) return;

        // สลับกล้องตามสถานะการเล็ง (Cinemachine จะคำนวณการซูมเข้า-ออกให้สมูทเอง!)
        if (playerCombat.isAiming)
        {
            vCamAiming.SetActive(true);
            vCamNormal.SetActive(false);
        }
        else
        {
            vCamAiming.SetActive(false);
            vCamNormal.SetActive(true);
        }
    }
}