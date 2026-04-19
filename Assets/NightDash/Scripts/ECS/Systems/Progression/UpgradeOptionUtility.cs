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
            GameLoopState loop,
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

            int preferredOffset = candidatePool.Count > 0
                ? (loop.Level + loop.PendingLevelUps + (preferFreshOptions ? previousOptions.Count + 1 : 0)) % candidatePool.Count
                : 0;

            if (preferFreshOptions)
            {
                AddPreferredOptions(ref options, candidatePool, previousOptions, preferredOffset, includePrevious: false);
            }

            AddPreferredOptions(ref options, candidatePool, previousOptions, preferredOffset, includePrevious: true);
            EnsureFreshUnlockPresence(ref options, candidatePool, previousOptions, preferredOffset, UpgradeKind.Weapon);
            EnsureFreshUnlockPresence(ref options, candidatePool, previousOptions, preferredOffset, UpgradeKind.Passive);
            EnsureOptionKindPresence(ref options, candidatePool, previousOptions, preferredOffset, UpgradeKind.Weapon);
            EnsureOptionKindPresence(ref options, candidatePool, previousOptions, preferredOffset, UpgradeKind.Passive);
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
            if (option.Kind == UpgradeKind.Weapon)
            {
                for (int i = 0; i < ownedWeapons.Length; i++)
                {
                    if (!ownedWeapons[i].Id.Equals(option.Id))
                    {
                        continue;
                    }

                    OwnedWeaponElement upgraded = ownedWeapons[i];
                    upgraded.Level = math.min(upgraded.MaxLevel, upgraded.Level + 1);
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
                    Level = 1,
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
                upgraded.Level = math.min(upgraded.MaxLevel, upgraded.Level + 1);
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
                Level = 1,
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

        internal static void AddPreferredOptions(
            ref DynamicBuffer<UpgradeOptionElement> options,
            List<UpgradeOptionElement> candidates,
            List<UpgradeOptionElement> previousOptions,
            int offset,
            bool includePrevious)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < candidates.Count && options.Length < 3; i++)
            {
                UpgradeOptionElement candidate = candidates[(offset + i) % candidates.Count];
                bool existedPreviously = ContainsOption(previousOptions, candidate);
                if (!includePrevious && existedPreviously && previousOptions.Count < candidates.Count)
                {
                    continue;
                }

                if (!ContainsOption(options, candidate.Id))
                {
                    options.Add(candidate);
                }
            }
        }

        internal static void EnsureOptionKindPresence(
            ref DynamicBuffer<UpgradeOptionElement> options,
            List<UpgradeOptionElement> candidates,
            List<UpgradeOptionElement> previousOptions,
            int offset,
            UpgradeKind requiredKind)
        {
            if (HasOptionKind(options, requiredKind))
            {
                return;
            }

            int replacementIndex = FindCandidateIndex(candidates, previousOptions, offset, requiredKind, includePrevious: false);
            if (replacementIndex < 0)
            {
                replacementIndex = FindCandidateIndex(candidates, previousOptions, offset, requiredKind, includePrevious: true);
            }

            if (replacementIndex < 0)
            {
                return;
            }

            UpgradeOptionElement replacement = candidates[replacementIndex];
            if (ContainsOption(options, replacement))
            {
                return;
            }

            if (options.Length < 3)
            {
                options.Add(replacement);
                return;
            }

            for (int i = options.Length - 1; i >= 0; i--)
            {
                if (options[i].Kind != requiredKind)
                {
                    options[i] = replacement;
                    return;
                }
            }
        }

        internal static void EnsureFreshUnlockPresence(
            ref DynamicBuffer<UpgradeOptionElement> options,
            List<UpgradeOptionElement> candidates,
            List<UpgradeOptionElement> previousOptions,
            int offset,
            UpgradeKind requiredKind)
        {
            if (HasFreshUnlock(options, requiredKind))
            {
                return;
            }

            int replacementIndex = FindCandidateIndex(
                candidates,
                previousOptions,
                offset,
                requiredKind,
                includePrevious: false,
                requireFreshUnlock: true);

            if (replacementIndex < 0)
            {
                replacementIndex = FindCandidateIndex(
                    candidates,
                    previousOptions,
                    offset,
                    requiredKind,
                    includePrevious: true,
                    requireFreshUnlock: true);
            }

            if (replacementIndex < 0)
            {
                return;
            }

            UpgradeOptionElement replacement = candidates[replacementIndex];
            if (ContainsOption(options, replacement))
            {
                return;
            }

            if (TryReplaceOption(ref options, replacement, preferReplaceFreshUnlock: false))
            {
                return;
            }

            if (options.Length < 3)
            {
                options.Add(replacement);
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

        internal static bool TryReplaceOption(
            ref DynamicBuffer<UpgradeOptionElement> options,
            UpgradeOptionElement replacement,
            bool preferReplaceFreshUnlock)
        {
            for (int i = options.Length - 1; i >= 0; i--)
            {
                UpgradeOptionElement existing = options[i];
                bool existingIsFreshUnlock = existing.CurrentLevel == 0;
                if (!preferReplaceFreshUnlock && existingIsFreshUnlock)
                {
                    continue;
                }

                if (existing.Kind == replacement.Kind && existingIsFreshUnlock == (replacement.CurrentLevel == 0))
                {
                    continue;
                }

                options[i] = replacement;
                return true;
            }

            return false;
        }
    }
}
