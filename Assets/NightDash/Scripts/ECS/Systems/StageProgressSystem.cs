using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct StageProgressSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var loop = SystemAPI.GetSingletonRW<GameLoopState>();
            var config = SystemAPI.GetSingleton<StageRuntimeConfig>();

            if (loop.ValueRO.RunEnded == 1)
            {
                return;
            }

            if (loop.ValueRO.ElapsedTime >= config.BossSpawnTime)
            {
                loop.ValueRW.IsBossSpawned = 1;
            }

            if (loop.ValueRO.ElapsedTime >= config.ClearTime)
            {
                loop.ValueRW.RunEnded = 1;
            }
        }
    }
}
