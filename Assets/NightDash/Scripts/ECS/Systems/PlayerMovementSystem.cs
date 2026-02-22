using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EnemySpawnSystem))]
    public partial struct PlayerMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<CombatStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<GameLoopState>().IsRunActive == 0)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            StageRuntimeConfig stage = SystemAPI.GetSingleton<StageRuntimeConfig>();
            float2 input = new float2(NightDashPlayerInputRuntime.MoveAxis.x, NightDashPlayerInputRuntime.MoveAxis.y);
            if (math.lengthsq(input) <= 0f)
            {
                return;
            }

            float2 dir = math.normalize(input);
            foreach (var (transform, stats) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<CombatStats>>().WithAll<PlayerTag>())
            {
                float3 delta = new float3(dir.x, dir.y, 0f) * stats.ValueRO.MoveSpeed * dt;
                float3 position = transform.ValueRO.Position + delta;
                if (stage.UseBounds == 1)
                {
                    position.x = math.clamp(position.x, stage.BoundsMin.x, stage.BoundsMax.x);
                    position.y = math.clamp(position.y, stage.BoundsMin.y, stage.BoundsMax.y);
                }

                transform.ValueRW.Position = position;
            }
        }
    }
}
