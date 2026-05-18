using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NightDash.Runtime
{
    internal static class RunSelectionLobbyWorldBridge
    {
        // Scratch buffers reused across calls so the lobby start path doesn't
        // allocate every press.
        private static readonly List<(string id, int level)> s_ModifierStages = new();
        private static readonly List<DifficultyModifierData> s_ModifierData = new();
        private static readonly List<int> s_ModifierLevels = new();

        public static bool TryApplySelectionToCurrentWorld(string stageId, string classId)
        {
            int worldCount = World.All.Count;
            if (worldCount == 0)
            {
                NightDashLog.Warn("[NightDash] No ECS world exists yet.");
                return false;
            }

            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<RunSelection>(),
                    ComponentType.ReadWrite<DataLoadState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                ResetRunSelectionEntity(entityManager, singleton, stageId, classId);

                NightDashLog.Info($"[NightDash] RunSelection applied directly to ECS world '{world.Name}': stage='{stageId}', class='{classId}'.");
                return true;
            }

            NightDashLog.Warn("[NightDash] No entity with RunSelection + DataLoadState found in any ECS world.");
            return false;
        }

        public static void SetRunActiveInCurrentWorld(bool active)
        {
            int worldCount = World.All.Count;
            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameLoopState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                GameLoopState loop = entityManager.GetComponentData<GameLoopState>(singleton);
                loop.IsRunActive = active ? (byte)1 : (byte)0;
                if (!active)
                {
                    loop.ElapsedTime = 0f;
                    loop.Level = 1;
                    loop.Experience = 0f;
                    if (loop.NextLevelExperience <= 0f)
                    {
                        loop.NextLevelExperience = 10f;
                    }
                }
                entityManager.SetComponentData(singleton, loop);
            }
        }

        public static bool TryGetPendingNavigation(out RunNavigationAction action, out string stageId, out string classId)
        {
            action = RunNavigationAction.None;
            stageId = null;
            classId = null;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<RunNavigationRequest>(),
                ComponentType.ReadOnly<GameLoopState>(),
                ComponentType.ReadOnly<RunSelection>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            Entity singleton = query.GetSingletonEntity();
            RunNavigationRequest navigation = entityManager.GetComponentData<RunNavigationRequest>(singleton);
            if (navigation.IsPending == 0 || navigation.Action == RunNavigationAction.None)
            {
                return false;
            }

            RunSelection selection = entityManager.GetComponentData<RunSelection>(singleton);
            action = navigation.Action;
            stageId = selection.StageId.IsEmpty ? "stage_01" : selection.StageId.ToString();
            classId = selection.ClassId.IsEmpty ? "class_warrior" : selection.ClassId.ToString();

            navigation.IsPending = 0;
            navigation.Action = RunNavigationAction.None;
            entityManager.SetComponentData(singleton, navigation);
            return true;
        }

        private static void ResetRunSelectionEntity(EntityManager entityManager, Entity singleton, string stageId, string classId)
        {
            entityManager.SetComponentData(singleton, new RunSelection
            {
                StageId = new FixedString64Bytes(stageId),
                ClassId = new FixedString64Bytes(classId)
            });
            entityManager.SetComponentData(singleton, new DataLoadState { HasLoaded = 0 });

            if (entityManager.HasComponent<GameLoopState>(singleton))
            {
                entityManager.SetComponentData(singleton, new GameLoopState
                {
                    ElapsedTime = 0f,
                    Level = 1,
                    Experience = 0f,
                    NextLevelExperience = 10f,
                    IsRunActive = 0,
                    Status = RunStatus.Loading,
                    PendingLevelUps = 0
                });
            }

            if (entityManager.HasComponent<StageRuntimeConfig>(singleton))
            {
                StageRuntimeConfig stageRuntime = entityManager.GetComponentData<StageRuntimeConfig>(singleton);
                stageRuntime.IsStageCleared = 0;
                entityManager.SetComponentData(singleton, stageRuntime);
            }

            if (entityManager.HasComponent<BossSpawnState>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossSpawnState
                {
                    HasSpawnedBoss = 0,
                    BossKilled = 0,
                    ChestPending = 0,
                    ChestOpened = 0
                });
            }

            if (entityManager.HasComponent<RunResultStats>(singleton))
            {
                entityManager.SetComponentData(singleton, new RunResultStats
                {
                    KillCount = 0,
                    GoldEarned = 0,
                    SoulsEarned = 0,
                    CurrentWave = 0,
                    RewardCommitted = 0
                });
            }

            if (entityManager.HasComponent<BossRewardState>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossRewardState
                {
                    HasPendingReward = 0,
                    EvolutionResolved = 0
                });
            }

            if (entityManager.HasComponent<BossRewardConfirmRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossRewardConfirmRequest
                {
                    IsPending = 0
                });
            }

            if (entityManager.HasComponent<ResultSnapshot>(singleton))
            {
                entityManager.SetComponentData(singleton, new ResultSnapshot
                {
                    HasSnapshot = 0,
                    IsVictory = 0,
                    ElapsedTime = 0f,
                    FinalLevel = 1,
                    KillCount = 0,
                    GoldEarned = 0,
                    SoulsEarned = 0,
                    RewardGranted = 0
                });
            }

            if (entityManager.HasComponent<UpgradeSelectionRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new UpgradeSelectionRequest
                {
                    SelectedOptionIndex = -1,
                    HasSelection = 0,
                    RerollRequested = 0
                });
            }

            if (entityManager.HasComponent<RunNavigationRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new RunNavigationRequest
                {
                    Action = RunNavigationAction.None,
                    IsPending = 0
                });
            }

            ApplyDifficultyModifiers(entityManager, singleton);
        }

        // Rebuilds the entity's DifficultyModifierElement buffer from the lobby's
        // modifier selection (stored in RunSelectionSession). Also pushes the
        // SO+level pairs to the HUD strip so the gameplay UI mirrors the run.
        private static void ApplyDifficultyModifiers(EntityManager entityManager, Entity singleton)
        {
            RunSelectionSession.GetCurrentModifierStages(s_ModifierStages);

            s_ModifierData.Clear();
            s_ModifierLevels.Clear();
            var resolvedStages = new List<DifficultyStage>(s_ModifierStages.Count);
            DataRegistry registry = DataRegistry.Instance;
            if (registry != null && s_ModifierStages.Count > 0)
            {
                for (int i = 0; i < s_ModifierStages.Count; i++)
                {
                    (string id, int level) entry = s_ModifierStages[i];
                    if (!registry.TryGetDifficulty(entry.id, out DifficultyModifierData data) || data == null) continue;
                    if (!data.TryGetStage(entry.level, out DifficultyStage stage)) continue;
                    s_ModifierData.Add(data);
                    s_ModifierLevels.Add(entry.level);
                    resolvedStages.Add(stage);
                }
            }

            if (!entityManager.HasBuffer<DifficultyModifierElement>(singleton))
            {
                entityManager.AddBuffer<DifficultyModifierElement>(singleton);
            }
            DynamicBuffer<DifficultyModifierElement> buffer =
                entityManager.GetBuffer<DifficultyModifierElement>(singleton);
            buffer.Clear();

            for (int i = 0; i < resolvedStages.Count; i++)
            {
                DifficultyStage stage = resolvedStages[i];
                buffer.Add(new DifficultyModifierElement
                {
                    RiskScore = stage.riskPoint,
                    RewardMultiplierBonus = stage.rewardBonusPct,
                    HpPct = stage.enemyModifiers.hpPct,
                    MoveSpeedPct = stage.enemyModifiers.moveSpeedPct,
                    SpawnRatePct = stage.enemyModifiers.spawnRatePct,
                    HealRatePct = stage.playerModifiers.healRatePct,
                    CooldownPct = stage.playerModifiers.cooldownPct,
                    HazardMultiplier = stage.runtimeEffects.hazardMultiplier,
                    OnKillExplosion = stage.runtimeEffects.onKillExplosion ? (byte)1 : (byte)0
                });
            }

            if (buffer.Length == 0)
            {
                // DifficultySystem requires at least one entry to write the
                // cached state. A zero entry resolves to all-1x multipliers
                // (no effect), which is what we want when nothing is picked.
                buffer.Add(default);
            }

            NightDashHudResultUI hud =
                Object.FindFirstObjectByType<NightDashHudResultUI>(FindObjectsInactive.Include);
            if (hud != null) hud.SetActiveDifficultyModifiers(s_ModifierData, s_ModifierLevels);
        }
    }
}
