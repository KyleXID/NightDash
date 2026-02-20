using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StageProgressSystem))]
    public partial struct MetaProgressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaProgress>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<DifficultyState>();
            state.RequireForUpdate<GameLoopState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
            StageRuntimeConfig stage = SystemAPI.GetSingleton<StageRuntimeConfig>();
            DifficultyState difficulty = SystemAPI.GetSingleton<DifficultyState>();
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();

            if (stage.IsStageCleared == 0 || loop.IsRunActive == 1)
            {
                return;
            }

            if (meta.ValueRO.LastRunReward > 0)
            {
                return;
            }

            int reward = (int)(10 * difficulty.RewardMultiplier);
            meta.ValueRW.ConquestPoints += reward;
            meta.ValueRW.LastRunReward = reward;
        }
    }
}
