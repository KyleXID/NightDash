using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            float3 playerPos = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPos = transform.ValueRO.Position;
                break;
            }

            foreach (var (enemyTransform, enemyStats) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<CombatStats>>().WithAll<EnemyTag>())
            {
                float3 dir = math.normalizesafe(playerPos - enemyTransform.ValueRO.Position);
                enemyTransform.ValueRW.Position += dir * enemyStats.ValueRO.MoveSpeed * dt;
            }

            foreach (var (projTransform, projectile, velocity, projectileEntity) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileData>, RefRO<PhysicsVelocity2D>>()
                    .WithAll<ProjectileTag>()
                    .WithEntityAccess())
            {
                projTransform.ValueRW.Position += velocity.ValueRO.Value * dt;
                projectile.ValueRW.Age += dt;

                if (projectile.ValueRO.Age >= projectile.ValueRO.LifeTime)
                {
                    ecb.DestroyEntity(projectileEntity);
                }
            }

            var enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, CombatStats, LocalTransform>().Build();
            var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyStats = enemyQuery.ToComponentDataArray<CombatStats>(Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            float gainedExp = 0f;

            foreach (var (projTransform, projectile, projectileEntity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectileData>>()
                    .WithAll<ProjectileTag>()
                    .WithEntityAccess())
            {
                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    float dist = math.distance(projTransform.ValueRO.Position, enemyTransforms[i].Position);
                    if (dist > 0.45f)
                    {
                        continue;
                    }

                    var nextStats = enemyStats[i];
                    nextStats.Health -= projectile.ValueRO.Damage;

                    if (nextStats.Health <= 0f)
                    {
                        ecb.DestroyEntity(enemyEntities[i]);
                        gainedExp += 3f;
                    }
                    else
                    {
                        ecb.SetComponent(enemyEntities[i], nextStats);
                    }

                    ecb.DestroyEntity(projectileEntity);
                    break;
                }
            }

            enemyEntities.Dispose();
            enemyStats.Dispose();
            enemyTransforms.Dispose();

            if (gainedExp > 0f && SystemAPI.HasSingleton<GameLoopState>())
            {
                var loop = SystemAPI.GetSingletonRW<GameLoopState>();
                loop.ValueRW.Experience += gainedExp;
            }
        }
    }
}
