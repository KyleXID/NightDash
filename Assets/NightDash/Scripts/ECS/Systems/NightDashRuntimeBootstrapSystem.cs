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
                typeof(SaveState),
                typeof(DataLoadState),
                typeof(RunSelection),
                typeof(EnemySpawnConfig));

            state.EntityManager.SetComponentData(entity, new GameLoopState
            {
                ElapsedTime = 0f,
                Level = 1,
                Experience = 0f,
                NextLevelExperience = 10f,
                IsRunActive = 1
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
            state.EntityManager.SetComponentData(entity, new BossSpawnState { HasSpawnedBoss = 0 });
            state.EntityManager.SetComponentData(entity, new DifficultyState { RiskScore = 0, RewardMultiplier = 1f });
            state.EntityManager.SetComponentData(entity, new EvolutionState
            {
                HasNormalEvolution = 0,
                HasAbyssEvolution = 0,
                CanAttemptAbyss = 1
            });
            state.EntityManager.SetComponentData(entity, new MetaProgress { ConquestPoints = 0, LastRunReward = 0 });
            state.EntityManager.SetComponentData(entity, new SaveState { LastSavedConquestPoints = -1 });
            state.EntityManager.SetComponentData(entity, new DataLoadState { HasLoaded = 0 });
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

            DynamicBuffer<StageTimelineElement> timeline = state.EntityManager.AddBuffer<StageTimelineElement>(entity);
            timeline.Add(new StageTimelineElement
            {
                StartTime = 0f,
                EndTime = 1200f,
                SpawnMultiplier = 1f,
                EnableBonusSpawn = 0
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
