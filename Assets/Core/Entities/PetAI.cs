using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Allegiance;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Server-authoritative AI state machine for pets.
    /// Handles Follow, Attack, Flee, and Guard states.
    ///
    /// In the DCC books, pet AI varies by intelligence:
    ///   - High Int pets (Donut, Int 6) make tactical decisions, use abilities wisely.
    ///   - Low Int pets (Mongo, Int 2) charge at the nearest enemy and bite.
    ///   - All pets have a leash range — they return to owner if too far.
    ///
    /// PetAI uses AbilityCaster for combat abilities (the same system players use).
    /// Targeting priority: enemies attacking owner > nearest enemy > follow owner.
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    [RequireComponent(typeof(TagContainer))]
    public class PetAI : NetworkBehaviour
    {
        private EntityAttributes _attrs;
        private TagContainer _tags;
        private AllegianceComponent _allegiance;
        private AbilityCaster _caster;

        // Set by PetManager on spawn.
        private PetManager _owner;
        private int _rosterIndex;
        private PetDefinition _petDef;

        // AI state.
        private PetAIState _state = PetAIState.Follow;
        private PetAIBehavior _behavior = PetAIBehavior.Aggressive;

        // Targeting.
        private EntityAttributes _currentTarget;
        private float _retargetTimer;
        private const float RetargetInterval = 0.5f;

        // Movement.
        private Vector3 _moveTarget;
        private float _moveSpeed;

        // Combat.
        private float _attackTimer;
        private const float AttackInterval = 1.5f;

        private void Awake()
        {
            _attrs = GetComponent<EntityAttributes>();
            _tags = GetComponent<TagContainer>();
            _allegiance = GetComponent<AllegianceComponent>();
            _caster = GetComponent<AbilityCaster>();
        }

        // ── Initialization (called by PetManager) ──────────────────────────

        public void Initialize(PetManager owner, int rosterIndex, PetDefinition petDef)
        {
            _owner = owner;
            _rosterIndex = rosterIndex;
            _petDef = petDef;
            _behavior = petDef.DefaultBehavior;
            _moveSpeed = petDef.EntityDefinition != null
                ? petDef.EntityDefinition.BaseMoveSpeed
                : 5f;

            _attrs.OnDied += HandleDeath;
        }

        public void SetBehavior(PetAIBehavior behavior)
        {
            _behavior = behavior;
            if (_behavior == PetAIBehavior.Passive)
            {
                _currentTarget = null;
                _state = PetAIState.Follow;
            }
        }

        // ── Update loop (server only) ──────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;
            if (!_attrs.IsAlive) return;
            if (_owner == null) return;

            float dt = Time.deltaTime;
            _retargetTimer -= dt;
            _attackTimer -= dt;

            switch (_state)
            {
                case PetAIState.Follow:
                    UpdateFollow(dt);
                    break;
                case PetAIState.Attack:
                    UpdateAttack(dt);
                    break;
                case PetAIState.ReturnToOwner:
                    UpdateReturnToOwner(dt);
                    break;
                case PetAIState.Guard:
                    UpdateGuard(dt);
                    break;
            }
        }

        // ── Follow state ───────────────────────────────────────────────────

        private void UpdateFollow(float dt)
        {
            Transform ownerTransform = _owner.transform;
            float distToOwner = Vector3.Distance(transform.position, ownerTransform.position);
            float followDist = _petDef != null ? _petDef.FollowDistance : 4f;

            // Move toward owner if too far.
            if (distToOwner > followDist)
            {
                Vector3 dir = (ownerTransform.position - transform.position).normalized;
                transform.position += dir * _moveSpeed * dt;
            }

            // Look for targets (if not passive).
            if (_behavior != PetAIBehavior.Passive && _retargetTimer <= 0f)
            {
                _retargetTimer = RetargetInterval;
                TryAcquireTarget();
            }
        }

        // ── Attack state ───────────────────────────────────────────────────

        private void UpdateAttack(float dt)
        {
            // Validate target.
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                _currentTarget = null;
                _state = PetAIState.Follow;
                return;
            }

            // Leash check: if too far from owner, return.
            float leash = _petDef != null ? _petDef.LeashRange : 20f;
            if (_owner != null &&
                Vector3.Distance(transform.position, _owner.transform.position) > leash)
            {
                _currentTarget = null;
                _state = PetAIState.ReturnToOwner;
                return;
            }

            // Move toward target.
            float distToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
            float attackRange = 2f; // Melee range default.

            if (distToTarget > attackRange)
            {
                Vector3 dir = (_currentTarget.transform.position - transform.position).normalized;
                transform.position += dir * _moveSpeed * dt;
            }
            else if (_attackTimer <= 0f)
            {
                // Attack!
                PerformAttack();
                _attackTimer = AttackInterval;
            }

            // Periodically retarget (pick closer/more dangerous enemy).
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = RetargetInterval;
                TryAcquireTarget();
            }
        }

        // ── Return to owner state ──────────────────────────────────────────

        private void UpdateReturnToOwner(float dt)
        {
            if (_owner == null) return;

            float dist = Vector3.Distance(transform.position, _owner.transform.position);
            float followDist = _petDef != null ? _petDef.FollowDistance : 4f;

            if (dist > followDist)
            {
                Vector3 dir = (_owner.transform.position - transform.position).normalized;
                transform.position += dir * _moveSpeed * 1.5f * dt; // Sprint back.
            }
            else
            {
                _state = PetAIState.Follow;
            }
        }

        // ── Guard state ────────────────────────────────────────────────────

        private Vector3 _guardPosition;

        private void UpdateGuard(float dt)
        {
            // Stay near guard position.
            float dist = Vector3.Distance(transform.position, _guardPosition);
            if (dist > 1f)
            {
                Vector3 dir = (_guardPosition - transform.position).normalized;
                transform.position += dir * _moveSpeed * dt;
            }

            // Still look for targets within aggro range.
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = RetargetInterval;
                TryAcquireTarget();
            }

            // If target found in guard mode, attack but return to guard pos after.
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                float targetDist = Vector3.Distance(transform.position, _currentTarget.transform.position);
                float aggroRange = _petDef != null ? _petDef.AggroRange : 10f;
                if (targetDist <= aggroRange)
                    _state = PetAIState.Attack;
                else
                    _currentTarget = null;
            }
        }

        /// <summary>Set guard position and switch to Guard behavior.</summary>
        public void SetGuardPosition(Vector3 position)
        {
            _guardPosition = position;
            _behavior = PetAIBehavior.Guard;
            _state = PetAIState.Guard;
        }

        // ── Targeting ──────────────────────────────────────────────────────

        private void TryAcquireTarget()
        {
            float aggroRange = _petDef != null ? _petDef.AggroRange : 10f;
            var colliders = Physics.OverlapSphere(transform.position, aggroRange);

            EntityAttributes bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var col in colliders)
            {
                var candidate = col.GetComponentInParent<EntityAttributes>();
                if (candidate == null || !candidate.IsAlive) continue;
                if (candidate == _attrs) continue; // Don't target self.

                // Skip allies.
                var candidateAllegiance = candidate.GetComponent<AllegianceComponent>();
                if (candidateAllegiance != null && _allegiance != null &&
                    _allegiance.IsAlliedWith(candidateAllegiance))
                    continue;

                // Defensive: only attack enemies that are attacking the owner.
                if (_behavior == PetAIBehavior.Defensive)
                {
                    // Simple heuristic: target must be close to owner.
                    if (_owner != null)
                    {
                        float distToOwner = Vector3.Distance(
                            candidate.transform.position, _owner.transform.position);
                        if (distToOwner > aggroRange * 0.5f) continue;
                    }
                }

                // Score: prioritize enemies closer to owner, then closer to pet.
                float dist = Vector3.Distance(transform.position, candidate.transform.position);
                float score = -dist;

                // Bonus for enemies near the owner (protect the master).
                if (_owner != null)
                {
                    float distToOwner = Vector3.Distance(
                        candidate.transform.position, _owner.transform.position);
                    score += (aggroRange - distToOwner) * 2f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                _currentTarget = bestTarget;
                if (_state == PetAIState.Follow || _state == PetAIState.Guard)
                    _state = PetAIState.Attack;
            }
        }

        // ── Combat ─────────────────────────────────────────────────────────

        private void PerformAttack()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive) return;

            // Use AbilityCaster if available (ability slot 0 is the primary attack).
            if (_caster != null)
            {
                _caster.CastAbility(0, _currentTarget.transform.position, 0);
                return;
            }

            // Fallback: simple direct damage using entity's Strength.
            // Without a configured ability, deal flat melee damage.
            float damage = _attrs.AttributeSet.MeleeDamageMultiplier * 10f;
            var ctx = Effects.EffectContext.Environment(transform.position);
            _currentTarget.AttributeSet.ApplyDamage(damage, ctx);
        }

        // ── Death ──────────────────────────────────────────────────────────

        private void HandleDeath()
        {
            _currentTarget = null;
            _state = PetAIState.Follow;

            // Notify PetManager so it can update roster and despawn.
            if (_owner != null)
                _owner.NotifyPetDeath(_rosterIndex);
        }

        public override void OnDestroy()
        {
            if (_attrs != null)
                _attrs.OnDied -= HandleDeath;
            base.OnDestroy();
        }
    }

    public enum PetAIState
    {
        Follow,
        Attack,
        ReturnToOwner,
        Guard
    }
}
