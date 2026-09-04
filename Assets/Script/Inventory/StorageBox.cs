using System;
using System.Collections.Generic;
using UnityEngine;

public class StorageBox : MonoBehaviour
{
    [Header("Storage Settings")]
    public int space = 16;
    public List<InventoryItem> items = new List<InventoryItem>();

    [Header("Loot Generation")]
    public bool generateLootOnStart = true;
    public int maxLootItems = 3;

    // 🌟 เปลี่ยนมารับค่าจากไฟล์ Data แทน!
    public LootTableData lootTable;

    public event Action OnStorageChanged;

    void Awake()
    {
        while (items.Count < space) items.Add(null);
    }

    void Start()
    {
        if (generateLootOnStart)
        {
            GenerateRandomLoot();
        }
    }

    private void GenerateRandomLoot()
    {
        // 🌟 ถ้าไม่ได้ใส่ไฟล์ Data หรือในไฟล์ไม่มีของ ให้ข้ามไปเลย
        if (lootTable == null || lootTable.possibleLoot == null || lootTable.possibleLoot.Count == 0) return;

        for (int i = 0; i < maxLootItems; i++)
        {
            LootDrop randomLoot = lootTable.possibleLoot[UnityEngine.Random.Range(0, lootTable.possibleLoot.Count)];

            float roll = UnityEngine.Random.Range(0f, 100f);
            if (roll <= randomLoot.dropChance)
            {
                int amount = UnityEngine.Random.Range(randomLoot.minAmount, randomLoot.maxAmount + 1);

                int emptyIndex = items.FindIndex(slot => slot == null);
                if (emptyIndex != -1)
                {
                    items[emptyIndex] = new InventoryItem(randomLoot.item, amount);
                }
            }
        }
    }

    public void SwapItems(int fromIndex, int toIndex)
    {
        while (items.Count <= Mathf.Max(fromIndex, toIndex)) items.Add(null);

        var temp = items[fromIndex];
        items[fromIndex] = items[toIndex];
        items[toIndex] = temp;

        RefreshUI();
    }

    public void RefreshUI()
    {
        OnStorageChanged?.Invoke();
    }
}