using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

namespace DCC.Core.Tags
{
    /// <summary>
    /// A fixed-size bitset representing a set of tags. Stored as a BitArray so
    /// union/intersection/except are fast bitwise operations.
    ///
    /// Supports up to 256 tags (32 bytes). Struct semantics — copy-on-write.
    /// Implements INetworkSerializable so it can cross the network via NGO.
    ///
    /// Key distinction:
    ///   - Raw mask: the tags explicitly set
    ///   - Resolved mask: raw + implied tags, with suppressed tags removed
    ///   Call Resolve() to get the effective mask the interaction engine uses.
    /// </summary>
    [Serializable]
    public struct TagMask : INetworkSerializable, IEquatable<TagMask>
    {
        public const int MaxTags = 256;
        private const int ByteCount = MaxTags / 8; // 32 bytes

        // Stored as a byte array for easy serialization.
        // null means all-zero (empty mask).
        private byte[] _bytes;

        public TagMask(int capacity = MaxTags)
        {
            _bytes = new byte[ByteCount];
        }

        private void EnsureAllocated()
        {
            if (_bytes == null) _bytes = new byte[ByteCount];
        }

        // ── Mutation (in-place, server-side only) ──────────────────────────

        public void Set(int bitIndex)
        {
            EnsureAllocated();
            _bytes[bitIndex >> 3] |= (byte)(1 << (bitIndex & 7));
        }

        public void Clear(int bitIndex)
        {
            EnsureAllocated();
            _bytes[bitIndex >> 3] &= (byte)~(1 << (bitIndex & 7));
        }

        // ── Query ──────────────────────────────────────────────────────────

        public bool HasTag(TagDefinition tag)
        {
            if (tag == null || tag.RuntimeId < 0) return false;
            return HasBit(tag.RuntimeId);
        }

        public bool HasBit(int bitIndex)
        {
            if (_bytes == null) return false;
            return (_bytes[bitIndex >> 3] & (1 << (bitIndex & 7))) != 0;
        }

        public bool HasAll(TagMask required)
        {
            if (required._bytes == null) return true;
            EnsureAllocated();
            for (int i = 0; i < ByteCount; i++)
            {
                if ((_bytes[i] & required._bytes[i]) != required._bytes[i])
                    return false;
            }
            return true;
        }

        public bool HasAny(TagMask mask)
        {
            if (mask._bytes == null) return false;
            if (_bytes == null) return false;
            for (int i = 0; i < ByteCount; i++)
            {
                if ((_bytes[i] & mask._bytes[i]) != 0) return true;
            }
            return false;
        }

        public bool IsEmpty()
        {
            if (_bytes == null) return true;
            for (int i = 0; i < ByteCount; i++)
                if (_bytes[i] != 0) return false;
            return true;
        }

        // ── Non-mutating set operations (return new masks) ─────────────────

        public TagMask Union(TagMask other)
        {
            var result = new TagMask();
            result.EnsureAllocated();
            var a = _bytes;
            var b = other._bytes;
            for (int i = 0; i < ByteCount; i++)
            {
                result._bytes[i] = (byte)(
                    (a != null ? a[i] : 0) | (b != null ? b[i] : 0));
            }
            return result;
        }

        public TagMask Intersect(TagMask other)
        {
            if (_bytes == null || other._bytes == null) return new TagMask();
            var result = new TagMask();
            result.EnsureAllocated();
            for (int i = 0; i < ByteCount; i++)
                result._bytes[i] = (byte)(_bytes[i] & other._bytes[i]);
            return result;
        }

        public TagMask Except(TagMask other)
        {
            var result = new TagMask();
            result.EnsureAllocated();
            for (int i = 0; i < ByteCount; i++)
            {
                int a = _bytes != null ? _bytes[i] : 0;
                int b = other._bytes != null ? other._bytes[i] : 0;
                result._bytes[i] = (byte)(a & ~b);
            }
            return result;
        }

        // ── Resolution ─────────────────────────────────────────────────────

        /// <summary>
        /// Computes the effective tag set:
        ///   1. Start with this mask
        ///   2. Add all implied tags (transitive, from TagRegistry)
        ///   3. Remove all suppressed tags
        /// This is what the interaction engine always operates on.
        /// </summary>
        public TagMask Resolve()
        {
            var registry = TagRegistry.Instance;
            if (registry == null) return this;

            var resolved = new TagMask();
            resolved.EnsureAllocated();
            if (_bytes != null)
                Array.Copy(_bytes, resolved._bytes, ByteCount);

            var suppressed = new TagMask();

            // First pass: collect all implied tags and all suppressions.
            for (int i = 0; i < MaxTags; i++)
            {
                if (!HasBit(i)) continue;
                var tag = registry.GetById(i);
                if (tag == null) continue;

                resolved = resolved.Union(registry.GetImpliedMask(tag));
                suppressed = suppressed.Union(registry.GetSuppressedMask(tag));
            }

            // Second pass: remove suppressed tags.
            return resolved.Except(suppressed);
        }

        // ── Enumeration ────────────────────────────────────────────────────

        public IEnumerable<TagDefinition> GetSetTags()
        {
            var registry = TagRegistry.Instance;
            if (_bytes == null || registry == null) yield break;
            for (int i = 0; i < MaxTags; i++)
            {
                if (HasBit(i))
                {
                    var tag = registry.GetById(i);
                    if (tag != null) yield return tag;
                }
            }
        }

        // ── Equality & hashing ─────────────────────────────────────────────

        public bool Equals(TagMask other)
        {
            for (int i = 0; i < ByteCount; i++)
            {
                int a = _bytes != null ? _bytes[i] : 0;
                int b = other._bytes != null ? other._bytes[i] : 0;
                if (a != b) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is TagMask m && Equals(m);

        public override int GetHashCode()
        {
            if (_bytes == null) return 0;
            int hash = 17;
            foreach (var b in _bytes) hash = hash * 31 + b;
            return hash;
        }

        // ── Network Serialization ──────────────────────────────────────────

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                var bytes = _bytes ?? new byte[ByteCount];
                for (int i = 0; i < ByteCount; i++)
                    serializer.SerializeValue(ref bytes[i]);
            }
            else
            {
                EnsureAllocated();
                for (int i = 0; i < ByteCount; i++)
                    serializer.SerializeValue(ref _bytes[i]);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        public static TagMask FromTags(params TagDefinition[] tags)
        {
            var mask = new TagMask();
            foreach (var t in tags)
                if (t != null && t.RuntimeId >= 0)
                    mask.Set(t.RuntimeId);
            return mask;
        }

        public override string ToString()
        {
            var tags = new List<string>();
            foreach (var t in GetSetTags()) tags.Add(t.DisplayName);
            return tags.Count == 0 ? "[empty]" : $"[{string.Join(", ", tags)}]";
        }
    }
}
