using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    public static class NightDashCombatEvents
    {
        /// <summary>Fired when a projectile damages an enemy. Args: position, damage amount.</summary>
        public static event System.Action<float3, float> OnEnemyDamaged;

        /// <summary>Fired when an enemy's health reaches zero. Args: position, isBoss.</summary>
        public static event System.Action<float3, bool> OnEnemyKilled;

        /// <summary>Fired when the player takes damage (contact or projectile). Args: position, damage amount.</summary>
        public static event System.Action<float3, float> OnPlayerDamaged;

        internal static void FireEnemyDamaged(float3 position, float damage)
        {
            OnEnemyDamaged?.Invoke(position, damage);
        }

        internal static void FireEnemyKilled(float3 position, bool isBoss)
        {
            OnEnemyKilled?.Invoke(position, isBoss);
        }

        internal static void FirePlayerDamaged(float3 position, float damage)
        {
            OnPlayerDamaged?.Invoke(position, damage);
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponSystem))]
    public partial struct CombatSystem : ISystem
    {
        private const float ContactRange = 0.9f;
        private const float CasterPreferredRange = 5.25f;
        private const float CasterAttackCycle = 1.8f;
        private const float CasterAttackWindow = 0.16f;
        private const float CasterProjectileSpeed = 7.4f;
        private const float CasterProjectileRadius = 0.32f;
        private const float BossAttackCycle = 2.4f;
        private const float BossAttackWindow = 0.18f;
        private const float BossAttackRange = 5.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CombatStats>();
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<RunResultStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            RefRW<RunResultStats> result = SystemAPI.GetSingletonRW<RunResultStats>();
            if (loop.ValueRO.IsRunActive == 0 || loop.ValueRO.Status != RunStatus.Playing)
            {
                return;
            }

            Entity playerEntity = Entity.Null;
            float3 playerPosition = float3.zero;
            RefRW<CombatStats> playerStatsRef = default;
            bool hasPlayer = false;

            foreach (var (transform, stats, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<CombatStats>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                playerEntity = entity;
                playerPosition = transform.ValueRO.Position;
                playerStatsRef = stats;
                hasPlayer = true;
                break;
            }

            float dt = SystemAPI.Time.DeltaTime;
            float contactDamage = 0f;
            float contactRangeSq = ContactRange * ContactRange;
            float bossAttackRangeSq = BossAttackRange * BossAttackRange;
            bool bossAttackFrame = math.frac(loop.ValueRO.ElapsedTime / BossAttackCycle) < (BossAttackWindow / BossAttackCycle);
            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            using NativeList<Entity> deadEnemies = new(Allocator.Temp);
            using NativeList<byte> deadEnemyBossFlags = new(Allocator.Temp);
            using NativeList<float3> deadEnemyPositions = new(Allocator.Temp);

            foreach (var (transform, stats, archetype, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<CombatStats>, RefRO<EnemyArchetypeData>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                float3 toPlayer = playerPosition - transform.ValueRO.Position;
                float lengthSq = math.lengthsq(toPlayer);
                FixedString64Bytes archetypeId = archetype.ValueRO.Id;
                bool isCaster = archetypeId == "ash_caster";
                bool isBoss = SystemAPI.HasComponent<BossTag>(entity);
                if (hasPlayer && lengthSq <= contactRangeSq)
                {
                    contactDamage += math.max(0f, stats.ValueRO.Damage) * dt;
                }

                if (hasPlayer &&
                    bossAttackFrame &&
                    isBoss &&
                    lengthSq <= bossAttackRangeSq)
                {
                    contactDamage += math.max(4f, stats.ValueRO.Damage * 2.2f);
                }

                if (hasPlayer &&
                    isCaster &&
                    lengthSq > contactRangeSq)
                {
                    float cycleOffset = math.frac((loop.ValueRO.ElapsedTime + entity.Index * 0.173f) / CasterAttackCycle);
                    if (cycleOffset < (CasterAttackWindow / CasterAttackCycle))
                    {
                        SpawnEnemyProjectile(ref ecb, transform.ValueRO.Position, playerPosition, math.max(4f, stats.ValueRO.Damage));
                    }
                }

                if (lengthSq <= 0.0001f)
                {
                    continue;
                }

                float3 direction = math.normalize(toPlayer);
                if (isCaster)
                {
                    float preferredRangeSq = CasterPreferredRange * CasterPreferredRange;
                    if (lengthSq > preferredRangeSq)
                    {
                        transform.ValueRW.Position += direction * stats.ValueRO.MoveSpeed * dt;
                    }
                    else if (lengthSq < preferredRangeSq * 0.55f)
                    {
                        transform.ValueRW.Position -= direction * stats.ValueRO.MoveSpeed * 0.75f * dt;
                    }
                }
                else
                {
                    transform.ValueRW.Position += direction * stats.ValueRO.MoveSpeed * dt;
                }
            }

            // Enemy separation: push apart overlapping enemies
            const float separationRadius = 0.6f;
            const float separationForce = 3.0f;
            float separationRadiusSq = separationRadius * separationRadius;

            foreach (var (transformA, entityA) in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<EnemyTag>().WithAbsent<Prefab>().WithEntityAccess())
            {
                float3 pushForce = float3.zero;
                foreach (var (transformB, entityB) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithAbsent<Prefab>().WithEntityAccess())
                {
                    if (entityA == entityB) continue;
                    float3 diff = transformA.ValueRO.Position - transformB.ValueRO.Position;
                    float distSq = math.lengthsq(diff);
                    if (distSq < separationRadiusSq && distSq > 0.0001f)
                    {
                        float dist = math.sqrt(distSq);
                        pushForce += (diff / dist) * (1f - dist / separationRadius);
                    }
                }
                if (math.lengthsq(pushForce) > 0.0001f)
                {
                    transformA.ValueRW.Position += math.normalize(pushForce) * separationForce * dt;
                }
            }

            foreach (var (projectileTransform, projectile, velocity, projectileEntity) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<ProjectileData>, RefRO<PhysicsVelocity2D>>()
                         .WithEntityAccess())
            {
                projectile.ValueRW.Lifetime -= dt;
                projectileTransform.ValueRW.Position += new float3(
                    velocity.ValueRO.Value.x * dt,
                    velocity.ValueRO.Value.y * dt,
                    0f);

                if (projectile.ValueRO.Lifetime <= 0f)
                {
                    ecb.DestroyEntity(projectileEntity);
                    continue;
                }

                if (projectile.ValueRO.IsPlayerOwned == 0)
                {
                    if (!hasPlayer || playerEntity == Entity.Null)
                    {
                        continue;
                    }

                    float playerDistanceSq = math.lengthsq(playerPosition - projectileTransform.ValueRO.Position);
                    if (playerDistanceSq <= projectile.ValueRO.Radius * projectile.ValueRO.Radius)
                    {
                        float projectileDamage = projectile.ValueRO.Damage;
                        CombatStats playerStats = playerStatsRef.ValueRO;
                        playerStats.CurrentHealth = math.max(0f, playerStats.CurrentHealth - projectileDamage);
                        playerStatsRef.ValueRW = playerStats;
                        ecb.DestroyEntity(projectileEntity);

                        NightDashCombatEvents.FirePlayerDamaged(playerPosition, projectileDamage);

                        if (playerStats.CurrentHealth <= 0f)
                        {
                            loop.ValueRW.IsRunActive = 0;
                            loop.ValueRW.Status = RunStatus.Defeat;
                            if (SystemAPI.HasSingleton<StageRuntimeConfig>())
                            {
                                RefRW<StageRuntimeConfig> stage = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
                                stage.ValueRW.IsStageCleared = 0;
                            }
                        }
                    }

                    continue;
                }

                bool consumed = false;
                float hitRadiusSq = projectile.ValueRO.Radius * projectile.ValueRO.Radius;
                foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                             .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                             .WithAll<EnemyTag>()
                             .WithEntityAccess())
                {
                    float distanceSq = math.lengthsq(enemyTransform.ValueRO.Position - projectileTransform.ValueRO.Position);
                    if (distanceSq > hitRadiusSq)
                    {
                        continue;
                    }

                    CombatStats updatedEnemy = enemyStats.ValueRO;
                    float damageDealt = projectile.ValueRO.Damage;
                    updatedEnemy.CurrentHealth = math.max(0f, updatedEnemy.CurrentHealth - damageDealt);
                    enemyStats.ValueRW = updatedEnemy;
                    consumed = true;

                    float3 enemyPos = enemyTransform.ValueRO.Position;
                    NightDashCombatEvents.FireEnemyDamaged(enemyPos, damageDealt);

                    if (updatedEnemy.CurrentHealth <= 0f)
                    {
                        if (!ContainsEntity(deadEnemies, enemyEntity))
                        {
                            deadEnemies.Add(enemyEntity);
                            deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(enemyEntity) ? 1 : 0));
                            deadEnemyPositions.Add(enemyPos);
                        }
                    }

                    break;
                }

                if (consumed)
                {
                    ecb.DestroyEntity(projectileEntity);
                }
            }

            if (hasPlayer && playerEntity != Entity.Null && contactDamage > 0f)
            {
                CombatStats playerStats = playerStatsRef.ValueRO;
                playerStats.CurrentHealth = math.max(0f, playerStats.CurrentHealth - contactDamage);
                playerStatsRef.ValueRW = playerStats;

                NightDashCombatEvents.FirePlayerDamaged(playerPosition, contactDamage);

                if (playerStats.CurrentHealth <= 0f)
                {
                    loop.ValueRW.IsRunActive = 0;
                    loop.ValueRW.Status = RunStatus.Defeat;
                    if (SystemAPI.HasSingleton<StageRuntimeConfig>())
                    {
                        RefRW<StageRuntimeConfig> stage = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
                        stage.ValueRW.IsStageCleared = 0;
                    }
                }
            }

            for (int i = 0; i < deadEnemies.Length; i++)
            {
                Entity enemyEntity = deadEnemies[i];
                if (!state.EntityManager.Exists(enemyEntity))
                {
                    continue;
                }

                bool isBoss = deadEnemyBossFlags[i] == 1;
                float3 deathPosition = deadEnemyPositions[i];
                FixedString64Bytes archetypeId = state.EntityManager.GetComponentData<EnemyArchetypeData>(enemyEntity).Id;
                ResolveEnemyRewards(archetypeId, isBoss, out int goldReward, out int soulReward, out float xpReward);

                NightDashCombatEvents.FireEnemyKilled(deathPosition, isBoss);

                result.ValueRW.KillCount += 1;
                result.ValueRW.GoldEarned += goldReward;
                result.ValueRW.SoulsEarned += soulReward;
                loop.ValueRW.Experience += xpReward;

                if (isBoss && SystemAPI.HasSingleton<BossRewardState>())
                {
                    RefRW<BossRewardState> reward = SystemAPI.GetSingletonRW<BossRewardState>();
                    reward.ValueRW.HasPendingReward = 1;
                    reward.ValueRW.EvolutionResolved = 0;
                }

                if (isBoss && SystemAPI.HasSingleton<BossSpawnState>())
                {
                    RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();
                    bossState.ValueRW.BossKilled = 1;
                    bossState.ValueRW.ChestPending = 1;
                }

                ecb.DestroyEntity(enemyEntity);
            }
        }

        private static void ResolveEnemyRewards(
            FixedString64Bytes archetypeId,
            bool isBoss,
            out int goldReward,
            out int soulReward,
            out float xpReward)
        {
            if (isBoss || archetypeId == "boss_agron")
            {
                goldReward = 25;
                soulReward = 6;
                xpReward = 45f;
                return;
            }

            if (archetypeId == "wasteland_brute")
            {
                goldReward = 2;
                soulReward = 2;
                xpReward = 10f;
                return;
            }

            if (archetypeId == "ash_caster")
            {
                goldReward = 2;
                soulReward = 2;
                xpReward = 9f;
                return;
            }

            if (archetypeId == "ember_bat")
            {
                goldReward = 1;
                soulReward = 1;
                xpReward = 5f;
                return;
            }

            goldReward = 1;
            soulReward = 1;
            xpReward = 6f;
        }

        private static bool ContainsEntity(NativeList<Entity> entities, Entity target)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SpawnEnemyProjectile(ref EntityCommandBuffer ecb, float3 origin, float3 target, float damage)
        {
            float3 direction3 = target - origin;
            direction3.z = 0f;
            if (math.lengthsq(direction3) <= 0.0001f)
            {
                direction3 = new float3(1f, 0f, 0f);
            }

            float2 direction = math.normalize(direction3.xy);
            Entity projectile = ecb.CreateEntity();
            ecb.AddComponent(projectile, LocalTransform.FromPosition(origin));
            ecb.AddComponent(projectile, new ProjectileData
            {
                Damage = damage,
                Lifetime = 1.6f,
                IsPlayerOwned = 0,
                Radius = CasterProjectileRadius,
                WeaponId = default,
                IsMelee = 0
            });
            ecb.AddComponent(projectile, new PhysicsVelocity2D
            {
                Value = direction * CasterProjectileSpeed
            });
        }
    }
}
