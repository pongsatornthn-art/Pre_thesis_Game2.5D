using UnityEngine;
using UnityEngine.UI;
using TMPro; // 🌟 1. เพิ่มบรรทัดนี้เพื่อเรียกใช้ TextMeshPro

public class SafeMinigame : MonoBehaviour
{
    [Header("ระบบ 3D (ตู้เซฟ)")]
    public GameObject safeCanvasUI;
    private bool isPlayerNear = false;
    private bool isMinigameActive = false;

    [Header("ตั้งค่ารหัสผ่านและของรางวัล")]
    public string correctPasscode = "1234";
    public KeyItemData rewardKey;

    [Header("ระบบ UI มินิเกม")]
    public RectTransform dialImage;

    // 🌟 2. เปลี่ยนจาก Text ธรรมดา เป็น TextMeshProUGUI
    public TextMeshProUGUI[] digitDisplays;

    private string currentInput = "";
    private int currentDialNumber = 0;

    void Start()
    {
        if (safeCanvasUI != null) safeCanvasUI.SetActive(false);
    }

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.E) && !isMinigameActive)
        {
            OpenSafeMinigame();
        }

        if (isMinigameActive && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSafeMinigame();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNear = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            CloseSafeMinigame();
        }
    }

    public void OpenSafeMinigame()
    {
        isMinigameActive = true;
        if (safeCanvasUI != null) safeCanvasUI.SetActive(true);
        currentInput = "";
        currentDialNumber = 0;
        UpdateDialVisual();
        UpdateDisplay();
    }

    public void CloseSafeMinigame()
    {
        isMinigameActive = false;
        if (safeCanvasUI != null) safeCanvasUI.SetActive(false);
    }

    public void RotateDialLeft()
    {
        currentDialNumber = (currentDialNumber - 1 + 10) % 10;
        UpdateDialVisual();
    }

    public void RotateDialRight()
    {
        currentDialNumber = (currentDialNumber + 1) % 10;
        UpdateDialVisual();
    }

    private void UpdateDialVisual()
    {
        if (dialImage != null)
            dialImage.localEulerAngles = new Vector3(0, 0, currentDialNumber * 36f);
    }

    public void ConfirmDigit()
    {
        if (currentInput.Length < 4)
        {
            currentInput += currentDialNumber.ToString();
            UpdateDisplay();

            if (currentInput.Length == 4)
            {
                CheckPasscode();
            }
        }
    }

    private void UpdateDisplay()
    {
        for (int i = 0; i < 4; i++)
        {
            if (digitDisplays != null && i < digitDisplays.Length && digitDisplays[i] != null)
            {
                if (i < currentInput.Length)
                    digitDisplays[i].text = currentInput[i].ToString();
                else
                    digitDisplays[i].text = "-";
            }
        }
    }

    private void CheckPasscode()
    {
        if (currentInput == correctPasscode)
        {
            Debug.Log("รหัสถูกต้อง! ตู้เซฟเปิดแล้ว");

            if (rewardKey != null)
            {
                rewardKey.Collect();
            }

            CloseSafeMinigame();

            if (GetComponent<Collider>() != null)
                GetComponent<Collider>().enabled = false;

            this.enabled = false;
        }
        else
        {
            Debug.Log("รหัสผิดพลาด! รีเซ็ตใหม่");
            currentInput = "";
            UpdateDisplay();
        }
    }
}