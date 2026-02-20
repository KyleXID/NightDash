using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StageTimelineSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemySpawnConfig>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<GameLoopState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            StageRuntimeConfig stageConfig = SystemAPI.GetSingleton<StageRuntimeConfig>();
            RefRW<EnemySpawnConfig> spawnConfig = SystemAPI.GetSingletonRW<EnemySpawnConfig>();

            if (loop.IsRunActive == 0 || stageConfig.IsStageCleared == 1)
            {
                return;
            }

            if (spawnConfig.ValueRO.EnemyPrefab == Entity.Null)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            spawnConfig.ValueRW.SpawnTimer -= dt;
            if (spawnConfig.ValueRO.SpawnTimer > 0f)
            {
                return;
            }

            float effectiveInterval = math.max(0.05f, spawnConfig.ValueRO.SpawnInterval / math.max(0.25f, stageConfig.SpawnRateMultiplier));
            spawnConfig.ValueRW.SpawnTimer = effectiveInterval;

            float3 playerPosition = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPosition = transform.ValueRO.Position;
                break;
            }

            var random = Unity.Mathematics.Random.CreateFromIndex(spawnConfig.ValueRO.RandomSeed == 0 ? 1u : spawnConfig.ValueRO.RandomSeed);
            float2 offset = random.NextFloat2Direction() * random.NextFloat(6f, 12f);
            spawnConfig.ValueRW.RandomSeed = random.NextUInt();

            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            Entity enemy = ecb.Instantiate(spawnConfig.ValueRO.EnemyPrefab);
            ecb.SetComponent(enemy, LocalTransform.FromPosition(new float3(playerPosition.x + offset.x, playerPosition.y + offset.y, 0f)));
        }
    }
}
