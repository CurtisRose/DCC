using UnityEngine;

namespace DCC.Core.Achievements
{
    /// <summary>
    /// Template-driven commentary lines for the System AI narrator.
    ///
    /// In the DCC books, the System AI (a.k.a. the Dungeon Master) is a petulant,
    /// snarky entity that narrates crawler progress with backhanded compliments,
    /// passive-aggressive warnings, and occasionally genuine (if reluctant) praise.
    ///
    /// Commentary is broadcast to all crawlers via floating text / chat.
    /// Templates support placeholders:
    ///   {player}  — crawler display name
    ///   {skill}   — skill name
    ///   {item}    — item/equipment name
    ///   {floor}   — current floor number
    ///   {mob}     — mob name
    ///   {stat}    — stat name
    ///   {value}   — numeric value (stat, level, count)
    ///   {class}   — class name
    ///   {race}    — race name
    ///
    /// Example templates from the books' tone:
    ///   "New achievement! {player} has earned 'Wow, You Actually Hit Something.'"
    ///   "Attention, crawlers. {player} has reached Floor {floor}. Try not to die immediately."
    ///   "{player} has maxed out {skill}. The System is... mildly impressed."
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/System AI Commentary", fileName = "Commentary_New")]
    public class SystemAICommentary : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }

        [field: SerializeField] public CommentaryTrigger Trigger { get; private set; }

        [field: SerializeField, TextArea(2, 5)]
        [Tooltip("Template string with {player}, {skill}, {item}, etc. placeholders.")]
        public string[] Templates { get; private set; }

        [field: SerializeField, Tooltip("If true, broadcast to ALL crawlers. If false, only the triggering player sees it.")]
        public bool IsBroadcast { get; private set; } = true;

        [field: SerializeField, Tooltip("Priority: higher priority lines replace lower ones when multiple trigger simultaneously.")]
        public int Priority { get; private set; }

        /// <summary>
        /// Pick a random template from the pool and fill placeholders.
        /// </summary>
        public string Format(CommentaryContext ctx)
        {
            if (Templates == null || Templates.Length == 0) return string.Empty;

            string template = Templates[Random.Range(0, Templates.Length)];
            return template
                .Replace("{player}", ctx.PlayerName ?? "Crawler")
                .Replace("{skill}", ctx.SkillName ?? "")
                .Replace("{item}", ctx.ItemName ?? "")
                .Replace("{floor}", ctx.FloorNumber.ToString())
                .Replace("{mob}", ctx.MobName ?? "")
                .Replace("{stat}", ctx.StatName ?? "")
                .Replace("{value}", ctx.Value.ToString())
                .Replace("{class}", ctx.ClassName ?? "")
                .Replace("{race}", ctx.RaceName ?? "");
        }
    }

    public enum CommentaryTrigger
    {
        /// <summary>A crawler earns any achievement.</summary>
        AchievementEarned,

        /// <summary>A crawler gets their first kill on a floor.</summary>
        FirstKill,

        /// <summary>A crawler dies.</summary>
        CrawlerDeath,

        /// <summary>A crawler reaches a new floor.</summary>
        FloorReached,

        /// <summary>A crawler levels up.</summary>
        LevelUp,

        /// <summary>A crawler maxes a skill.</summary>
        SkillMaxed,

        /// <summary>A new tag combination is discovered (global first).</summary>
        GlobalFirstDiscovery,

        /// <summary>A floor countdown warning (60s, 30s, 10s).</summary>
        CountdownWarning,

        /// <summary>The floor collapses.</summary>
        FloorCollapse,

        /// <summary>A crawler kills another crawler (PVP).</summary>
        PvpKill,

        /// <summary>A crawler opens a high-tier loot box.</summary>
        RareLootBoxOpened,

        /// <summary>Custom trigger fired by code.</summary>
        Custom
    }

    /// <summary>
    /// Data bag passed to SystemAICommentary.Format() for placeholder substitution.
    /// </summary>
    public struct CommentaryContext
    {
        public string PlayerName;
        public string SkillName;
        public string ItemName;
        public int FloorNumber;
        public string MobName;
        public string StatName;
        public int Value;
        public string ClassName;
        public string RaceName;
    }
}
