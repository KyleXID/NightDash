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

            if (loop.ValueRO.IsRunActive == 0 || loop.ValueRO.Status != RunStatus.Playing)
            {
                return;
            }

            if (bossState.ValueRO.BossKilled == 1)
            {
                stage.ValueRW.IsStageCleared = 1;
                loop.ValueRW.IsRunActive = 0;
                loop.ValueRW.Status = RunStatus.Victory;
                bossState.ValueRW.ChestPending = 1;
            }
        }
    }
}
