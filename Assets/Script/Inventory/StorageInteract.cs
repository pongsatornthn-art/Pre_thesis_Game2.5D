using UnityEngine;

public class StorageInteract : MonoBehaviour
{
    [Header("UI Reference")]
    public StorageBoxUI boxUI;
    public GameObject playerInventoryUI;

    private StorageBox myBox;
    private bool isPlayerNear = false;

    void Start()
    {
        myBox = GetComponent<StorageBox>();
    }

    void Update()
    {
        if (isPlayerNear && InteractInput.PickupPressed) // [แก้โดย Claude 2026-10-07] กลับเป็น E ตามเดิม (ปุ่มเก็บของ/เปิดปิด)
        {
            if (boxUI.uiPanel.activeSelf)
            {
                CloseUIs();
            }
            else
            {
                OpenUIs();
            }
        }

        if (boxUI.uiPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUIs();
        }
    }


    private void OpenUIs()
    {
        boxUI.OpenBox(myBox);
        if (playerInventoryUI != null) playerInventoryUI.SetActive(true);
    }

    private void CloseUIs()
    {
        boxUI.CloseBox();
        if (playerInventoryUI != null) playerInventoryUI.SetActive(false);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            CloseUIs();
        }
    }
}