using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;

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
            var gameLoop = SystemAPI.GetSingletonRW<GameLoopState>();
            ref var loop = ref gameLoop.ValueRW;

            if (loop.RunEnded == 1)
            {
                return;
            }

            loop.ElapsedTime += SystemAPI.Time.DeltaTime;

            while (loop.Experience >= loop.NextLevelExperience)
            {
                loop.Experience -= loop.NextLevelExperience;
                loop.Level += 1;
                loop.NextLevelExperience *= 1.2f;
            }
        }
    }
}
