using UnityEngine;

namespace DCC.Items
{
    /// <summary>
    /// Defines a loot box that can be collected and opened.
    ///
    /// In the DCC books:
    ///   - Loot boxes can ONLY be opened in Safe Rooms.
    ///   - Boxes are collected during gameplay and stored until a safe room is reached.
    ///   - Benefactor Boxes are purchased by alien sponsors at great expense.
    ///     Cost scales with tier. May contain items from the sponsor's homeworld.
    ///   - Savage Boxes drop from PVP kills. Always contain PVP Coupons + combat loot.
    ///   - Celestial Boxes are the rarest — only ~2,145 ever awarded in DCC history.
    ///
    /// Example configurations:
    ///
    ///   Bronze Adventurer Box:
    ///     Tier: Bronze, Type: Adventurer
    ///     LootTable: BronzeAdventurer_LootTable (potions, bandages, common gear)
    ///
    ///   Gold Boss Box:
    ///     Tier: Gold, Type: Boss
    ///     LootTable: GoldBoss_LootTable (rare equipment, stat potions, skill tomes)
    ///
    ///   Savage Box:
    ///     Tier: Silver, Type: Savage
    ///     LootTable: Savage_LootTable (PVP coupons, combat consumables)
    ///     PvpCoupons: 5
    ///
    ///   Platinum Benefactor Box:
    ///     Tier: Platinum, Type: Benefactor
    ///     LootTable: PlatBenefactor_LootTable (sponsor-themed items, powerful gear)
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Loot Box", fileName = "LootBox_New")]
    public class LootBoxDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Classification")]
        [field: SerializeField] public LootBoxTier Tier { get; private set; } = LootBoxTier.Iron;
        [field: SerializeField] public LootBoxType Type { get; private set; } = LootBoxType.Adventurer;

        [Header("Loot")]
        [field: SerializeField, Tooltip("The loot table rolled when this box is opened.")]
        public LootTable LootTable { get; private set; }

        [field: SerializeField, Tooltip("PVP Coupons included (Savage boxes). 0 for non-Savage boxes.")]
        public int PvpCoupons { get; private set; }

        [field: SerializeField, Tooltip(
            "Bonus roll count added to the loot table's base RollCount. " +
            "Higher tier boxes get more rolls.")]
        public int BonusRolls { get; private set; }
    }
}
