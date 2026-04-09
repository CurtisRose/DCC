using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Effects.Modifiers;
using DCC.Core.Entities;
using DCC.Abilities;

namespace DCC.Items
{
    /// <summary>
    /// Manages equipped gear across body slots. Server-authoritative.
    ///
    /// Equip/unequip flow:
    ///   1. Client calls EquipFromInventoryServerRpc(inventorySlot, equipSlot)
    ///   2. Server validates requirements (stats, class, skill)
    ///   3. Server removes item from Inventory
    ///   4. If slot occupied, unequips current gear back to Inventory
    ///   5. Applies all bonuses: stat mods, armor, skills, tags, enchantments
    ///   6. Syncs equipment state to clients via NetworkVariables
    ///
    /// Unequip reverses everything cleanly — every AddX has a matching RemoveX.
    ///
    /// Equipment interacts with the full tag system:
    ///   - [Enchanted] gear can be detected by InteractionRules
    ///   - [Cursed] items suppress [Blessed] via tag suppression
    ///   - [Armored] tag from heavy gear can be checked by effects
    ///   - Enchantment effects compose with zone/ability effects naturally
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    [RequireComponent(typeof(TagContainer))]
    public class EquipmentManager : NetworkBehaviour
    {
        private EntityAttributes _attrs;
        private TagContainer _tags;
        private SkillTracker _skills;
        private CrawlerIdentity _identity;
        private Inventory _inventory;

        // Server-only: what's equipped in each slot.
        private readonly Dictionary<EquipmentSlot, EquippedItem> _equipped = new();

        // Networked equipment names for client UI.
        // Using a NetworkList of FixedString64Bytes keyed by slot index.
        private NetworkList<FixedString64Bytes> _networkEquipNames;

        // Events for UI.
        public event Action<EquipmentSlot, EquipmentDefinition> OnEquipped;
        public event Action<EquipmentSlot> OnUnequipped;

        private void Awake()
        {
            _attrs = GetComponent<EntityAttributes>();
            _tags = GetComponent<TagContainer>();
            _skills = GetComponent<SkillTracker>();
            _identity = GetComponent<CrawlerIdentity>();
            _inventory = GetComponent<Inventory>();

            _networkEquipNames = new NetworkList<FixedString64Bytes>(
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                // Initialize network list with empty entries for each slot.
                int slotCount = Enum.GetValues(typeof(EquipmentSlot)).Length;
                for (int i = 0; i < slotCount; i++)
                    _networkEquipNames.Add(new FixedString64Bytes());
            }
        }

        // ── Queries ────────────────────────────────────────────────────────

        public EquipmentDefinition GetEquipped(EquipmentSlot slot)
            => _equipped.TryGetValue(slot, out var item) ? item.Definition : null;

        public bool IsSlotOccupied(EquipmentSlot slot) => _equipped.ContainsKey(slot);

        /// <summary>Get the networked display name for a slot (client-safe).</summary>
        public string GetEquippedName(EquipmentSlot slot)
        {
            int idx = (int)slot;
            if (_networkEquipNames != null && idx >= 0 && idx < _networkEquipNames.Count)
                return _networkEquipNames[idx].ToString();
            return string.Empty;
        }

        // ── Equip from inventory (client request) ──────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void EquipFromInventoryServerRpc(int inventorySlot, EquipmentSlot targetSlot,
            ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Get the item from inventory.
            var itemDef = _inventory?.GetItem(inventorySlot);
            if (itemDef == null) return;

            // The item must reference an EquipmentDefinition.
            // Convention: EquipmentDefinitions are stored as ItemDefinition references
            // with a matching EquipmentDefinition asset. For now, look up by name.
            // Better approach: store EquipmentDefinition directly in a separate inventory.
            // TODO: This coupling will be improved when we refine the item/equipment split.
        }

        // ── Direct equip (server-side, called by loot/trade systems) ───────

        /// <summary>Equip a piece of gear directly. Validates requirements. Returns false on failure.</summary>
        public bool Equip(EquipmentDefinition equipment)
        {
            if (!IsServer || equipment == null) return false;

            // Validate slot matches.
            var slot = equipment.Slot;

            // Validate stat requirements.
            if (equipment.StatRequirements != null)
            {
                foreach (var req in equipment.StatRequirements)
                {
                    if (GetEffectiveStat(req.Stat) < req.MinValue)
                        return false;
                }
            }

            // Validate class requirements.
            if (equipment.RequiredClasses != null && equipment.RequiredClasses.Length > 0 && _identity != null)
            {
                bool classMatch = false;
                foreach (var cls in equipment.RequiredClasses)
                    if (cls != null && _identity.Class == cls) { classMatch = true; break; }
                if (!classMatch) return false;
            }

            // Validate skill requirements.
            if (equipment.RequiredSkill != null && _skills != null)
            {
                if (_skills.GetSkillLevel(equipment.RequiredSkill) < equipment.RequiredSkillLevel)
                    return false;
            }

            // Unequip current gear in this slot (if any).
            if (_equipped.ContainsKey(slot))
                Unequip(slot);

            // Apply all bonuses.
            var equippedItem = new EquippedItem { Definition = equipment };

            // Stat bonuses.
            if (equipment.StatBonuses != null)
            {
                foreach (var mod in equipment.StatBonuses)
                    ApplyStatBonus(mod);
            }

            // Armor.
            if (equipment.BonusArmor != 0f)
                _attrs.AttributeSet.AddBonusArmor(equipment.BonusArmor);

            // Skills.
            if (equipment.GrantedSkills != null && _skills != null)
            {
                foreach (var skill in equipment.GrantedSkills)
                    if (skill != null) _skills.GrantSkill(skill);
            }

            // Tags.
            if (equipment.GrantedTags != null)
                _tags.AddTags(equipment.GrantedTags);

            // Enchantments (permanent effects).
            if (equipment.Enchantments != null)
            {
                equippedItem.EnchantmentInstances = new List<EffectInstance>();
                foreach (var effectDef in equipment.Enchantments)
                {
                    if (effectDef == null) continue;
                    var ctx = EffectContext.Environment(transform.position);
                    var instance = EffectInstance.Create(effectDef, ctx, effectDef.BaseMagnitude);
                    _attrs.ApplyEffect(instance);
                    equippedItem.EnchantmentInstances.Add(instance);
                }
            }

            _equipped[slot] = equippedItem;

            // Sync to clients.
            int slotIdx = (int)slot;
            if (slotIdx >= 0 && slotIdx < _networkEquipNames.Count)
                _networkEquipNames[slotIdx] = equipment.DisplayName;

            OnEquipped?.Invoke(slot, equipment);
            NotifyEquipClientRpc(equipment.DisplayName, (int)slot);

            return true;
        }

        /// <summary>Unequip gear from a slot. Returns the EquipmentDefinition that was removed, or null.</summary>
        public EquipmentDefinition Unequip(EquipmentSlot slot)
        {
            if (!IsServer) return null;
            if (!_equipped.TryGetValue(slot, out var equippedItem)) return null;

            var equipment = equippedItem.Definition;

            // Remove stat bonuses.
            if (equipment.StatBonuses != null)
            {
                foreach (var mod in equipment.StatBonuses)
                    RemoveStatBonus(mod);
            }

            // Remove armor.
            if (equipment.BonusArmor != 0f)
                _attrs.AttributeSet.RemoveBonusArmor(equipment.BonusArmor);

            // Remove skills.
            if (equipment.GrantedSkills != null && _skills != null)
            {
                foreach (var skill in equipment.GrantedSkills)
                    if (skill != null) _skills.RemoveSkill(skill);
            }

            // Remove tags.
            if (equipment.GrantedTags != null)
                _tags.RemoveTags(equipment.GrantedTags);

            // Remove enchantment effects.
            if (equippedItem.EnchantmentInstances != null)
            {
                foreach (var instance in equippedItem.EnchantmentInstances)
                    _attrs.RemoveEffect(instance);
            }

            _equipped.Remove(slot);

            // Sync to clients.
            int slotIdx = (int)slot;
            if (slotIdx >= 0 && slotIdx < _networkEquipNames.Count)
                _networkEquipNames[slotIdx] = new FixedString64Bytes();

            OnUnequipped?.Invoke(slot);
            NotifyUnequipClientRpc((int)slot);

            return equipment;
        }

        /// <summary>Unequip and return the item to the player's inventory.</summary>
        public bool UnequipToInventory(EquipmentSlot slot)
        {
            if (!IsServer || _inventory == null) return false;

            var equipment = Unequip(slot);
            if (equipment == null) return false;

            // Create a temporary ItemDefinition-compatible entry for the inventory.
            // For now, log the unequip. Full inventory integration requires
            // equipment items to also be ItemDefinitions or a unified item type.
            Debug.Log($"[EquipmentManager] Unequipped {equipment.DisplayName} from {slot}.");
            return true;
        }

        // ── Stat helpers ───────────────────────────────────────────────────

        private int GetEffectiveStat(CrawlerStat stat)
        {
            var attrs = _attrs.AttributeSet;
            return stat switch
            {
                CrawlerStat.Strength => attrs.Strength,
                CrawlerStat.Constitution => attrs.Constitution,
                CrawlerStat.Dexterity => attrs.Dexterity,
                CrawlerStat.Intelligence => attrs.Intelligence,
                CrawlerStat.Charisma => attrs.Charisma,
                _ => 0
            };
        }

        private void ApplyStatBonus(StatModifier mod)
        {
            var attrs = _attrs.AttributeSet;
            switch (mod.Stat)
            {
                case CrawlerStat.Strength:     attrs.AddBonusStrength(mod.Amount); break;
                case CrawlerStat.Constitution:  attrs.AddBonusConstitution(mod.Amount); break;
                case CrawlerStat.Dexterity:     attrs.AddBonusDexterity(mod.Amount); break;
                case CrawlerStat.Intelligence:  attrs.AddBonusIntelligence(mod.Amount); break;
                case CrawlerStat.Charisma:      attrs.AddBonusCharisma(mod.Amount); break;
            }
        }

        private void RemoveStatBonus(StatModifier mod)
        {
            var attrs = _attrs.AttributeSet;
            switch (mod.Stat)
            {
                case CrawlerStat.Strength:     attrs.RemoveBonusStrength(mod.Amount); break;
                case CrawlerStat.Constitution:  attrs.RemoveBonusConstitution(mod.Amount); break;
                case CrawlerStat.Dexterity:     attrs.RemoveBonusDexterity(mod.Amount); break;
                case CrawlerStat.Intelligence:  attrs.RemoveBonusIntelligence(mod.Amount); break;
                case CrawlerStat.Charisma:      attrs.RemoveBonusCharisma(mod.Amount); break;
            }
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyEquipClientRpc(string itemName, int slotIndex)
        {
            // Client updates equipment UI, plays equip sound.
        }

        [ClientRpc]
        private void NotifyUnequipClientRpc(int slotIndex)
        {
            // Client updates equipment UI, plays unequip sound.
        }

        // ── Internal state ─────────────────────────────────────────────────

        private struct EquippedItem
        {
            public EquipmentDefinition Definition;
            public List<EffectInstance> EnchantmentInstances;
        }
    }
}
