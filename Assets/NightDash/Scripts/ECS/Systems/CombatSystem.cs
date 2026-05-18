using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    // NightDashCombatEvents — moved to Combat/CombatEvents.cs (S2-02).
    // Helpers ResolveEnemyRewards / ContainsEntity / SpawnEnemyProjectile —
    // moved to Combat/CombatHelpers.cs (S2-02). Call through CombatHelpers.*
    // below; external consumers are unaffected (namespace preserved).

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponSystem))]
    public partial struct CombatSystem : ISystem
    {
        private const float ContactRange = 0.9f;
        private const float CasterPreferredRange = 5.25f;
        private const float CasterAttackCycle = 1.8f;
        private const float CasterAttackWindow = 0.16f;
        private const float BossAttackCycle = 2.4f;
        private const float BossAttackWindow = 0.18f;
        private const float BossAttackRange = 5.5f;

        // Seeded so debug runs are repeatable but not all-identical. The
        // PRNG drives player → enemy crit rolls; state advances across
        // hits so consecutive rolls diverge.
        private static Unity.Mathematics.Random _critRng = Unity.Mathematics.Random.CreateFromIndex(0xC817_3144u);

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

                // Status effects: stun blocks everything (movement + ranged
                // attack + contact contribution); freeze blocks movement
                // alone — the caster ranged volley still resolves so frozen
                // enemies aren't completely inert when shooting from range.
                bool isStunned = false;
                bool isFrozen = false;
                if (SystemAPI.HasComponent<StatusEffectState>(entity))
                {
                    StatusEffectState sfx = SystemAPI.GetComponent<StatusEffectState>(entity);
                    isStunned = (sfx.ActiveMask & StatusEffectBits.Stun) != 0;
                    isFrozen  = (sfx.ActiveMask & StatusEffectBits.Freeze) != 0;
                }
                if (hasPlayer && lengthSq <= contactRangeSq && !isStunned)
                {
                    contactDamage += math.max(0f, stats.ValueRO.Damage) * dt;
                }

                if (hasPlayer &&
                    bossAttackFrame &&
                    isBoss &&
                    !isStunned &&
                    lengthSq <= bossAttackRangeSq)
                {
                    contactDamage += math.max(4f, stats.ValueRO.Damage * 2.2f);
                }

                if (hasPlayer &&
                    isCaster &&
                    !isStunned &&
                    lengthSq > contactRangeSq)
                {
                    float cycleOffset = math.frac((loop.ValueRO.ElapsedTime + entity.Index * 0.173f) / CasterAttackCycle);
                    if (cycleOffset < (CasterAttackWindow / CasterAttackCycle))
                    {
                        CombatHelpers.SpawnEnemyProjectile(ref ecb, transform.ValueRO.Position, playerPosition, math.max(4f, stats.ValueRO.Damage));
                    }
                }

                if (lengthSq <= 0.0001f)
                {
                    continue;
                }

                // Frozen / stunned enemies stand still. Caster behavior is
                // already gated above; this skip covers the brute/runner
                // chase path too.
                if (isFrozen || isStunned)
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

            // Enemy separation: push apart overlapping enemies, weighted by mass.
            // Heavy archetypes (brute, boss) shove lighter ones aside so the
            // player can be reached even when the screen is congested. Mass
            // ratio is applied to pushForce magnitude (no normalize) — heavy
            // adjacent neighbours push self lightly, light neighbours push self hard.
            const float separationRadius = 0.6f;
            const float separationForce = 3.0f;
            float separationRadiusSq = separationRadius * separationRadius;

            foreach (var (transformA, archetypeA, entityA) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<EnemyArchetypeData>>()
                         .WithAll<EnemyTag>().WithAbsent<Prefab>().WithEntityAccess())
            {
                float massA = GetEnemyMass(archetypeA.ValueRO.Id);
                float3 pushForce = float3.zero;
                foreach (var (transformB, archetypeB, entityB) in SystemAPI
                             .Query<RefRO<LocalTransform>, RefRO<EnemyArchetypeData>>()
                             .WithAll<EnemyTag>().WithAbsent<Prefab>().WithEntityAccess())
                {
                    if (entityA == entityB) continue;
                    float3 diff = transformA.ValueRO.Position - transformB.ValueRO.Position;
                    float distSq = math.lengthsq(diff);
                    if (distSq < separationRadiusSq && distSq > 0.0001f)
                    {
                        float dist = math.sqrt(distSq);
                        float massB = GetEnemyMass(archetypeB.ValueRO.Id);
                        // mass ratio: heavier B → A is pushed harder; heavier A → A is pushed less.
                        float massRatio = massB / massA;
                        pushForce += (diff / dist) * (1f - dist / separationRadius) * massRatio;
                    }
                }
                if (math.lengthsq(pushForce) > 0.0001f)
                {
                    transformA.ValueRW.Position += pushForce * separationForce * dt;
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
                        ApplyDamageWithShield(ref playerStats, projectileDamage);
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
                    // Crit roll — reads player CritChance/Multiplier off
                    // the cached playerStatsRef. Falls through to base
                    // damage when no player is present or crit is unset.
                    if (hasPlayer)
                    {
                        CombatStats playerForCrit = playerStatsRef.ValueRO;
                        if (playerForCrit.CritChance > 0f && playerForCrit.CritMultiplier > 1f &&
                            _critRng.NextFloat() < playerForCrit.CritChance)
                        {
                            damageDealt *= playerForCrit.CritMultiplier;
                        }
                    }
                    updatedEnemy.CurrentHealth = math.max(0f, updatedEnemy.CurrentHealth - damageDealt);
                    enemyStats.ValueRW = updatedEnemy;
                    consumed = true;

                    // Queue status effect rolls — applying mid-foreach is a
                    // structural change that would invalidate iteration. The
                    // queue is drained after the hit loop finishes.
                    if (SystemAPI.HasSingleton<StatusEffectConfig>())
                    {
                        QueueStatusOnHit(enemyEntity, SystemAPI.HasComponent<BossTag>(enemyEntity));
                    }

                    float3 enemyPos = enemyTransform.ValueRO.Position;
                    NightDashCombatEvents.FireEnemyDamaged(enemyPos, damageDealt);

                    if (updatedEnemy.CurrentHealth <= 0f)
                    {
                        if (!CombatHelpers.ContainsEntity(deadEnemies, enemyEntity))
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

            // Drain the status-effect queue now that the hit foreach has
            // released its iterator — structural changes are safe here.
            FlushStatusQueue(ref state);

            if (hasPlayer && playerEntity != Entity.Null && contactDamage > 0f)
            {
                CombatStats playerStats = playerStatsRef.ValueRO;
                ApplyDamageWithShield(ref playerStats, contactDamage);
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
                CombatHelpers.ResolveEnemyRewards(archetypeId, isBoss, out int goldReward, out int soulReward, out float xpReward);

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

            // Difficulty modifier: on-kill explosion. If the modifier is active,
            // any enemy that died this frame splashes nearby survivors for fixed
            // damage. Splash victims that die from this don't drop rewards
            // (they re-enter the deadEnemies pipeline next frame instead).
            if (deadEnemyPositions.Length > 0 &&
                SystemAPI.HasSingleton<DifficultyState>() &&
                SystemAPI.GetSingleton<DifficultyState>().OnKillExplosionEnabled != 0)
            {
                ApplyOnKillExplosions(ref state, deadEnemies, deadEnemyPositions);
            }
        }

        private void ApplyOnKillExplosions(
            ref SystemState state,
            NativeList<Entity> deadEnemies,
            NativeList<float3> deadEnemyPositions)
        {
            const float ExplosionRadius = 4f;
            const float ExplosionDamage = 25f;
            float radiusSq = ExplosionRadius * ExplosionRadius;

            foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                         .WithAll<EnemyTag>()
                         .WithEntityAccess())
            {
                if (CombatHelpers.ContainsEntity(deadEnemies, enemyEntity)) continue;
                if (enemyStats.ValueRO.CurrentHealth <= 0f) continue;

                float3 pos = enemyTransform.ValueRO.Position;
                bool inBlast = false;
                for (int j = 0; j < deadEnemyPositions.Length; j++)
                {
                    if (math.lengthsq(pos - deadEnemyPositions[j]) <= radiusSq)
                    {
                        inBlast = true;
                        break;
                    }
                }
                if (!inBlast) continue;

                CombatStats updated = enemyStats.ValueRO;
                updated.CurrentHealth = math.max(0f, updated.CurrentHealth - ExplosionDamage);
                enemyStats.ValueRW = updated;
                NightDashCombatEvents.FireEnemyDamaged(pos, ExplosionDamage);
            }
        }

        // Per-archetype mass for enemy separation. Higher mass = pushes others
        // aside more, gets pushed less. Tuned so brute and boss can shove
        // smaller mobs out of the way to reach the player.
        private static float GetEnemyMass(FixedString64Bytes id)
        {
            if (id == "boss_agron") return 10f;
            if (id == "wasteland_brute") return 4f;
            if (id == "elt_wastes_executor") return 3f;
            if (id == "ember_bat") return 0.5f;
            return 1f; // ghoul_scout, ash_caster, default
        }

        // Drains shield first, then bleeds the remainder into CurrentHealth.
        // Resets the time-since-hit counter so ShieldSystem holds regen for
        // a moment before ticking shield back up. Mutates `stats` in place.
        private static void ApplyDamageWithShield(ref CombatStats stats, float damage)
        {
            if (damage <= 0f) return;
            stats.TimeSinceLastHit = 0f;
            if (stats.CurrentShield > 0f)
            {
                float absorbed = math.min(stats.CurrentShield, damage);
                stats.CurrentShield -= absorbed;
                damage -= absorbed;
            }
            if (damage > 0f)
            {
                stats.CurrentHealth = math.max(0f, stats.CurrentHealth - damage);
            }
        }

        // Status-effect roll queue. Filled during the hit-foreach (no
        // structural changes allowed mid-iteration), drained immediately
        // after via FlushStatusQueue → main-thread EntityManager writes so
        // the component is visible on the SAME frame.
        //
        // Must be static: ISystem requires an unmanaged struct, and a
        // managed List<> instance field breaks the source generator (the
        // World then tries to register CombatSystem as ComponentSystemBase
        // and throws). Same reason _critRng is static.
        private static Unity.Mathematics.Random _statusRng =
            Unity.Mathematics.Random.CreateFromIndex(0xA17F_3911u);
        private static readonly System.Collections.Generic.List<(Entity target, byte mask, bool isBoss)>
            _statusQueueScratch = new();

        private void QueueStatusOnHit(Entity target, bool isBoss)
        {
            StatusEffectConfig cfg = SystemAPI.GetSingleton<StatusEffectConfig>();
            byte mask = 0;
            if (_statusRng.NextFloat() < cfg.BurnApplyChance)   mask |= StatusEffectBits.Burn;
            if (_statusRng.NextFloat() < cfg.PoisonApplyChance) mask |= StatusEffectBits.Poison;
            if (_statusRng.NextFloat() < cfg.FreezeApplyChance) mask |= StatusEffectBits.Freeze;
            if (_statusRng.NextFloat() < cfg.StunApplyChance)   mask |= StatusEffectBits.Stun;
            if (mask == 0) return;
            _statusQueueScratch.Add((target, mask, isBoss));
        }

        private void FlushStatusQueue(ref SystemState state)
        {
            if (_statusQueueScratch == null || _statusQueueScratch.Count == 0) return;
            StatusEffectConfig cfg = SystemAPI.GetSingleton<StatusEffectConfig>();
            EntityManager em = state.EntityManager;
            for (int i = 0; i < _statusQueueScratch.Count; i++)
            {
                var (target, mask, isBoss) = _statusQueueScratch[i];
                if (!em.Exists(target)) continue;
                if ((mask & StatusEffectBits.Burn)   != 0)
                    StatusEffectSystem.ApplyEffect(em, target, StatusEffectKind.Burn, cfg, isBoss);
                if ((mask & StatusEffectBits.Poison) != 0)
                    StatusEffectSystem.ApplyEffect(em, target, StatusEffectKind.Poison, cfg, isBoss);
                if ((mask & StatusEffectBits.Freeze) != 0)
                    StatusEffectSystem.ApplyEffect(em, target, StatusEffectKind.Freeze, cfg, isBoss);
                if ((mask & StatusEffectBits.Stun)   != 0)
                    StatusEffectSystem.ApplyEffect(em, target, StatusEffectKind.Stun, cfg, isBoss);
            }
            _statusQueueScratch.Clear();
        }
    }
}
