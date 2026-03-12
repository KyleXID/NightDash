using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DifficultySystem))]
    public partial struct StageTimelineSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<StageTimelineElement>();
            state.RequireForUpdate<RunResultStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float elapsed = SystemAPI.GetSingleton<GameLoopState>().ElapsedTime;
            RefRW<RunResultStats> result = SystemAPI.GetSingletonRW<RunResultStats>();

            foreach (var (stage, timeline) in SystemAPI
                         .Query<RefRW<StageRuntimeConfig>, DynamicBuffer<StageTimelineElement>>())
            {
                float spawnRateMultiplier = 1f;
                int currentWave = timeline.Length > 0 ? timeline.Length : 1;

                for (int i = 0; i < timeline.Length; i++)
                {
                    StageTimelineElement step = timeline[i];
                    if (elapsed >= step.StartTime && elapsed < step.EndTime)
                    {
                        spawnRateMultiplier = step.SpawnMultiplier;
                        currentWave = i + 1;
                        break;
                    }
                }

                stage.ValueRW.SpawnRateMultiplier = spawnRateMultiplier;
                result.ValueRW.CurrentWave = currentWave;
                break;
            }
        }
    }
}
