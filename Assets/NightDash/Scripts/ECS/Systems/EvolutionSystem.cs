using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using NightDash.Data;
using NightDash.Runtime;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct EvolutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EvolutionState>();
            state.RequireForUpdate<BossSpawnState>();
            state.RequireForUpdate<BossRewardState>();
            state.RequireForUpdate<BossRewardConfirmRequest>();
            state.RequireForUpdate<DifficultyState>();
            state.RequireForUpdate<OwnedWeaponElement>();
            state.RequireForUpdate<OwnedPassiveElement>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var registry = DataRegistry.Instance;
            if (registry?.Catalog == null)
            {
                return;
            }

            RefRW<EvolutionState> evolution = SystemAPI.GetSingletonRW<EvolutionState>();
            RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();
            RefRW<BossRewardState> bossReward = SystemAPI.GetSingletonRW<BossRewardState>();
            RefRW<BossRewardConfirmRequest> confirm = SystemAPI.GetSingletonRW<BossRewardConfirmRequest>();
            DifficultyState difficulty = SystemAPI.GetSingleton<DifficultyState>();
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();

            // Debug (F8, dev builds): force every owned weapon to its evolved form
            // immediately, ignoring the normal level-up-card conditions. Normal
            // evolution flows through the level-up cards (see EvolutionUtility +
            // UpgradeOptionUtility / UpgradeApplySystem).
            if (loop.Status == RunStatus.Playing && EvolutionDebug.ConsumeForceRequest())
            {
                DynamicBuffer<OwnedWeaponElement> weapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
                if (EvolutionUtility.ForceEvolveAll(registry, ref weapons))
                {
                    EntityCommandBuffer ecb = SystemAPI
                        .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);
                    EvolutionUtility.CleanupOrphanOrbits(ref state, weapons, ecb);
                }
            }

            // ----------------------------------------------------------------
            // Boss reward flow (chest + UI flags). Weapon evolution itself is now
            // delivered through level-up cards, so this only drives the boss chest
            // and the cosmetic "evolution available" flags.
            // ----------------------------------------------------------------
            if (bossState.ValueRO.BossKilled == 0 || bossReward.ValueRO.HasPendingReward == 0)
            {
                return;
            }

            if (loop.Status != RunStatus.Victory)
            {
                return;
            }

            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            EvolutionState nextEvolution = evolution.ValueRO;
            TryApplyBossRewardEvolution(registry, ownedWeapons, ownedPassives, difficulty, ref nextEvolution);
            evolution.ValueRW = nextEvolution;

            if (confirm.ValueRO.IsPending == 0)
            {
                return;
            }

            confirm.ValueRW.IsPending = 0;
            bossReward.ValueRW.HasPendingReward = 0;
            bossReward.ValueRW.EvolutionResolved = 1;
            bossState.ValueRW.ChestPending = 0;
            bossState.ValueRW.ChestOpened = 1;
        }

        // -------------------------------------------------------------------
        // Boss-reward UI flags. Sets HasNormal/HasAbyssEvolution so the result
        // screen can show "evolution available".
        // -------------------------------------------------------------------
        private static void TryApplyBossRewardEvolution(
            DataRegistry registry,
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            DynamicBuffer<OwnedPassiveElement> ownedPassives,
            DifficultyState difficulty,
            ref EvolutionState evolution)
        {
            if (registry?.Catalog?.evolutions == null)
            {
                return;
            }

            bool foundNormal = false;
            bool foundAbyss = false;

            for (int i = 0; i < registry.Catalog.evolutions.Count; i++)
            {
                EvolutionData evolutionData = registry.Catalog.evolutions[i];
                if (evolutionData == null || !MeetsEvolutionRequirements(evolutionData, ownedWeapons, ownedPassives, difficulty))
                {
                    continue;
                }

                if (evolutionData.isAbyss)
                {
                    foundAbyss = true;
                }
                else
                {
                    foundNormal = true;
                }
            }

            if (foundNormal)
            {
                evolution.HasNormalEvolution = 1;
            }

            if (foundAbyss && evolution.CanAttemptAbyss == 1)
            {
                evolution.HasAbyssEvolution = 1;
            }
        }

        private static bool MeetsEvolutionRequirements(
            EvolutionData evolutionData,
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            DynamicBuffer<OwnedPassiveElement> ownedPassives,
            DifficultyState difficulty)
        {
            if (!HasWeaponRequirement(ownedWeapons, evolutionData.requiredWeaponId, evolutionData.requiredWeaponLevel))
            {
                return false;
            }

            if (difficulty.RiskScore < evolutionData.requiredRiskScoreMin)
            {
                return false;
            }

            if (evolutionData.requiredPassiveIds != null)
            {
                for (int i = 0; i < evolutionData.requiredPassiveIds.Count; i++)
                {
                    string passiveId = evolutionData.requiredPassiveIds[i];
                    if (string.IsNullOrWhiteSpace(passiveId))
                    {
                        continue;
                    }

                    if (!HasPassiveRequirement(ownedPassives, passiveId))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasWeaponRequirement(
            DynamicBuffer<OwnedWeaponElement> ownedWeapons,
            string requiredWeaponId,
            int requiredWeaponLevel)
        {
            if (string.IsNullOrWhiteSpace(requiredWeaponId))
            {
                return false;
            }

            int safeRequiredLevel = math.max(1, requiredWeaponLevel);
            for (int i = 0; i < ownedWeapons.Length; i++)
            {
                OwnedWeaponElement owned = ownedWeapons[i];
                if (owned.Id.ToString() == requiredWeaponId && owned.Level >= safeRequiredLevel)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPassiveRequirement(DynamicBuffer<OwnedPassiveElement> ownedPassives, string requiredPassiveId)
        {
            for (int i = 0; i < ownedPassives.Length; i++)
            {
                if (ownedPassives[i].Id.ToString() == requiredPassiveId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
