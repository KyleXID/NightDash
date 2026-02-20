using Unity.Burst;
using Unity.Entities;
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
            float dt = SystemAPI.Time.DeltaTime;
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();

            if (loop.ValueRO.IsRunActive == 0)
            {
                return;
            }

            loop.ValueRW.ElapsedTime += dt;
            loop.ValueRW.Experience += dt * 2f;

            if (loop.ValueRO.Experience >= loop.ValueRO.NextLevelExperience)
            {
                loop.ValueRW.Experience -= loop.ValueRO.NextLevelExperience;
                loop.ValueRW.Level += 1;
                loop.ValueRW.NextLevelExperience = loop.ValueRO.NextLevelExperience * 1.2f;
            }
        }
    }
}
