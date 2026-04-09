namespace DCC.Items
{
    /// <summary>
    /// Body slots where equipment can be worn.
    ///
    /// In the DCC books, equipment is a major source of power:
    ///   - Carl's Enchanted Pedicure Kit of the Sylph (Feet)
    ///   - Gauntlet of the Blood-Soaked Path (Hands)
    ///   - Various enchanted armor pieces and accessories
    ///
    /// Two ring slots allow stacking ring effects — a common RPG pattern
    /// that matches the books' approach to magical accessories.
    /// </summary>
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        Ring1,
        Ring2,
        Weapon,
        Offhand
    }
}
