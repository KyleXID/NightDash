using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

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
            state.RequireForUpdate<EvolutionState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<DifficultyState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<EvolutionState> evolution = SystemAPI.GetSingletonRW<EvolutionState>();
            StageRuntimeConfig stage = SystemAPI.GetSingleton<StageRuntimeConfig>();
            DifficultyState difficulty = SystemAPI.GetSingleton<DifficultyState>();

            if (stage.IsStageCleared == 0)
            {
                return;
            }

            if (evolution.ValueRO.HasNormalEvolution == 0)
            {
                evolution.ValueRW.HasNormalEvolution = 1;
            }

            if (evolution.ValueRO.HasAbyssEvolution == 0 && evolution.ValueRO.CanAttemptAbyss == 1 && difficulty.RiskScore >= 10)
            {
                evolution.ValueRW.HasAbyssEvolution = 1;
            }
        }
    }
}
