using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

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
            state.RequireForUpdate<EnemySpawnConfig>();
            state.RequireForUpdate<StageTimelineElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameLoop = SystemAPI.GetSingleton<GameLoopState>();
            var spawnConfig = SystemAPI.GetSingletonRW<EnemySpawnConfig>();
            var timeline = SystemAPI.GetSingletonBuffer<StageTimelineElement>(true);

            float intervalMultiplier = 1f;
            int spawnCountBonus = 0;

            for (int i = 0; i < timeline.Length; i++)
            {
                if (gameLoop.ElapsedTime >= timeline[i].StartTime)
                {
                    intervalMultiplier = timeline[i].SpawnIntervalMultiplier;
                    spawnCountBonus = timeline[i].SpawnCountBonus;
                }
            }

            spawnConfig.ValueRW.RuntimeIntervalMultiplier = intervalMultiplier;
            spawnConfig.ValueRW.RuntimeSpawnCountBonus = spawnCountBonus;
        }
    }
}
