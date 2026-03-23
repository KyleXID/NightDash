using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StageTimelineSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemySpawnConfig>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<BossSpawnState>();
            state.RequireForUpdate<SpawnArchetypeElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            StageRuntimeConfig stageConfig = SystemAPI.GetSingleton<StageRuntimeConfig>();
            RefRW<EnemySpawnConfig> spawnConfig = SystemAPI.GetSingletonRW<EnemySpawnConfig>();
            RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();
            DynamicBuffer<SpawnArchetypeElement> spawnArchetypes = SystemAPI.GetSingletonBuffer<SpawnArchetypeElement>();

            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing || stageConfig.IsStageCleared == 1)
            {
                return;
            }

            float3 playerPosition = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPosition = transform.ValueRO.Position;
                break;
            }

            var random = Unity.Mathematics.Random.CreateFromIndex(spawnConfig.ValueRO.RandomSeed == 0 ? 1u : spawnConfig.ValueRO.RandomSeed);
            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (bossState.ValueRO.HasSpawnedBoss == 0 &&
                loop.ElapsedTime >= stageConfig.BossSpawnTime &&
                spawnConfig.ValueRO.BossPrefab != Entity.Null)
            {
                Entity boss = ecb.Instantiate(spawnConfig.ValueRO.BossPrefab);
                float2 bossOffset = random.NextFloat2Direction() * 10f;
                ecb.SetComponent(boss, LocalTransform.FromPosition(new float3(playerPosition.x + bossOffset.x, playerPosition.y + bossOffset.y, 0f)));
                ApplyEnemyArchetype(ref ecb, boss, ResolveSpawnProfile(spawnArchetypes, loop.ElapsedTime, includeBoss: true, ref random, fallbackBoss: true));
                spawnConfig.ValueRW.RandomSeed = random.NextUInt();
                bossState.ValueRW.HasSpawnedBoss = 1;
                return;
            }

            if (bossState.ValueRO.HasSpawnedBoss == 1 || spawnConfig.ValueRO.EnemyPrefab == Entity.Null)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            spawnConfig.ValueRW.SpawnTimer -= dt;
            if (spawnConfig.ValueRO.SpawnTimer > 0f)
            {
                return;
            }

            float phaseSpawnPerMinute = ResolveSpawnRatePerMinute(spawnArchetypes, loop.ElapsedTime, includeBoss: false);
            float baseInterval = phaseSpawnPerMinute > 0f
                ? 60f / phaseSpawnPerMinute
                : spawnConfig.ValueRO.SpawnInterval;

            // Escalation: spawn rate increases over time (up to 3x at 10 min)
            float escalation = 1f + math.min(2f, loop.ElapsedTime / 300f);
            float effectiveInterval = math.max(0.05f, baseInterval / (math.max(0.25f, stageConfig.SpawnRateMultiplier) * escalation));
            spawnConfig.ValueRW.SpawnTimer = effectiveInterval;

            // Spawn 1-3 enemies per batch (more as time passes)
            int batchSize = 1 + (int)math.min(2f, loop.ElapsedTime / 240f);
            for (int s = 0; s < batchSize; s++)
            {
                float2 offset = random.NextFloat2Direction() * random.NextFloat(4f, 10f);
                Entity enemy = ecb.Instantiate(spawnConfig.ValueRO.EnemyPrefab);
                ecb.SetComponent(enemy, LocalTransform.FromPosition(new float3(playerPosition.x + offset.x, playerPosition.y + offset.y, 0f)));
                ApplyEnemyArchetype(ref ecb, enemy, ResolveSpawnProfile(spawnArchetypes, loop.ElapsedTime, includeBoss: false, ref random, fallbackBoss: false));
            }
            spawnConfig.ValueRW.RandomSeed = random.NextUInt();
        }

        private static EnemySpawnProfile ResolveSpawnProfile(
            DynamicBuffer<SpawnArchetypeElement> spawnArchetypes,
            float elapsedTime,
            bool includeBoss,
            ref Unity.Mathematics.Random random,
            bool fallbackBoss)
        {
            int totalWeight = 0;
            for (int i = 0; i < spawnArchetypes.Length; i++)
            {
                SpawnArchetypeElement entry = spawnArchetypes[i];
                if (elapsedTime < entry.StartTime || elapsedTime >= entry.EndTime)
                {
                    continue;
                }

                if ((entry.IsBoss == 1) != includeBoss)
                {
                    continue;
                }

                totalWeight += ResolveSelectionWeight(entry);
            }

            if (totalWeight <= 0)
            {
                return fallbackBoss
                    ? ResolveProfile(new FixedString64Bytes("boss_agron"))
                    : ResolveProfile(new FixedString64Bytes("ghoul_scout"));
            }

            int roll = random.NextInt(totalWeight);
            for (int i = 0; i < spawnArchetypes.Length; i++)
            {
                SpawnArchetypeElement entry = spawnArchetypes[i];
                if (elapsedTime < entry.StartTime || elapsedTime >= entry.EndTime)
                {
                    continue;
                }

                if ((entry.IsBoss == 1) != includeBoss)
                {
                    continue;
                }

                roll -= ResolveSelectionWeight(entry);
                if (roll < 0)
                {
                    return ResolveProfile(entry.EnemyId);
                }
            }

            return fallbackBoss
                ? ResolveProfile(new FixedString64Bytes("boss_agron"))
                : ResolveProfile(new FixedString64Bytes("ghoul_scout"));
        }

        private static float ResolveSpawnRatePerMinute(
            DynamicBuffer<SpawnArchetypeElement> spawnArchetypes,
            float elapsedTime,
            bool includeBoss)
        {
            int totalSpawnPerMinute = 0;
            for (int i = 0; i < spawnArchetypes.Length; i++)
            {
                SpawnArchetypeElement entry = spawnArchetypes[i];
                if (elapsedTime < entry.StartTime || elapsedTime >= entry.EndTime)
                {
                    continue;
                }

                if ((entry.IsBoss == 1) != includeBoss)
                {
                    continue;
                }

                totalSpawnPerMinute += math.max(0, entry.SpawnPerMinute);
            }

            return totalSpawnPerMinute;
        }

        private static int ResolveSelectionWeight(SpawnArchetypeElement entry)
        {
            int weight = math.max(1, entry.Weight);
            int spawnPerMinute = math.max(1, entry.SpawnPerMinute);
            return math.max(1, weight * spawnPerMinute);
        }

        private static void ApplyEnemyArchetype(ref EntityCommandBuffer ecb, Entity enemy, EnemySpawnProfile profile)
        {
            if (profile.IsBoss)
            {
                ecb.AddComponent<BossTag>(enemy);
            }
            else
            {
                ecb.RemoveComponent<BossTag>(enemy);
            }

            ecb.SetComponent(enemy, new CombatStats
            {
                CurrentHealth = profile.MaxHealth,
                MaxHealth = profile.MaxHealth,
                Damage = profile.Damage,
                MoveSpeed = profile.MoveSpeed
            });
            ecb.SetComponent(enemy, new EnemyArchetypeData
            {
                Id = profile.Id
            });
        }

        private static EnemySpawnProfile ResolveProfile(FixedString64Bytes enemyId)
        {
            if (enemyId == "ember_bat")
            {
                return new EnemySpawnProfile("ember_bat", 16f, 3f, 1.7f, false);
            }

            if (enemyId == "wasteland_brute")
            {
                return new EnemySpawnProfile("wasteland_brute", 54f, 10f, 0.9f, false);
            }

            if (enemyId == "ash_caster")
            {
                return new EnemySpawnProfile("ash_caster", 28f, 7f, 1.2f, false);
            }

            if (enemyId == "boss_agron")
            {
                return new EnemySpawnProfile("boss_agron", 320f, 14f, 0.85f, true);
            }

            return new EnemySpawnProfile("ghoul_scout", 22f, 4f, 1.4f, false);
        }

        private readonly struct EnemySpawnProfile
        {
            public EnemySpawnProfile(string id, float maxHealth, float damage, float moveSpeed, bool isBoss)
            {
                Id = id;
                MaxHealth = maxHealth;
                Damage = damage;
                MoveSpeed = moveSpeed;
                IsBoss = isBoss;
            }

            public FixedString64Bytes Id { get; }
            public float MaxHealth { get; }
            public float Damage { get; }
            public float MoveSpeed { get; }
            public bool IsBoss { get; }
        }
    }
}
