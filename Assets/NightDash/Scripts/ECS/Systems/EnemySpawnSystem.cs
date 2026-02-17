using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<RandomState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawnConfigRW = SystemAPI.GetSingletonRW<EnemySpawnConfig>();
            ref var spawnConfig = ref spawnConfigRW.ValueRW;

            if (spawnConfig.EnemyPrefab == Entity.Null)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            float interval = math.max(0.1f, spawnConfig.BaseInterval * spawnConfig.RuntimeIntervalMultiplier);
            spawnConfig.Timer -= dt;

            if (spawnConfig.Timer > 0f)
            {
                return;
            }

            float3 playerPos = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPos = transform.ValueRO.Position;
                break;
            }

            var randomState = SystemAPI.GetSingletonRW<RandomState>();
            var random = randomState.ValueRW.Value;

            int spawnCount = math.max(1, spawnConfig.BaseSpawnCount + spawnConfig.RuntimeSpawnCountBonus);
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            for (int i = 0; i < spawnCount; i++)
            {
                Entity enemy = ecb.Instantiate(spawnConfig.EnemyPrefab);
                float2 dir = random.NextFloat2Direction();
                float distance = random.NextFloat(8f, 14f);
                float3 spawnPos = playerPos + new float3(dir.x, dir.y, 0f) * distance;

                ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, 1f));
            }

            randomState.ValueRW.Value = random;
            spawnConfig.Timer += interval;
        }
    }
}
