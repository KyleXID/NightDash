using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

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
            state.RequireForUpdate<DifficultyState>();
            state.RequireForUpdate<DifficultyModifierElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (difficulty, modifiers) in SystemAPI
                         .Query<RefRW<DifficultyState>, DynamicBuffer<DifficultyModifierElement>>())
            {
                int risk = 0;
                float rewardMultiplier = 1f;

                for (int i = 0; i < modifiers.Length; i++)
                {
                    risk += modifiers[i].RiskScore;
                    rewardMultiplier += modifiers[i].RewardMultiplierBonus;
                }

                difficulty.ValueRW.RiskScore = risk;
                difficulty.ValueRW.RewardMultiplier = rewardMultiplier;
                break;
            }
        }
    }
}
