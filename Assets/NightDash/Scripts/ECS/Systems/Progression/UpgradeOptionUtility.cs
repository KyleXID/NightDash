using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// NOTE: InternalsVisibleTo is declared once in Assets/NightDash/Scripts/AssemblyInfo.cs
// (S1-03). Do not duplicate it here — C# rejects the same assembly-name attribute twice.

namespace NightDash.ECS.Systems.Progression
{
    /// <summary>
    /// Pure-static helpers extracted from the monolithic ProgressionSystem.
    /// Kept as managed (List&lt;T&gt;) for parity with the original implementation.
    /// Burst / NativeList migration is tracked as Open Question 3 in S1-05 RFC.
    /// </summary>
    internal static class UpgradeOptionUtility
    {
        // Relative likelihood of each card kind being drawn. Passives are
        // intentionally more likely than weapons (designer request 2026-06-15).
        // The RNG itself is owned/seeded by the caller (UpgradeOptionGeneratorSystem)
        // and passed in by ref, so card draws and rarity rolls are deterministic
        // and unit-testable while still varying per run at runtime.
        internal const float WeaponDrawWeight  = 1.0f;
        internal const float PassiveDrawWeight = 2.0f;

        // -------------------------------------------------------------------------
        // Public entry points called by the split ISystem types
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds up to 3 upgrade options into <paramref name="options"/>.
        /// Corresponds to original GenerateOptions (lines 151-272).
        /// </summary>
        internal static void BuildOptions(
            DataRegistry registry,
            ref DynamicBuffer<UpgradeOptionElement> options,
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            DynamicBuffer<OwnedPassiveElement> ownedPassives,
            DynamicBuffer<AvailableWeaponElement> availableWeapons,
            DynamicBuffer<AvailablePassiveElement> availablePassives,
            PlayerProgressionState progression,
            ref Unity.Mathematics.Random rng,
            bool preferFreshOptions)
        {
            var previousOptions = new List<UpgradeOptionElement>(options.Length);
            for (int i = 0; i < options.Length; i++)
            {
                previousOptions.Add(options[i]);
            }

            options.Clear();

            var candidatePool = new List<UpgradeOptionElement>(16);

            // Owned weapons that can still level up
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement owned = ownedWeapons[i];
                if (owned.Level >= owned.MaxLevel)
                {
                    continue;
                }

                AddCandidate(candidatePool, new UpgradeOptionElement
                {
                    Kind = UpgradeKind.Weapon,
                    Id = owned.Id,
                    CurrentLevel = owned.Level,
                    NextLevel = owned.Level + 1,
                    MaxLevel = owned.MaxLevel
                });
            }

            // New weapon unlocks (within slot limit)
            if (ownedWeapons.Length < progression.WeaponSlotLimit)
            {
                for (int i = 0; i < availableWeapons.Length; i++)
                {
                    if (ContainsWeapon(ownedWeapons, availableWeapons[i].Id))
                    {
                        continue;
                    }

                    if (!registry.TryGetWeapon(availableWeapons[i].Id.ToString(), out WeaponData weapon) || weapon == null)
                    {
                        continue;
                    }

                    AddCandidate(candidatePool, new UpgradeOptionElement
                    {
                        Kind = UpgradeKind.Weapon,
                        Id = availableWeapons[i].Id,
                        CurrentLevel = 0,
                        NextLevel = 1,
                        MaxLevel = math.max(1, weapon.maxLevel)
                    });
                }
            }

            // Owned passives that can still level up
            for (int i = 0; i < ownedPassives.Length; i++)
            {
                OwnedPassiveElement owned = ownedPassives[i];
                if (owned.Level >= owned.MaxLevel)
                {
                    continue;
                }

                AddCandidate(candidatePool, new UpgradeOptionElement
                {
                    Kind = UpgradeKind.Passive,
                    Id = owned.Id,
                    CurrentLevel = owned.Level,
                    NextLevel = owned.Level + 1,
                    MaxLevel = owned.MaxLevel
                });
            }

            // New passive unlocks (within slot limit)
            if (ownedPassives.Length < progression.PassiveSlotLimit)
            {
                for (int i = 0; i < availablePassives.Length; i++)
                {
                    if (ContainsPassive(ownedPassives, availablePassives[i].Id))
                    {
                        continue;
                    }

                    if (!registry.TryGetPassive(availablePassives[i].Id.ToString(), out PassiveData passive) || passive == null)
                    {
                        continue;
                    }

                    AddCandidate(candidatePool, new UpgradeOptionElement
                    {
                        Kind = UpgradeKind.Passive,
                        Id = availablePassives[i].Id,
                        CurrentLevel = 0,
                        NextLevel = 1,
                        MaxLevel = math.max(1, passive.maxLevel)
                    });
                }
            }

            // Weighted random draw: pick up to 3 distinct cards from the pool.
            // Passives carry a higher weight (PassiveDrawWeight) than weapons,
            // so passive cards appear more often than weapon cards.
            DrawWeightedOptions(ref options, candidatePool, previousOptions, ref rng, preferFreshOptions);

            // Roll a rarity per offered card (70% Common / 25% Rare / 5% Legendary).
            // Higher rarity grants bonus levels when the card is picked (ApplySelection).
            for (int i = 0; i < options.Length; i++)
            {
                UpgradeOptionElement o = options[i];
                float roll = rng.NextFloat();
                o.Rarity = roll < 0.70f ? (byte)0 : (roll < 0.95f ? (byte)1 : (byte)2);
                options[i] = o;
            }
        }

        /// <summary>
        /// Applies the chosen upgrade option to the player's owned weapon/passive buffers.
        /// Corresponds to original ApplySelection (lines 274-345).
        /// </summary>
        internal static void ApplySelection(
            DataRegistry registry,
            UpgradeOptionElement option,
            PlayerProgressionState progression,
            ref DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            ref DynamicBuffer<OwnedPassiveElement> ownedPassives)
        {
            // Higher-rarity cards grant bonus levels on top of the normal +1.
            int bonus = option.Rarity >= 2 ? 2 : (option.Rarity == 1 ? 1 : 0);

            if (option.Kind == UpgradeKind.Weapon)
            {
                for (int i = 0; i < ownedWeapons.Length; i++)
                {
                    if (!ownedWeapons[i].Id.Equals(option.Id))
                    {
                        continue;
                    }

                    OwnedWeaponElement upgraded = ownedWeapons[i];
                    upgraded.Level = math.min(upgraded.MaxLevel, upgraded.Level + 1 + bonus);
                    ownedWeapons[i] = upgraded;
                    return;
                }

                if (ownedWeapons.Length >= progression.WeaponSlotLimit)
                {
                    return;
                }

                ownedWeapons.Add(new OwnedWeaponElement
                {
                    Id = option.Id,
                    Level = math.min(option.MaxLevel, 1 + bonus),
                    MaxLevel = option.MaxLevel,
                    CooldownRemaining = 0f
                });
                return;
            }

            if (option.Kind != UpgradeKind.Passive)
            {
                return;
            }

            for (int i = 0; i < ownedPassives.Length; i++)
            {
                if (!ownedPassives[i].Id.Equals(option.Id))
                {
                    continue;
                }

                OwnedPassiveElement upgraded = ownedPassives[i];
                upgraded.Level = math.min(upgraded.MaxLevel, upgraded.Level + 1 + bonus);
                ownedPassives[i] = upgraded;
                return;
            }

            if (ownedPassives.Length >= progression.PassiveSlotLimit)
            {
                return;
            }

            if (!registry.TryGetPassive(option.Id.ToString(), out PassiveData passive) || passive == null)
            {
                return;
            }

            ownedPassives.Add(new OwnedPassiveElement
            {
                Id = option.Id,
                Level = math.min(math.max(1, passive.maxLevel), 1 + bonus),
                MaxLevel = math.max(1, passive.maxLevel)
            });
        }

        // -------------------------------------------------------------------------
        // Internal helpers
        // -------------------------------------------------------------------------

        internal static bool ContainsWeapon(DynamicBuffer<OwnedWeaponElement> ownedWeapons, FixedString64Bytes id)
        {
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                if (ownedWeapons[i].Id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsPassive(DynamicBuffer<OwnedPassiveElement> ownedPassives, FixedString64Bytes id)
        {
            for (int i = 0; i < ownedPassives.Length; i++)
            {
                if (ownedPassives[i].Id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsOption(DynamicBuffer<UpgradeOptionElement> options, FixedString64Bytes id)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsOption(DynamicBuffer<UpgradeOptionElement> options, UpgradeOptionElement candidate)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == candidate.Kind && options[i].Id.Equals(candidate.Id))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsOption(List<UpgradeOptionElement> options, UpgradeOptionElement candidate)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Kind == candidate.Kind && options[i].Id.Equals(candidate.Id))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void AddCandidate(List<UpgradeOptionElement> candidates, UpgradeOptionElement candidate)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == candidate.Kind && candidates[i].Id.Equals(candidate.Id))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        internal static float DrawWeight(UpgradeKind kind)
            => kind == UpgradeKind.Passive ? PassiveDrawWeight : WeaponDrawWeight;

        /// <summary>
        /// Picks up to 3 distinct cards from <paramref name="candidates"/> via a
        /// weighted random draw (passives weighted higher than weapons). On a
        /// reroll it first tries to exclude the previously-shown cards. The caller
        /// owns/advances <paramref name="rng"/>, so successive level-ups and runs
        /// surface a genuinely random set — no more level-indexed fixed ordering.
        /// </summary>
        internal static void DrawWeightedOptions(
            ref DynamicBuffer<UpgradeOptionElement> options,
            List<UpgradeOptionElement> candidates,
            List<UpgradeOptionElement> previousOptions,
            ref Unity.Mathematics.Random rng,
            bool preferFreshOptions)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            // Working pool we draw from without replacement. On reroll, prefer
            // cards that weren't shown last time; if that leaves too few to fill
            // the slots, fall back to the full candidate set.
            var pool = new List<UpgradeOptionElement>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (preferFreshOptions && ContainsOption(previousOptions, candidates[i]))
                {
                    continue;
                }

                pool.Add(candidates[i]);
            }

            if (pool.Count < math.min(3, candidates.Count))
            {
                pool.Clear();
                pool.AddRange(candidates);
            }

            int target = math.min(3, pool.Count);
            while (options.Length < target && pool.Count > 0)
            {
                float total = 0f;
                for (int i = 0; i < pool.Count; i++)
                {
                    total += DrawWeight(pool[i].Kind);
                }

                float r = rng.NextFloat(0f, total);
                int picked = pool.Count - 1;
                float acc = 0f;
                for (int i = 0; i < pool.Count; i++)
                {
                    acc += DrawWeight(pool[i].Kind);
                    if (r < acc)
                    {
                        picked = i;
                        break;
                    }
                }

                options.Add(pool[picked]);
                pool.RemoveAt(picked);
            }
        }

        internal static int FindCandidateIndex(
            List<UpgradeOptionElement> candidates,
            List<UpgradeOptionElement> previousOptions,
            int offset,
            UpgradeKind requiredKind,
            bool includePrevious,
            bool requireFreshUnlock = false)
        {
            if (candidates.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                int index = (offset + i) % candidates.Count;
                UpgradeOptionElement candidate = candidates[index];
                if (candidate.Kind != requiredKind)
                {
                    continue;
                }

                if (requireFreshUnlock && candidate.CurrentLevel != 0)
                {
                    continue;
                }

                bool existedPreviously = ContainsOption(previousOptions, candidate);
                if (!includePrevious && existedPreviously && previousOptions.Count < candidates.Count)
                {
                    continue;
                }

                return index;
            }

            return -1;
        }

        internal static bool HasFreshUnlock(DynamicBuffer<UpgradeOptionElement> options, UpgradeKind kind)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == kind && options[i].CurrentLevel == 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasOptionKind(DynamicBuffer<UpgradeOptionElement> options, UpgradeKind kind)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
