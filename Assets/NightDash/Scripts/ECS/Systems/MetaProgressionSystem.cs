using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EvolutionSystem))]
    public partial struct MetaProgressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<MetaProgress>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var loop = SystemAPI.GetSingletonRW<GameLoopState>();
            if (loop.ValueRO.RunEnded == 0 || loop.ValueRO.RewardGranted == 1)
            {
                return;
            }

            var meta = SystemAPI.GetSingletonRW<MetaProgress>();
            int reward = 100 + (loop.ValueRO.RiskScore * 2);
            meta.ValueRW.ConquestPoint += reward;
            loop.ValueRW.RewardGranted = 1;
        }
    }
}
