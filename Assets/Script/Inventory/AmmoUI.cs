using UnityEngine;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject ammoPanel;
    public TextMeshProUGUI ammoText;

    [Header("System References")]
    public PlayerCombat playerCombat;

    [Header("Color Settings")]
    public Color normalColor = Color.white;
    public Color emptyColor = Color.red;

    void Update()
    {
        if (Inventory.Instance == null || playerCombat == null) return;

        ItemData currentWeapon = Inventory.Instance.currentEquippedItem;

        // 🌟 แปลงร่าง ItemData เป็น RangedWeaponData (ปืน) ก่อนเรียกใช้
        if (currentWeapon is RangedWeaponData gun)
        {
            if (!ammoPanel.activeSelf) ammoPanel.SetActive(true);

            int ammoInMag = playerCombat.currentAmmoInMag;
            int spareAmmo = 0;

            if (gun.ammoType != null)
            {
                // เรียกใช้ .ammoType จาก gun ได้เลย
                spareAmmo = Inventory.Instance.GetItemCount(gun.ammoType);
            }

            if (ammoText != null)
            {
                ammoText.text = $"[ {ammoInMag} / {spareAmmo} ]";

                if (spareAmmo <= 0)
                {
                    ammoText.color = emptyColor;
                }
                else
                {
                    ammoText.color = normalColor;
                }
            }
        }
        else
        {
            if (ammoPanel.activeSelf) ammoPanel.SetActive(false);
        }
    }
}