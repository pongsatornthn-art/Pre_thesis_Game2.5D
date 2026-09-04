using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LootDrop
{
    public ItemData item;
    [Range(0, 100)] public float dropChance = 50f;
    public int minAmount = 1;
    public int maxAmount = 1;
}

[CreateAssetMenu(fileName = "New Loot Table", menuName = "Inventory/Loot Table")]
public class LootTableData : ScriptableObject
{
    [Header("Loot Settings")]
    public List<LootDrop> possibleLoot;
}