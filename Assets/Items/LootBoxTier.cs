namespace DCC.Items
{
    /// <summary>
    /// Loot box quality tiers from the DCC books (ascending).
    /// Higher tiers contain better items and rarer drops.
    ///
    /// In the books:
    ///   - Iron/Bronze/Silver: distributed liberally on early floors (potions, bandages, common gear)
    ///   - Gold: boss kills, notable achievements
    ///   - Platinum: System AI discretionary limit (can award up to this tier)
    ///   - Legendary: extremely rare, powerful items
    ///   - Celestial: only 2,145 ever awarded in the history of Dungeon Crawler World.
    ///     No crawler has received more than 4. No more than 18 per season. Often tied to gods.
    /// </summary>
    public enum LootBoxTier
    {
        Iron,
        Bronze,
        Silver,
        Gold,
        Platinum,
        Legendary,
        Celestial
    }

    /// <summary>
    /// Loot box source types from the DCC books.
    /// Each type has different drop conditions and content pools.
    /// </summary>
    public enum LootBoxType
    {
        /// <summary>Standard gear boxes. Most common drop from mobs and exploration.</summary>
        Adventurer,

        /// <summary>Dropped by bosses. Better loot than Adventurer at the same tier.</summary>
        Boss,

        /// <summary>Dropped on PVP kills. Contains PVP Coupons and combat gear.</summary>
        Savage,

        /// <summary>Purchased by alien sponsors/patrons for their favorite crawlers. May contain items from the sponsor's homeworld.</summary>
        Benefactor,

        /// <summary>Tied to gods and cosmic events. Rarest type.</summary>
        Celestial
    }
}
