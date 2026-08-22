using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingCycleUI : MonoBehaviour
{
    [Header("UI References (ลาก UI มาใส่ให้ตรง)")]
    public TMP_Text valueText; // Text ตรงกลางที่แสดงค่า (เช่น Thai, English)
    public Button leftButton;  // ปุ่มลูกศรซ้าย
    public Button rightButton; // ปุ่มลูกศรขวา

    private List<string> options = new List<string>();
    private int currentIndex = 0;

    // Delegate ส่ง Event กลับไปบอก Manager ว่า "ผู้เล่นกดเปลี่ยนค่าแล้วนะ!"
    public System.Action<string> OnValueChanged;

    private void Awake()
    {
        // ผูกปุ่มเข้ากับฟังก์ชันเลื่อนซ้าย-ขวา แบบอัตโนมัติ
        if (leftButton != null) leftButton.onClick.AddListener(CycleLeft);
        if (rightButton != null) rightButton.onClick.AddListener(CycleRight);
    }

    public void Setup(List<string> newOptions, string currentValue)
    {
        options = newOptions;
        
        // หาว่าค่าปัจจุบันอยู่ลำดับที่เท่าไหร่ในลิสต์
        currentIndex = options.IndexOf(currentValue);
        if (currentIndex < 0) currentIndex = 0; // ถ้าหาไม่เจอ ให้เริ่มที่ 0

        UpdateUI();
    }

    private void CycleLeft()
    {
        if (options.Count == 0) return;
        
        currentIndex--;
        if (currentIndex < 0) currentIndex = options.Count - 1; // เลื่อนซ้ายสุดแล้ว ให้วนกลับไปขวาสุด
        
        UpdateUI();
        OnValueChanged?.Invoke(options[currentIndex]);
    }

    private void CycleRight()
    {
        if (options.Count == 0) return;
        
        currentIndex++;
        if (currentIndex >= options.Count) currentIndex = 0; // เลื่อนขวาสุดแล้ว ให้วนกลับไปซ้ายสุด
        
        UpdateUI();
        OnValueChanged?.Invoke(options[currentIndex]);
    }

    private void UpdateUI()
    {
        if (options.Count > 0 && valueText != null)
        {
            valueText.text = options[currentIndex];
        }
    }
}
