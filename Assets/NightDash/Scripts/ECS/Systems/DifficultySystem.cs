using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct DifficultySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<DifficultyModifierElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameLoop = SystemAPI.GetSingletonRW<GameLoopState>();
            var modifiers = SystemAPI.GetSingletonBuffer<DifficultyModifierElement>(true);

            int riskSum = 0;
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Enabled == 1)
                {
                    riskSum += (int)modifiers[i].RiskValue;
                }
            }

            gameLoop.ValueRW.RiskScore = riskSum;
        }
    }
}
