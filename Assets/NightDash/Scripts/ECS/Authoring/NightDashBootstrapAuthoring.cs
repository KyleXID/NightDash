using System;
using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public class NightDashBootstrapAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject enemyPrefab;

        [Header("Game Loop")]
        public int stageIndex = 1;
        public int startLevel = 1;
        public float startNextLevelExp = 10f;
        public float bossSpawnTime = 900f;
        public float clearTime = 1200f;

        [Header("Spawn")]
        public float baseSpawnInterval = 1.5f;
        public int baseSpawnCount = 1;

        [Header("Meta")]
        public int startConquestPoint;

        [Header("Random")]
        public uint randomSeed = 12345;

        [Serializable]
        public struct TimelinePoint
        {
            public float startTime;
            public float spawnIntervalMultiplier;
            public int spawnCountBonus;
        }

        [Serializable]
        public struct DifficultyEntry
        {
            public int modifierId;
            public float riskValue;
            public float rewardMultiplier;
            public bool enabled;
        }

        public TimelinePoint[] timelinePoints;
        public DifficultyEntry[] difficultyEntries;

        public class Baker : Baker<NightDashBootstrapAuthoring>
        {
            public override void Bake(NightDashBootstrapAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new GameLoopState
                {
                    StageIndex = authoring.stageIndex,
                    ElapsedTime = 0f,
                    Level = math.max(1, authoring.startLevel),
                    Experience = 0f,
                    NextLevelExperience = math.max(1f, authoring.startNextLevelExp),
                    RiskScore = 0,
                    IsBossSpawned = 0,
                    IsBossDefeated = 0,
                    RunEnded = 0,
                    RewardGranted = 0
                });

                AddComponent(entity, new EvolutionState
                {
                    NormalEvolutionCount = 0,
                    AbyssEvolutionCount = 0,
                    CanAbyssEvolution = 0
                });

                AddComponent(entity, new MetaProgress
                {
                    ConquestPoint = math.max(0, authoring.startConquestPoint),
                    AttackNodeLevel = 0,
                    SurvivalNodeLevel = 0,
                    AbyssNodeLevel = 0
                });

                AddComponent(entity, new EnemySpawnConfig
                {
                    EnemyPrefab = authoring.enemyPrefab != null
                        ? GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    BaseInterval = math.max(0.1f, authoring.baseSpawnInterval),
                    Timer = math.max(0.1f, authoring.baseSpawnInterval),
                    BaseSpawnCount = math.max(1, authoring.baseSpawnCount),
                    RuntimeIntervalMultiplier = 1f,
                    RuntimeSpawnCountBonus = 0
                });

                AddComponent(entity, new StageRuntimeConfig
                {
                    BossSpawnTime = math.max(10f, authoring.bossSpawnTime),
                    ClearTime = math.max(30f, authoring.clearTime)
                });

                uint seed = authoring.randomSeed == 0 ? 1u : authoring.randomSeed;
                AddComponent(entity, new RandomState
                {
                    Value = Unity.Mathematics.Random.CreateFromIndex(seed)
                });

                var timelineBuffer = AddBuffer<StageTimelineElement>(entity);
                if (authoring.timelinePoints != null)
                {
                    for (int i = 0; i < authoring.timelinePoints.Length; i++)
                    {
                        timelineBuffer.Add(new StageTimelineElement
                        {
                            StartTime = math.max(0f, authoring.timelinePoints[i].startTime),
                            SpawnIntervalMultiplier = math.max(0.2f, authoring.timelinePoints[i].spawnIntervalMultiplier),
                            SpawnCountBonus = math.max(0, authoring.timelinePoints[i].spawnCountBonus)
                        });
                    }
                }

                var difficultyBuffer = AddBuffer<DifficultyModifierElement>(entity);
                if (authoring.difficultyEntries != null)
                {
                    for (int i = 0; i < authoring.difficultyEntries.Length; i++)
                    {
                        difficultyBuffer.Add(new DifficultyModifierElement
                        {
                            ModifierId = authoring.difficultyEntries[i].modifierId,
                            RiskValue = math.max(0f, authoring.difficultyEntries[i].riskValue),
                            RewardMultiplier = math.max(1f, authoring.difficultyEntries[i].rewardMultiplier),
                            Enabled = authoring.difficultyEntries[i].enabled ? (byte)1 : (byte)0
                        });
                    }
                }
            }
        }
    }
}
