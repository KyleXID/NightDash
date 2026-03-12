using NightDash.ECS.Authoring;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct NightDashRuntimeBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityQuery existing = SystemAPI.QueryBuilder()
                .WithAll<RunSelection, DataLoadState>()
                .Build();
            if (!existing.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            if (!NightDashRuntimeToggles.EnableFallbackBootstrapWhenBakingMissing)
            {
                state.Enabled = false;
                return;
            }

            var authoring = Object.FindFirstObjectByType<NightDashBootstrapAuthoring>(FindObjectsInactive.Include);
            if (authoring == null)
            {
                // Scene may not be loaded yet; keep trying until authoring appears.
                return;
            }

            CreateFallbackSingleton(ref state);
            NightDashLog.Warn("[NightDash] Fallback runtime bootstrap singleton created because baked bootstrap entity was missing.");
            state.Enabled = false;
        }

        private static void CreateFallbackSingleton(ref SystemState state)
        {
            Entity entity = state.EntityManager.CreateEntity(
                typeof(GameLoopState),
                typeof(StageRuntimeConfig),
                typeof(BossSpawnState),
                typeof(DifficultyState),
                typeof(EvolutionState),
                typeof(MetaProgress),
                typeof(RunResultStats),
                typeof(BossRewardState),
                typeof(BossRewardConfirmRequest),
                typeof(ResultSnapshot),
                typeof(SaveState),
                typeof(DataLoadState),
                typeof(PlayerProgressionState),
                typeof(UpgradeSelectionRequest),
                typeof(RunNavigationRequest),
                typeof(RunSelection),
                typeof(EnemySpawnConfig));

            state.EntityManager.SetComponentData(entity, new GameLoopState
            {
                ElapsedTime = 0f,
                Level = 1,
                Experience = 0f,
                NextLevelExperience = 10f,
                IsRunActive = 0,
                Status = RunStatus.Loading,
                PendingLevelUps = 0
            });
            state.EntityManager.SetComponentData(entity, new StageRuntimeConfig
            {
                StageDuration = 900f,
                BossSpawnTime = 900f,
                SpawnRateMultiplier = 1f,
                IsStageCleared = 0,
                UseBounds = 1,
                BoundsMin = new float2(-30f, -18f),
                BoundsMax = new float2(30f, 18f)
            });
            state.EntityManager.SetComponentData(entity, new BossSpawnState
            {
                HasSpawnedBoss = 0,
                BossKilled = 0,
                ChestPending = 0,
                ChestOpened = 0
            });
            state.EntityManager.SetComponentData(entity, new DifficultyState { RiskScore = 0, RewardMultiplier = 1f });
            state.EntityManager.SetComponentData(entity, new EvolutionState
            {
                HasNormalEvolution = 0,
                HasAbyssEvolution = 0,
                CanAttemptAbyss = 1
            });
            state.EntityManager.SetComponentData(entity, new MetaProgress { ConquestPoints = 0, LastRunReward = 0 });
            state.EntityManager.SetComponentData(entity, new RunResultStats
            {
                KillCount = 0,
                GoldEarned = 0,
                SoulsEarned = 0,
                CurrentWave = 0,
                RewardCommitted = 0
            });
            state.EntityManager.SetComponentData(entity, new BossRewardState
            {
                HasPendingReward = 0,
                EvolutionResolved = 0
            });
            state.EntityManager.SetComponentData(entity, new BossRewardConfirmRequest { IsPending = 0 });
            state.EntityManager.SetComponentData(entity, new ResultSnapshot
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
            state.EntityManager.SetComponentData(entity, new SaveState { LastSavedConquestPoints = -1 });
            state.EntityManager.SetComponentData(entity, new DataLoadState { HasLoaded = 0 });
            state.EntityManager.SetComponentData(entity, new PlayerProgressionState
            {
                WeaponSlotLimit = 6,
                PassiveSlotLimit = 6,
                RerollsRemaining = 1
            });
            state.EntityManager.SetComponentData(entity, new UpgradeSelectionRequest
            {
                SelectedOptionIndex = -1,
                HasSelection = 0,
                RerollRequested = 0
            });
            state.EntityManager.SetComponentData(entity, new RunNavigationRequest
            {
                Action = RunNavigationAction.None,
                IsPending = 0
            });
            state.EntityManager.SetComponentData(entity, new RunSelection
            {
                StageId = new FixedString64Bytes("stage_01"),
                ClassId = new FixedString64Bytes("class_warrior")
            });
            state.EntityManager.SetComponentData(entity, new EnemySpawnConfig
            {
                EnemyPrefab = Entity.Null,
                BossPrefab = Entity.Null,
                SpawnInterval = 1.5f,
                SpawnTimer = 1.5f,
                RandomSeed = 2026
            });

            state.EntityManager.AddBuffer<StageTimelineElement>(entity);
            state.EntityManager.AddBuffer<SpawnArchetypeElement>(entity);
            state.EntityManager.AddBuffer<OwnedWeaponElement>(entity);
            state.EntityManager.AddBuffer<OwnedPassiveElement>(entity);
            state.EntityManager.AddBuffer<UpgradeOptionElement>(entity);
            state.EntityManager.AddBuffer<AvailableWeaponElement>(entity);
            state.EntityManager.AddBuffer<AvailablePassiveElement>(entity);
            DynamicBuffer<StageTimelineElement> timeline = state.EntityManager.GetBuffer<StageTimelineElement>(entity);
            DynamicBuffer<SpawnArchetypeElement> spawnArchetypes = state.EntityManager.GetBuffer<SpawnArchetypeElement>(entity);
            timeline.Add(new StageTimelineElement
            {
                StartTime = 0f,
                EndTime = 1200f,
                SpawnMultiplier = 1f,
                EnableBonusSpawn = 0
            });
            spawnArchetypes.Add(new SpawnArchetypeElement
            {
                StartTime = 0f,
                EndTime = 1200f,
                EnemyId = new FixedString64Bytes("ghoul_scout"),
                Weight = 10,
                SpawnPerMinute = 24,
                IsBoss = 0
            });
            spawnArchetypes.Add(new SpawnArchetypeElement
            {
                StartTime = 900f,
                EndTime = 1200f,
                EnemyId = new FixedString64Bytes("boss_agron"),
                Weight = 1,
                SpawnPerMinute = 1,
                IsBoss = 1
            });

            DynamicBuffer<DifficultyModifierElement> difficulty = state.EntityManager.AddBuffer<DifficultyModifierElement>(entity);
            difficulty.Add(new DifficultyModifierElement
            {
                RiskScore = 0,
                RewardMultiplierBonus = 0f,
                EnemyHealthMultiplier = 1f,
                EnemySpeedMultiplier = 1f
            });
        }
    }
}
