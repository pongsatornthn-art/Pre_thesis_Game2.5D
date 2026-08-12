using UnityEngine;
using System;
using System.Collections;

public class HotbarController : MonoBehaviour
{
    [Header("Hotbar Settings")]
    public int totalSlots = 4;
    private int selectedIndex = 0;

    public event Action<int> OnHotbarSlotSelected;

    IEnumerator Start()
    {
        // 🌟 1. สมัครรับการแจ้งเตือน เมื่อกระเป๋ามีการเปลี่ยนแปลง (เช่น เก็บของ/ทิ้งของ)
        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnInventoryChanged += RefreshCurrentSlot;
        }

        yield return new WaitForEndOfFrame();
        SelectSlot(0);
    }

    void OnDestroy()
    {
        // ยกเลิกการรับแจ้งเตือนเมื่อลบสคริปต์ทิ้ง (กัน Error ปลายทาง)
        if (Inventory.Instance != null)
        {
            Inventory.Instance.OnInventoryChanged -= RefreshCurrentSlot;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            selectedIndex--;
            if (selectedIndex < 0) selectedIndex = totalSlots - 1;
            SelectSlot(selectedIndex);
        }
        else if (scroll < 0f)
        {
            selectedIndex++;
            if (selectedIndex >= totalSlots) selectedIndex = 0;
            SelectSlot(selectedIndex);
        }
    }

    private void SelectSlot(int index)
    {
        selectedIndex = index;
        OnHotbarSlotSelected?.Invoke(index);

        // ดึงอาวุธขึ้นมาถือ
        ForceEquipCurrentSlot();
    }

    // 🌟 2. ฟังก์ชันใหม่: คอยเช็กว่าของในช่องปัจจุบัน เปลี่ยนไปจากที่ถืออยู่หรือไม่
    private void RefreshCurrentSlot()
    {
        if (Inventory.Instance == null) return;

        InventoryItem slotItem = Inventory.Instance.GetItemAt(selectedIndex);
        ItemData itemInSlot = (slotItem != null) ? slotItem.itemData : null;

        // ถ้าของในช่องที่ครอบอยู่ "ไม่ตรง" กับของที่ระบบต่อสู้ถืออยู่ (เช่น เพิ่งเก็บปืนเข้าช่องว่าง) ให้บังคับถือทันที
        if (Inventory.Instance.currentEquippedItem != itemInSlot)
        {
            ForceEquipCurrentSlot();
        }
    }

    // 🌟 3. แยกฟังก์ชันบังคับถือของออกมา เพื่อให้ใช้ร่วมกันได้
    private void ForceEquipCurrentSlot()
    {
        if (Inventory.Instance == null) return;

        InventoryItem slotItem = Inventory.Instance.GetItemAt(selectedIndex);

        if (slotItem != null && slotItem.itemData != null)
        {
            Inventory.Instance.EquipItem(slotItem.itemData);
            // Debug.Log($"ถือไอเทม: {slotItem.itemData.itemName}");
        }
        else
        {
            Inventory.Instance.Unequip();
            // Debug.Log("มือเปล่า");
        }
    }
}