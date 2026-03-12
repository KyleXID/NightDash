using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GameLoopSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();

            if (loop.ValueRO.IsRunActive == 0 || loop.ValueRO.Status != RunStatus.Playing)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            loop.ValueRW.ElapsedTime += dt;

            if (loop.ValueRO.Experience >= loop.ValueRO.NextLevelExperience)
            {
                while (loop.ValueRO.Experience >= loop.ValueRO.NextLevelExperience)
                {
                    float requiredXp = loop.ValueRO.NextLevelExperience;
                    int nextLevel = loop.ValueRO.Level + 1;

                    loop.ValueRW.Experience -= requiredXp;
                    loop.ValueRW.Level = nextLevel;
                    loop.ValueRW.PendingLevelUps += 1;
                    loop.ValueRW.NextLevelExperience = GetExperienceRequiredForLevel(nextLevel);
                }

                if (loop.ValueRO.PendingLevelUps > 0)
                {
                    loop.ValueRW.Status = RunStatus.LevelUpSelection;
                }
            }
        }

        private static float GetExperienceRequiredForLevel(int level)
        {
            int safeLevel = math.max(1, level);
            return math.floor(20f + 8f * safeLevel + 1.6f * safeLevel * safeLevel);
        }
    }
}
