using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct ProgressionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<PlayerProgressionState>();
            state.RequireForUpdate<UpgradeSelectionRequest>();
            state.RequireForUpdate<OwnedWeaponElement>();
            state.RequireForUpdate<OwnedPassiveElement>();
            state.RequireForUpdate<UpgradeOptionElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var registry = DataRegistry.Instance;
            if (registry?.Catalog == null)
            {
                return;
            }

            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            RefRW<PlayerProgressionState> progression = SystemAPI.GetSingletonRW<PlayerProgressionState>();
            RefRW<UpgradeSelectionRequest> request = SystemAPI.GetSingletonRW<UpgradeSelectionRequest>();
            DynamicBuffer<UpgradeOptionElement> options = SystemAPI.GetSingletonBuffer<UpgradeOptionElement>();
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            DynamicBuffer<AvailableWeaponElement> availableWeapons = SystemAPI.GetSingletonBuffer<AvailableWeaponElement>();
            DynamicBuffer<AvailablePassiveElement> availablePassives = SystemAPI.GetSingletonBuffer<AvailablePassiveElement>();

            string classId = SystemAPI.GetSingleton<RunSelection>().ClassId.ToString();

            if (loop.ValueRO.PendingLevelUps > 0 && loop.ValueRO.Status == RunStatus.Playing)
            {
                loop.ValueRW.Status = RunStatus.LevelUpSelection;
            }

            if (loop.ValueRO.Status != RunStatus.LevelUpSelection)
            {
                return;
            }

            if (request.ValueRO.RerollRequested == 1)
            {
                request.ValueRW.RerollRequested = 0;
                request.ValueRW.HasSelection = 0;
                request.ValueRW.SelectedOptionIndex = -1;

                if (progression.ValueRO.RerollsRemaining > 0)
                {
                    progression.ValueRW.RerollsRemaining -= 1;
                    GenerateOptions(
                        registry,
                        ref options,
                        ownedWeapons,
                        ownedPassives,
                        availableWeapons,
                        availablePassives,
                        progression.ValueRO,
                        loop.ValueRO,
                        preferFreshOptions: true);
                }
            }

            if (options.Length == 0)
            {
                GenerateOptions(
                    registry,
                    ref options,
                    ownedWeapons,
                    ownedPassives,
                    availableWeapons,
                    availablePassives,
                    progression.ValueRO,
                    loop.ValueRO,
                    preferFreshOptions: false);
            }

            if (options.Length == 0)
            {
                loop.ValueRW.PendingLevelUps = math.max(0, loop.ValueRO.PendingLevelUps - 1);
                if (loop.ValueRO.PendingLevelUps > 1)
                {
                    GenerateOptions(
                        registry,
                        ref options,
                        ownedWeapons,
                        ownedPassives,
                        availableWeapons,
                        availablePassives,
                        progression.ValueRO,
                        loop.ValueRO,
                        preferFreshOptions: false);
                }
                else
                {
                    loop.ValueRW.Status = RunStatus.Playing;
                }

                return;
            }

            if (request.ValueRO.HasSelection == 0)
            {
                return;
            }

            int optionIndex = request.ValueRO.SelectedOptionIndex;
            request.ValueRW.HasSelection = 0;
            request.ValueRW.SelectedOptionIndex = -1;
            if (optionIndex < 0 || optionIndex >= options.Length)
            {
                return;
            }

            ApplySelection(registry, options[optionIndex], progression.ValueRO, ref ownedWeapons, ref ownedPassives);
            RuntimeBalanceUtility.RefreshPlayerRuntime(ref state, registry, classId);

            options.Clear();
            loop.ValueRW.PendingLevelUps = math.max(0, loop.ValueRO.PendingLevelUps - 1);
            if (loop.ValueRO.PendingLevelUps > 1)
            {
                GenerateOptions(
                    registry,
                    ref options,
                    ownedWeapons,
                    ownedPassives,
                    availableWeapons,
                    availablePassives,
                    progression.ValueRO,
                    loop.ValueRO,
                    preferFreshOptions: false);
            }
            else
            {
                loop.ValueRW.Status = RunStatus.Playing;
            }
        }

        private static void GenerateOptions(
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

        private static void ApplySelection(
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

        private static bool ContainsWeapon(DynamicBuffer<OwnedWeaponElement> ownedWeapons, FixedString64Bytes id)
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

        private static bool ContainsPassive(DynamicBuffer<OwnedPassiveElement> ownedPassives, FixedString64Bytes id)
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

        private static bool ContainsOption(DynamicBuffer<UpgradeOptionElement> options, FixedString64Bytes id)
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

        private static void AddCandidate(List<UpgradeOptionElement> candidates, UpgradeOptionElement candidate)
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

        private static void AddPreferredOptions(
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

        private static void EnsureOptionKindPresence(
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

        private static void EnsureFreshUnlockPresence(
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

        private static bool ContainsOption(DynamicBuffer<UpgradeOptionElement> options, UpgradeOptionElement candidate)
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

        private static int FindCandidateIndex(
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

        private static bool HasFreshUnlock(DynamicBuffer<UpgradeOptionElement> options, UpgradeKind kind)
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

        private static bool HasOptionKind(DynamicBuffer<UpgradeOptionElement> options, UpgradeKind kind)
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

        private static bool TryReplaceOption(
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

        private static bool ContainsOption(List<UpgradeOptionElement> options, UpgradeOptionElement candidate)
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
    }
}
