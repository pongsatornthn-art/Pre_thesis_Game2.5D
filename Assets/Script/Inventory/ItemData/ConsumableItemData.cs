using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Inventory/Items/Consumable")]
public class ConsumableItemData : ItemData
{
    [Header("Consumable Stats")]
    public int healAmount = 50;
    public float digestionReduceAmount = 20f;

    private void Reset() { itemType = ItemType.Consumable; }
}