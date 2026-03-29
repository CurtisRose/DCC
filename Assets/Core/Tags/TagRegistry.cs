using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCC.Core.Tags
{
    /// <summary>
    /// Singleton registry initialized once at runtime. Assigns RuntimeIds to all
    /// TagDefinitions and precomputes the implication/suppression closure for each tag
    /// so that TagMask.Resolve() is O(n) rather than recursive.
    ///
    /// Load all TagDefinitions via Resources.LoadAll or Addressables before calling Initialize.
    /// </summary>
    public class TagRegistry
    {
        public static TagRegistry Instance { get; private set; }

        private TagDefinition[] _allTags;
        // Precomputed: for each tag index, the full set of tags it implies (transitive).
        private TagMask[] _impliedClosure;
        // Precomputed: for each tag index, the full set of tags it suppresses.
        private TagMask[] _suppressedClosure;

        private readonly Dictionary<string, TagDefinition> _byName = new();

        public static void Initialize(TagDefinition[] allTags)
        {
            var registry = new TagRegistry();
            registry.Build(allTags);
            Instance = registry;
        }

        private void Build(TagDefinition[] allTags)
        {
            _allTags = allTags;

            // Assign runtime IDs.
            for (int i = 0; i < allTags.Length; i++)
            {
                allTags[i].RuntimeId = i;
                _byName[allTags[i].name] = allTags[i];
                _byName[allTags[i].DisplayName] = allTags[i];
            }

            _impliedClosure = new TagMask[allTags.Length];
            _suppressedClosure = new TagMask[allTags.Length];

            // Build transitive implication closure using BFS per tag.
            for (int i = 0; i < allTags.Length; i++)
            {
                var visited = new HashSet<int>();
                var queue = new Queue<TagDefinition>();
                queue.Enqueue(allTags[i]);

                var implied = new TagMask(allTags.Length);
                var suppressed = new TagMask(allTags.Length);

                while (queue.Count > 0)
                {
                    var tag = queue.Dequeue();
                    if (!visited.Add(tag.RuntimeId)) continue;

                    if (tag.ImpliedTags != null)
                    {
                        foreach (var imp in tag.ImpliedTags)
                        {
                            if (imp == null) continue;
                            implied.Set(imp.RuntimeId);
                            queue.Enqueue(imp);
                        }
                    }

                    if (tag.SuppressedTags != null)
                    {
                        foreach (var sup in tag.SuppressedTags)
                        {
                            if (sup == null) continue;
                            suppressed.Set(sup.RuntimeId);
                        }
                    }
                }

                _impliedClosure[i] = implied;
                _suppressedClosure[i] = suppressed;
            }

            Debug.Log($"[TagRegistry] Initialized with {allTags.Length} tags.");
        }

        public TagDefinition GetByName(string nameOrDisplayName)
        {
            _byName.TryGetValue(nameOrDisplayName, out var tag);
            return tag;
        }

        public TagDefinition GetById(int id)
        {
            if (id < 0 || id >= _allTags.Length) return null;
            return _allTags[id];
        }

        public int TagCount => _allTags?.Length ?? 0;

        /// <summary>Returns the precomputed implication closure mask for a tag.</summary>
        public TagMask GetImpliedMask(TagDefinition tag)
        {
            if (tag.RuntimeId < 0) throw new InvalidOperationException($"Tag {tag.name} not registered.");
            return _impliedClosure[tag.RuntimeId];
        }

        /// <summary>Returns the precomputed suppression mask for a tag.</summary>
        public TagMask GetSuppressedMask(TagDefinition tag)
        {
            if (tag.RuntimeId < 0) throw new InvalidOperationException($"Tag {tag.name} not registered.");
            return _suppressedClosure[tag.RuntimeId];
        }

        public IReadOnlyList<TagDefinition> AllTags => _allTags;
    }
}
