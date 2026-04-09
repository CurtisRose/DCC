using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Core.Zones;
using DCC.Abilities;

namespace DCC.Items
{
    /// <summary>
    /// Handles crafting requests. Server-authoritative.
    /// Attached to the player entity alongside Inventory and EquipmentManager.
    ///
    /// Crafting flow:
    ///   1. Client calls CraftServerRpc(stationIndex, recipeIndex)
    ///   2. Server validates:
    ///      - Player is in a safe room ([InSafeRoom] tag)
    ///      - Player has access to the station (via PersonalSpace)
    ///      - Player meets tag/skill requirements
    ///      - All ingredients are in Inventory
    ///   3. Server removes ingredients from Inventory
    ///   4. Server adds output to Inventory (or equipment stash)
    ///   5. Server grants skill XP if recipe has a SkillToLevel
    ///   6. Client gets notification of crafted item
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class CraftingManager : NetworkBehaviour
    {
        [SerializeField, Tooltip("Tag required to craft (InSafeRoom). Leave null to allow crafting anywhere.")]
        private TagDefinition _safeRoomTag;

        private TagContainer _tags;
        private Inventory _inventory;
        private SkillTracker _skills;

        private void Awake()
        {
            _tags = GetComponent<TagContainer>();
            _inventory = GetComponent<Inventory>();
            _skills = GetComponent<SkillTracker>();
        }

        // ── Crafting ───────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void CraftServerRpc(int stationIndex, int recipeIndex, ServerRpcParams rpcParams = default)
        {
            // In a full implementation, stationIndex references the player's PersonalSpace
            // installed stations. For now, accept the station and recipe directly.
        }

        /// <summary>
        /// Attempt to craft a recipe at a station. Server-only.
        /// Returns true on success.
        /// </summary>
        public bool Craft(CraftingStation station, CraftingRecipe recipe)
        {
            if (!IsServer || station == null || recipe == null) return false;

            // Safe room check.
            if (_safeRoomTag != null && _tags != null && !_tags.HasTag(_safeRoomTag))
                return false;

            // Station requirement: recipe must belong to this station.
            if (recipe.RequiredStation != null && recipe.RequiredStation != station)
                return false;

            // Station tag requirements.
            if (station.RequiredTags != null && _tags != null)
            {
                foreach (var tag in station.RequiredTags)
                    if (tag != null && !_tags.HasTag(tag)) return false;
            }

            // Recipe tag requirements.
            if (recipe.RequiredCrafterTags != null && _tags != null)
            {
                foreach (var tag in recipe.RequiredCrafterTags)
                    if (tag != null && !_tags.HasTag(tag)) return false;
            }

            // Skill requirement.
            if (recipe.RequiredSkill != null && _skills != null)
            {
                if (_skills.GetSkillLevel(recipe.RequiredSkill) < recipe.RequiredSkillLevel)
                    return false;
            }

            // Check ingredients.
            if (!HasAllIngredients(recipe))
                return false;

            // Consume ingredients.
            ConsumeIngredients(recipe);

            // Produce output.
            if (recipe.OutputItem != null && _inventory != null)
            {
                _inventory.AddItem(recipe.OutputItem, recipe.OutputQuantity);
            }
            // Equipment output would go to an equipment stash (future refinement).

            // Grant skill XP.
            if (recipe.SkillToLevel != null && _skills != null)
                _skills.RecordSkillUse(recipe.SkillToLevel);

            string outputName = recipe.OutputItem != null
                ? recipe.OutputItem.DisplayName
                : recipe.OutputEquipment != null
                    ? recipe.OutputEquipment.DisplayName
                    : "Unknown";

            NotifyCraftedClientRpc(outputName, recipe.OutputQuantity);
            return true;
        }

        // ── Ingredient validation ──────────────────────────────────────────

        private bool HasAllIngredients(CraftingRecipe recipe)
        {
            if (recipe.Ingredients == null || _inventory == null) return true;

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Item == null) continue;
                int found = CountItem(ingredient.Item);
                if (found < ingredient.Quantity) return false;
            }
            return true;
        }

        private void ConsumeIngredients(CraftingRecipe recipe)
        {
            if (recipe.Ingredients == null || _inventory == null) return;

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Item == null) continue;
                int remaining = ingredient.Quantity;

                // Remove items from inventory slots.
                for (int slot = 0; remaining > 0; slot++)
                {
                    var item = _inventory.GetItem(slot);
                    if (item == null) continue;
                    if (item != ingredient.Item) continue;

                    _inventory.RemoveItem(slot);
                    remaining--;
                }
            }
        }

        private int CountItem(ItemDefinition item)
        {
            int count = 0;
            // Iterate inventory slots to count matching items.
            // Inventory doesn't expose slot count publicly, so we probe until null.
            for (int i = 0; i < 100; i++)
            {
                var slotItem = _inventory.GetItem(i);
                if (slotItem == null && i > 20) break; // Past reasonable slot count.
                if (slotItem == item) count++;
            }
            return count;
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyCraftedClientRpc(string itemName, int quantity)
        {
            // Client shows "Crafted: {itemName} x{quantity}" notification.
        }
    }
}
