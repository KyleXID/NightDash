using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EvolutionSystem))]
    public partial struct StageProgressSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<BossSpawnState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            RefRW<StageRuntimeConfig> stage = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
            RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();

            if (loop.ValueRO.IsRunActive == 0)
            {
                return;
            }

            if (bossState.ValueRO.HasSpawnedBoss == 0 && loop.ValueRO.ElapsedTime >= stage.ValueRO.BossSpawnTime)
            {
                bossState.ValueRW.HasSpawnedBoss = 1;
            }

            if (loop.ValueRO.ElapsedTime >= stage.ValueRO.StageDuration)
            {
                stage.ValueRW.IsStageCleared = 1;
                loop.ValueRW.IsRunActive = 0;
            }
        }
    }
}
