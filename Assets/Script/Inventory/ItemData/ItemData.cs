using UnityEngine;
using UnityEngine.Scripting;
public enum ItemType { General, MeleeWeapon, RangedWeapon, Ammo, Totem, Consumable, Key }

[Preserve]
[CreateAssetMenu(fileName = "New General Item", menuName = "Inventory/Items/General Item")]
public class ItemData : ScriptableObject
{
    [Header("General Info (ข้อมูลพื้นฐาน)")]
    public string itemName = "New Item";
    public Sprite icon;
    [TextArea] public string description;
    public Sprite descriptionImage;

    [Header("Stacking & Type")]
    public bool isStackable = true;
    public int maxStack = 99;
    public ItemType itemType;
    public int price = 50;

    [Header("Equipment Visuals")]
    public Sprite equippedSprite;
}