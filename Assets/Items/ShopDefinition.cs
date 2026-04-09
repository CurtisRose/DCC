using UnityEngine;

namespace DCC.Items
{
    /// <summary>
    /// Defines a shop's inventory and pricing.
    ///
    /// In the DCC books:
    ///   - Shops are found in safe rooms, staffed by Bopca Protectors or NPCs.
    ///   - Stock rotates per floor (higher floors = better items, higher prices).
    ///   - Shops sell potions, equipment, crafting materials, and special items.
    ///   - Some shops are class-specific (e.g., magic shops need [Magical] tag).
    ///   - Prices can be marked up or discounted based on Charisma.
    ///
    /// Example shops:
    ///
    ///   "General Store" (every safe room):
    ///     Stock: healing potions, antidotes, bandages, basic weapons
    ///     CharismaDiscount: true
    ///
    ///   "Mordecai's Potion Emporium" (Floor 3+ safe rooms):
    ///     Stock: advanced potions, alchemy ingredients, rare reagents
    ///     RequiredFloor: 3
    ///
    ///   "The Armory" (Floor 5+ safe rooms):
    ///     Stock: uncommon/rare equipment, weapon upgrades
    ///     RequiredFloor: 5
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Shop", fileName = "Shop_New")]
    public class ShopDefinition : ScriptableObject
    {
        [field: SerializeField] public string ShopName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Stock")]
        [field: SerializeField] public ShopEntry[] Stock { get; private set; }

        [Header("Availability")]
        [field: SerializeField, Tooltip("Minimum floor number for this shop to appear.")]
        public int RequiredFloor { get; private set; }

        [field: SerializeField, Tooltip("If true, prices scale down with buyer's Charisma.")]
        public bool CharismaDiscount { get; private set; } = true;

        [field: SerializeField, Tooltip("Discount per Charisma point (e.g., 0.02 = 2% per Cha point).")]
        public float DiscountPerCharisma { get; private set; } = 0.02f;

        [field: SerializeField, Tooltip("Maximum discount percentage (e.g., 0.5 = 50% max discount).")]
        public float MaxDiscount { get; private set; } = 0.5f;
    }

    [System.Serializable]
    public struct ShopEntry
    {
        [Tooltip("Item for sale (mutually exclusive with Equipment).")]
        public ItemDefinition Item;

        [Tooltip("Equipment for sale (mutually exclusive with Item).")]
        public EquipmentDefinition Equipment;

        [Tooltip("Base gold price. 0 = use the item/equipment's GoldValue.")]
        public int BasePrice;

        [Tooltip("Max number in stock per restock cycle. -1 = unlimited.")]
        public int StockLimit;

        [Tooltip("Quantity per purchase (e.g., 3 for a pack of potions).")]
        public int Quantity;

        public string DisplayName => Item != null ? Item.DisplayName
            : Equipment != null ? Equipment.DisplayName
            : "Unknown";

        public int EffectivePrice => BasePrice > 0 ? BasePrice
            : Item != null ? Item.GoldValue
            : Equipment != null ? Equipment.GoldValue
            : 0;
    }
}
