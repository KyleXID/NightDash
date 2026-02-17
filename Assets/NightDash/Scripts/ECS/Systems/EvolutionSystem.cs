using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct EvolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<EvolutionState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var loop = SystemAPI.GetSingleton<GameLoopState>();
            var evolution = SystemAPI.GetSingletonRW<EvolutionState>();

            if (loop.IsBossDefeated == 1 && loop.RiskScore >= 40)
            {
                evolution.ValueRW.CanAbyssEvolution = 1;
            }
        }
    }
}
