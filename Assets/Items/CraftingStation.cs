using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Items
{
    /// <summary>
    /// Defines a type of crafting station that can be installed in a Personal Space.
    ///
    /// Stations from the DCC books:
    ///
    ///   Alchemy Table:
    ///     Description: "Brew potions and antidotes."
    ///     RequiredTags: [] (anyone can use, but recipes may require [Alchemist])
    ///     Recipes: all potion crafting recipes
    ///     → Mordecai's specialty. He crafts at Carl's Alchemy Table.
    ///
    ///   Workbench:
    ///     Description: "Craft traps, explosives, and mechanical devices."
    ///     RequiredTags: [] (recipes gate by skill)
    ///     Recipes: explosive devices, traps, mechanical items
    ///
    ///   Enchanting Table:
    ///     Description: "Imbue equipment with magical effects."
    ///     RequiredTags: [Magical] (need some magical capability)
    ///     Recipes: enchantment-related crafting
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Crafting Station", fileName = "Station_New")]
    public class CraftingStation : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Requirements")]
        [field: SerializeField, Tooltip("Tags the crafter must have to use this station at all.")]
        public TagDefinition[] RequiredTags { get; private set; }

        [Header("Recipes")]
        [field: SerializeField, Tooltip("All recipes available at this station.")]
        public CraftingRecipe[] AvailableRecipes { get; private set; }

        [Header("Economy")]
        [field: SerializeField, Tooltip("Gold cost to install this station in a Personal Space.")]
        public int InstallCost { get; private set; } = 200;
    }
}
