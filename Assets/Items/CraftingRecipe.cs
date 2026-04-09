using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Abilities;

namespace DCC.Items
{
    /// <summary>
    /// A crafting recipe — pure data. Defines ingredients, output, and requirements.
    ///
    /// Crafting in the DCC books:
    ///   - Mordecai (Carl's Game Guide / Manager) is an alchemical master
    ///   - He crafts potions at an Alchemy Table in Carl's Personal Space
    ///   - Crafting uses ingredients from inventory
    ///   - Some recipes require specific skills or tags
    ///
    /// Example recipes:
    ///
    ///   Good Healing Potion:
    ///     Ingredients: [{ Herb x2 }, { Empty Flask x1 }]
    ///     Output: Good Healing Potion x1
    ///     RequiredStation: Alchemy Table
    ///     RequiredSkill: null (anyone can craft basic potions)
    ///
    ///   Superb Strength Potion:
    ///     Ingredients: [{ Ogre Blood x1 }, { Iron Dust x1 }, { Good Mana Potion x1 }]
    ///     Output: Superb Strength Potion x1
    ///     RequiredStation: Alchemy Table
    ///     RequiredSkill: Alchemy (level 5)
    ///     RequiredCrafterTags: [Alchemist]
    ///
    ///   Explosive Device:
    ///     Ingredients: [{ Gunpowder x3 }, { Metal Casing x1 }, { Fuse x1 }]
    ///     Output: Explosive Device x1
    ///     RequiredStation: Workbench
    ///     RequiredSkill: Explosives Handling (level 3)
    ///
    ///   Poison Antidote:
    ///     Ingredients: [{ Antidote Root x1 }, { Purified Water x1 }]
    ///     Output: Poison Antidote x1
    ///     RequiredStation: Alchemy Table
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Crafting Recipe", fileName = "Recipe_New")]
    public class CraftingRecipe : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Ingredients")]
        [field: SerializeField] public RecipeIngredient[] Ingredients { get; private set; }

        [Header("Output")]
        [field: SerializeField, Tooltip("The item produced. Null if output is equipment.")]
        public ItemDefinition OutputItem { get; private set; }

        [field: SerializeField, Tooltip("The equipment produced. Null if output is a consumable item.")]
        public EquipmentDefinition OutputEquipment { get; private set; }

        [field: SerializeField] public int OutputQuantity { get; private set; } = 1;

        [Header("Requirements")]
        [field: SerializeField, Tooltip("The type of crafting station required. Must match station on PersonalSpace.")]
        public CraftingStation RequiredStation { get; private set; }

        [field: SerializeField, Tooltip("Skill required to craft. Null = no skill needed.")]
        public SkillDefinition RequiredSkill { get; private set; }

        [field: SerializeField] public int RequiredSkillLevel { get; private set; }

        [field: SerializeField, Tooltip("Tags the crafter must have to use this recipe.")]
        public TagDefinition[] RequiredCrafterTags { get; private set; }

        [Header("Skill Reward")]
        [field: SerializeField, Tooltip("Skill that gains XP when this recipe is crafted. Null = no XP.")]
        public SkillDefinition SkillToLevel { get; private set; }
    }

    [System.Serializable]
    public struct RecipeIngredient
    {
        public ItemDefinition Item;
        public int Quantity;
    }
}
