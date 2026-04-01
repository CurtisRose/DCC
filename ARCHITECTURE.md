# Dungeon Crawler Carl — Architecture Reference

## Emergent Gameplay System

The core design principle: **effects are data, not code**.
No interaction is hardcoded. Every emergent combination falls out naturally from
the tag system.

### How "Healing Smoke" works (no code written for it)

1. `SmokeGrenade` spawns a `ZoneInstance` with tags `[Gas, Smoke, Obscuring]`
2. Player throws a `HealingPotion` (UseMode: `InfuseZone`) into the zone
3. `Inventory.InfuseNearestZone()` calls `zone.InfuseEffect(HealEffect, ctx)`
4. `HealEffect.GrantedTags = [Healing]` → added to zone's `TagContainer`
5. Zone tags are now `[Gas, Smoke, Obscuring, Healing]`
6. `ZoneInstance` ticks every 1s → `EffectComposer.ApplyAll([SmokeEffect, HealEffect], entity, ctx)`
7. `HealEffect.CanApplyTo` checks `RequiredTargetTags: [Living]` → passes for players
8. Entity heals. **Zero code written for this specific interaction.**

### How "Grenade heals Living, damages Undead" works

Configure one `ItemDefinition` with two effects:
```
Effects:
  HealEffect   { RequiredTargetTags: [Living],  BaseMagnitude: 50 }
  DamageEffect { RequiredTargetTags: [Undead],  BaseMagnitude: 50, GrantedTags: [Radiant] }
```
The `RequiredTargetTags` gate means each effect only fires on the right entity type.

### How "Teleport Trap triggers on items OR people" works

Configure `TeleportEffect.RequiredTargetTags`:
- `[Corporeal]` → triggers on anything physical (items + players + enemies)
- `[Living, Player]` → players only
- `[Item]` → items only
- `[Undead]` → undead only (holy ward)

No code change required for any variation.

### How "Lightning + Wet = more damage" works

- `WetStatusEffect` applies the `[Wet]` tag to entities
- `LightningDamageEffect` (a `DamageEffect` asset) is configured:
  ```
  GrantedTags: [Lightning, Electrified]
  AmplifiedByTargetTags: [Wet] × 2.5
  ```
- When lightning hits a wet target: magnitude × 2.5 automatically
- No "wet + lightning" rule needed anywhere

---

## System Map

```
TagBootstrapper              ← initialize before anything else (ExecutionOrder -1000)
  └─ TagRegistry             ← singleton, resolves implication/suppression closures

TagContainer (MonoBehaviour) ← on every entity, zone, projectile, item
  └─ TagMask                 ← bitset, Resolve() expands implications + removes suppressions

EntityAttributes             ← health, effects, ticking
  └─ AttributeSet            ← health pool, armor, speed, event callbacks
  └─ TagContainer
  └─ AllegianceComponent

ZoneInstance                 ← spatial effect surface
  └─ TagContainer            ← grows when effects are infused
  └─ InfuseEffect()          ← the emergence entry point for zone interactions

InteractionEngine (Server)   ← evaluates InteractionRules, calls EffectComposer
  └─ InteractionRule[]       ← designer-authored, priority-sorted

EffectComposer (static)      ← merges effect tags, resolves composite, applies to target

DiscoverySystem (Server)     ← logs novel tag combinations, notifies players via ClientRpc

AllegianceMatrix (Server)    ← tracks teams, dynamic alliances
  ← NOTE: never blocks damage. Friendly fire is always on.
```

---

## Adding New Content (No Code Required)

### New damage type (e.g., Sonic)
1. Create `Tag_Sonic.asset` with `DisplayName: "Sonic"`
2. Create `Effect_SonicDamage.asset` (DamageEffect):
   - `GrantedTags: [Sonic, Vibrating]`
   - `AmplifiedByTargetTags: [Brittle] × 3.0` (crystals shatter)
   - `DiminishedByTargetTags: [Rubbery] × 0.1`
3. Assign to any item/ability. Done.

### New enemy type (e.g., Crystal Golem)
1. Create `EntityDef_CrystalGolem.asset`:
   - `BaseTags: [Construct, Corporeal, Crystalline, Brittle]`
2. Crystal Golem automatically:
   - Takes 3× damage from Sonic (existing Sonic effect sees [Brittle])
   - Can be shattered by explosion knockback (InteractionRule for [Brittle] + [Explosive])
   - Is Corporeal → gets caught by teleport traps

### New zone type (e.g., Acid Pool)
1. Create `Tag_Acid.asset`, `Tag_Corrosive.asset`
2. Create `Effect_AcidDamage.asset`:
   - `GrantedTags: [Acid, Corrosive]`
   - `AmplifiedByTargetTags: [Armored] × 1.5` (acid eats through armor)
3. Create `ZoneDef_AcidPool.asset`:
   - `InitialTags: [Liquid, Acid, Corrosive, Persistent]`
   - `InitialEffects: [AcidDamageEffect]`
4. Acid Pool + Smoke Grenade lands in it →
   Smoke zone infused with [Acid] → acid smoke cloud → everyone inside corrodes

---

## Crawler Stats & Progression

Faithful to the DCC books. Five core stats, DCC scale: 0 = unconscious, 4 = average, 10 = peak human.

| Stat | Drives | Derived From |
|------|--------|--------------|
| **Strength** | Melee damage multiplier (+10%/pt above 4), athletics | `MeleeDamageMultiplier` |
| **Constitution** | Max HP (50 + Con×25), health regen, potion cooldown | `MaxHealth`, `HealthRegenPerSecond`, `PotionCooldownDuration` |
| **Dexterity** | Move speed bonus (+3%/pt above 4), dodge, crafting | `MoveSpeed` |
| **Intelligence** | Max MP (= Int), mana regen, spell power (+8%/pt above 4) | `MaxMana`, `ManaRegenPerSecond`, `SpellPowerMultiplier` |
| **Charisma** | NPC manipulation, pet slots, bard magic, sponsor appeal | _(gameplay hooks)_ |

**Leveling**: 3 stat points per level (allocated in safe rooms).

### Mana System
- Max MP = Intelligence score (1 Int = 1 MP, faithful to books)
- Mana regen scales exponentially with Int (Int 3 ≈ 1 MP/hour, Int 17 ≈ 1 MP/min)
- Spells cost mana (configured per AbilityDefinition)
- Mana Potions restore MP instantly

### Skills vs Spells
- **Skills**: Nonmagical talents, no mana cost, level through use (cap 15, Primal race → 20)
- **Spells**: Magical, cost mana, learned from tomes, scale with Intelligence
- Both configured as AbilityDefinition assets; `IsSpell` flag distinguishes them

### Potion Cooldown
After drinking any potion, a cooldown starts (30s – Con×0.5s, min 5s).
Drinking another potion during cooldown inflicts Poisoned. Faithful to the books.

---

## Buffs, Debuffs & Status Effects

All status effects are `StatusEffect` assets with a `StatusType`:

| StatusType | Behavior | Examples |
|------------|----------|----------|
| `Debuff` | Tags + speed change only | Slowed, Frosted |
| `DamageOverTime` | Periodic damage ticks | Poisoned, Sepsis, Bleed, Burning |
| `HealOverTime` | Periodic healing ticks | Regeneration |
| `CrowdControl` | Prevents actions/movement | Stunned, Paralyzed, Fear, Peace-Bonded |

Key DCC distinction: **Poisoned** has `ImmuneToHealing: true` (needs antidote), while **Sepsis** can be healed with Heal spells/potions.

### Stat Modifier Buffs
`StatModifierEffect` temporarily modifies crawler stats. Used for buff potions, gear enchantments, curses.
Stacks additively. Cleanly removed on expiration.

---

## Multiplayer Authority Model

| System | Authority |
|--------|-----------|
| TagContainer mutations | Server only |
| EntityAttributes (health, effects) | Server only |
| ZoneInstance (ticking, infusion) | Server only |
| InteractionEngine | Server only |
| AllegianceMatrix | Server only |
| Player movement position | Client (ClientNetworkTransform) |
| Camera, local UI | Client only |
| Health bar display | Client reads NetworkVariable |

**Friendly fire**: Never blocked at engine level. Allegiance only affects XP attribution
and AI targeting priority.
