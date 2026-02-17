using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial struct WeaponSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<WeaponRuntimeData>();
            state.RequireForUpdate<RandomState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var randomState = SystemAPI.GetSingletonRW<RandomState>();
            var random = randomState.ValueRW.Value;
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (weapon, transform) in
                SystemAPI.Query<RefRW<WeaponRuntimeData>, RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                weapon.ValueRW.Timer -= dt;
                if (weapon.ValueRO.Timer > 0f || weapon.ValueRO.ProjectilePrefab == Entity.Null)
                {
                    continue;
                }

                weapon.ValueRW.Timer += math.max(0.05f, weapon.ValueRO.Cooldown);

                Entity projectile = ecb.Instantiate(weapon.ValueRO.ProjectilePrefab);
                float2 dir2 = random.NextFloat2Direction();
                float3 velocity = new float3(dir2.x, dir2.y, 0f) * weapon.ValueRO.ProjectileSpeed;

                ecb.SetComponent(projectile,
                    LocalTransform.FromPositionRotationScale(transform.ValueRO.Position, quaternion.identity, 1f));
                ecb.SetComponent(projectile, new ProjectileData
                {
                    Speed = math.length(velocity),
                    Damage = weapon.ValueRO.Damage,
                    LifeTime = weapon.ValueRO.ProjectileLifeTime,
                    Age = 0f
                });
                ecb.SetComponent(projectile, new PhysicsVelocity2D { Value = velocity });
            }

            randomState.ValueRW.Value = random;
        }
    }
}
