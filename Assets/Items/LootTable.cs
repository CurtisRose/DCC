using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCC.Items
{
    /// <summary>
    /// Weighted loot table for rolling item/equipment drops.
    /// Referenced by LootBoxDefinition assets.
    ///
    /// Each entry has a weight (higher = more likely), an optional item or equipment,
    /// a quantity range, and a guaranteed flag for must-drop entries.
    ///
    /// Rolling uses weighted random selection. The table can roll multiple items
    /// at once (e.g., a Gold Boss Box might roll 3 items from the table).
    ///
    /// Example table for a Bronze Adventurer Box:
    ///   { Good Healing Potion,  weight: 40, qty: 1-3 }
    ///   { Bandage,              weight: 30, qty: 2-5 }
    ///   { Iron Helmet,          weight: 10, qty: 1   }
    ///   { Iron Sword,           weight: 10, qty: 1   }
    ///   { Good Mana Potion,     weight: 8,  qty: 1-2 }
    ///   { Poison Antidote,      weight: 2,  qty: 1   }
    ///
    /// Example table for a Gold Boss Box:
    ///   { Rare Equipment (any), weight: 30, qty: 1   }
    ///   { Superb Str Potion,    weight: 15, qty: 1   }
    ///   { Great Healing Potion, weight: 25, qty: 1-2 }
    ///   { Skill Tome,           weight: 10, qty: 1   }
    ///   { Gold Coins,           weight: 20, qty: 50-200 }
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Loot Table", fileName = "LootTable_New")]
    public class LootTable : ScriptableObject
    {
        [field: SerializeField] public LootEntry[] Entries { get; private set; }

        [field: SerializeField, Tooltip("Number of rolls when this table is used. Each roll picks one entry.")]
        public int RollCount { get; private set; } = 1;

        /// <summary>
        /// Roll the table once, returning a single entry based on weighted random selection.
        /// Uses System.Random for deterministic server-side rolls.
        /// </summary>
        public LootEntry? Roll(System.Random rng)
        {
            if (Entries == null || Entries.Length == 0) return null;

            float totalWeight = 0f;
            foreach (var entry in Entries)
                totalWeight += entry.Weight;

            if (totalWeight <= 0f) return null;

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            foreach (var entry in Entries)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative)
                    return entry;
            }

            return Entries[Entries.Length - 1];
        }

        /// <summary>
        /// Roll the table RollCount times. Returns all results including guaranteed drops.
        /// </summary>
        public List<LootResult> RollAll(System.Random rng)
        {
            var results = new List<LootResult>();

            // Guaranteed drops always included.
            if (Entries != null)
            {
                foreach (var entry in Entries)
                {
                    if (entry.Guaranteed)
                        results.Add(ResolveEntry(entry, rng));
                }
            }

            // Random rolls.
            for (int i = 0; i < RollCount; i++)
            {
                var entry = Roll(rng);
                if (entry.HasValue)
                    results.Add(ResolveEntry(entry.Value, rng));
            }

            return results;
        }

        private LootResult ResolveEntry(LootEntry entry, System.Random rng)
        {
            int qty = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            return new LootResult
            {
                Item = entry.Item,
                Equipment = entry.Equipment,
                GoldAmount = entry.GoldAmount > 0 ? entry.GoldAmount * qty : 0,
                Quantity = entry.GoldAmount > 0 ? 1 : qty
            };
        }
    }

    [Serializable]
    public struct LootEntry
    {
        [Tooltip("Consumable/usable item to drop. Leave null if dropping equipment or gold.")]
        public ItemDefinition Item;

        [Tooltip("Equipment to drop. Leave null if dropping a consumable item or gold.")]
        public EquipmentDefinition Equipment;

        [Tooltip("Gold amount per quantity. Set > 0 for gold drops (Item and Equipment should be null).")]
        public int GoldAmount;

        [Tooltip("Selection weight. Higher = more likely to be rolled.")]
        public float Weight;

        [Tooltip("Minimum quantity when this entry is selected.")]
        public int MinQuantity;

        [Tooltip("Maximum quantity (inclusive).")]
        public int MaxQuantity;

        [Tooltip("If true, this entry is always included regardless of rolls.")]
        public bool Guaranteed;
    }

    /// <summary>Result of a single loot table roll. Exactly one of Item/Equipment/Gold will be set.</summary>
    public struct LootResult
    {
        public ItemDefinition Item;
        public EquipmentDefinition Equipment;
        public int GoldAmount;
        public int Quantity;

        public bool IsItem => Item != null;
        public bool IsEquipment => Equipment != null;
        public bool IsGold => GoldAmount > 0 && Item == null && Equipment == null;
    }
}
