using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public sealed class NightDashBootstrapAuthoring : MonoBehaviour
    {
        [Header("Run")]
        public string selectedStageId = "stage_01";
        public string selectedClassId = "class_warrior";
        public float stageDurationSeconds = 900f;
        public float bossSpawnTimeSeconds = 900f;
        public bool allowAbyssEvolution = true;

        [Header("Spawn")]
        public GameObject enemyPrefab;
        public GameObject bossPrefab;
        public float spawnInterval = 1.5f;
        public uint randomSeed = 2026;

        [Header("Timeline")]
        public TimelineWindow[] timeline =
        {
            new TimelineWindow { startTime = 0f, endTime = 180f, spawnMultiplier = 1f, bonusSpawn = false },
            new TimelineWindow { startTime = 180f, endTime = 360f, spawnMultiplier = 1.2f, bonusSpawn = false },
            new TimelineWindow { startTime = 360f, endTime = 600f, spawnMultiplier = 1.4f, bonusSpawn = true },
            new TimelineWindow { startTime = 600f, endTime = 840f, spawnMultiplier = 1.8f, bonusSpawn = true },
            new TimelineWindow { startTime = 840f, endTime = 1200f, spawnMultiplier = 2.2f, bonusSpawn = true }
        };

        [Header("Difficulty Checklist")]
        public DifficultyEntry[] difficultyModifiers =
        {
            new DifficultyEntry { riskScore = 2, rewardMultiplierBonus = 0.2f, enemyHealthMultiplier = 1.2f, enemySpeedMultiplier = 1.1f },
            new DifficultyEntry { riskScore = 3, rewardMultiplierBonus = 0.3f, enemyHealthMultiplier = 1.3f, enemySpeedMultiplier = 1.2f }
        };

        [System.Serializable]
        public struct TimelineWindow
        {
            public float startTime;
            public float endTime;
            public float spawnMultiplier;
            public bool bonusSpawn;
        }

        [System.Serializable]
        public struct DifficultyEntry
        {
            public int riskScore;
            public float rewardMultiplierBonus;
            public float enemyHealthMultiplier;
            public float enemySpeedMultiplier;
        }

        private sealed class NightDashBootstrapBaker : Unity.Entities.Baker<NightDashBootstrapAuthoring>
        {
            public override void Bake(NightDashBootstrapAuthoring authoring)
            {
                GameObject enemyPrefab = authoring.enemyPrefab;
                GameObject bossPrefab = authoring.bossPrefab;

                Transform parent = authoring.transform.parent;
                if (parent != null)
                {
                    if (enemyPrefab == null)
                    {
                        Transform enemyFallback = parent.Find("EnemyPrefab");
                        if (enemyFallback != null)
                        {
                            enemyPrefab = enemyFallback.gameObject;
                        }
                    }

                    if (bossPrefab == null)
                    {
                        Transform bossFallback = parent.Find("BossPrefab");
                        if (bossFallback != null)
                        {
                            bossPrefab = bossFallback.gameObject;
                        }
                    }
                }

                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new GameLoopState
                {
                    ElapsedTime = 0f,
                    Level = 1,
                    Experience = 0f,
                    NextLevelExperience = 10f,
                    IsRunActive = 1
                });

                AddComponent(entity, new StageRuntimeConfig
                {
                    StageDuration = authoring.stageDurationSeconds,
                    BossSpawnTime = authoring.bossSpawnTimeSeconds,
                    SpawnRateMultiplier = 1f,
                    IsStageCleared = 0
                });

                AddComponent(entity, new BossSpawnState { HasSpawnedBoss = 0 });
                AddComponent(entity, new DifficultyState { RiskScore = 0, RewardMultiplier = 1f });
                AddComponent(entity, new EvolutionState
                {
                    HasNormalEvolution = 0,
                    HasAbyssEvolution = 0,
                    CanAttemptAbyss = authoring.allowAbyssEvolution ? (byte)1 : (byte)0
                });
                AddComponent(entity, new MetaProgress { ConquestPoints = 0, LastRunReward = 0 });
                AddComponent(entity, new SaveState { LastSavedConquestPoints = -1 });
                AddComponent(entity, new DataLoadState { HasLoaded = 0 });
                AddComponent(entity, new RunSelection
                {
                    StageId = new FixedString64Bytes(authoring.selectedStageId),
                    ClassId = new FixedString64Bytes(authoring.selectedClassId)
                });

                AddComponent(entity, new EnemySpawnConfig
                {
                    EnemyPrefab = enemyPrefab != null ? GetEntity(enemyPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    BossPrefab = bossPrefab != null ? GetEntity(bossPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    SpawnInterval = authoring.spawnInterval,
                    SpawnTimer = authoring.spawnInterval,
                    RandomSeed = authoring.randomSeed
                });

                DynamicBuffer<StageTimelineElement> timelineBuffer = AddBuffer<StageTimelineElement>(entity);
                if (authoring.timeline != null)
                {
                    for (int i = 0; i < authoring.timeline.Length; i++)
                    {
                        timelineBuffer.Add(new StageTimelineElement
                        {
                            StartTime = authoring.timeline[i].startTime,
                            EndTime = authoring.timeline[i].endTime,
                            SpawnMultiplier = authoring.timeline[i].spawnMultiplier,
                            EnableBonusSpawn = authoring.timeline[i].bonusSpawn ? (byte)1 : (byte)0
                        });
                    }
                }

                DynamicBuffer<DifficultyModifierElement> difficultyBuffer = AddBuffer<DifficultyModifierElement>(entity);
                if (authoring.difficultyModifiers != null)
                {
                    for (int i = 0; i < authoring.difficultyModifiers.Length; i++)
                    {
                        difficultyBuffer.Add(new DifficultyModifierElement
                        {
                            RiskScore = authoring.difficultyModifiers[i].riskScore,
                            RewardMultiplierBonus = authoring.difficultyModifiers[i].rewardMultiplierBonus,
                            EnemyHealthMultiplier = authoring.difficultyModifiers[i].enemyHealthMultiplier,
                            EnemySpeedMultiplier = authoring.difficultyModifiers[i].enemySpeedMultiplier
                        });
                    }
                }
            }
        }
    }
}
