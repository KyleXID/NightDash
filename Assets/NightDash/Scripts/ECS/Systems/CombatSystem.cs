using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponSystem))]
    public partial struct CombatSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float3 playerPosition = float3.zero;
            bool hasPlayer = false;

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPosition = transform.ValueRO.Position;
                hasPlayer = true;
                break;
            }

            if (!hasPlayer)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform, stats) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<CombatStats>>().WithAll<EnemyTag>())
            {
                float3 toPlayer = playerPosition - transform.ValueRO.Position;
                float lengthSq = math.lengthsq(toPlayer);
                if (lengthSq <= 0.0001f)
                {
                    continue;
                }

                float3 direction = math.normalize(toPlayer);
                transform.ValueRW.Position += direction * stats.ValueRO.MoveSpeed * dt;
            }

            foreach (var projectile in SystemAPI.Query<RefRW<ProjectileData>>())
            {
                projectile.ValueRW.Lifetime -= dt;
            }
        }
    }
}
