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

            // Pause: freeze all combat (enemy movement, projectiles, contact
            // damage) while a GameplayPauseTag exists (pause menu / confirm).
            if (SystemAPI.HasSingleton<GameplayPauseTag>())
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
                         .Query<RefRW<LocalTransform>, RefRW<ProjectileData>, RefRW<PhysicsVelocity2D>>()
                         .WithEntityAccess())
            {
                projectile.ValueRW.Lifetime -= dt;

                // Behavior-driven movement. Orbit weapons re-anchor to the live
                // player position each frame (stay attached as the player
                // moves); ground zones stay put; everything else travels along
                // its velocity (linear projectiles, melee, sky-fall descent).
                byte behavior = projectile.ValueRO.Behavior;
                if (behavior == (byte)ProjectileBehavior.Orbit && hasPlayer && SystemAPI.HasComponent<OrbitState>(projectileEntity))
                {
                    OrbitState orbit = SystemAPI.GetComponent<OrbitState>(projectileEntity);
                    orbit.Angle += orbit.AngularSpeed * dt;
                    orbit.Radius += orbit.RadiusGrowth * dt; // >0 → spirals outward (light ring); 0 → fixed orbit
                    SystemAPI.SetComponent(projectileEntity, orbit);
                    float r = orbit.Radius;
                    projectileTransform.ValueRW.Position = playerPosition + new float3(math.cos(orbit.Angle) * r, orbit.CenterYOffset + math.sin(orbit.Angle) * r, 0f);
                }
                else if (behavior == (byte)ProjectileBehavior.Whip && hasPlayer && SystemAPI.HasComponent<WhipState>(projectileEntity))
                {
                    // Rubber-band chain in three phases over the lifetime: extend
                    // out to MaxReach, DWELL there, then retract to 0. The dwell
                    // keeps the scythe head planted at full reach for a beat before
                    // snapping back, instead of bouncing straight off the peak.
                    const float ExtendRatio = 0.25f; // 0 → MaxReach
                    const float HoldRatio   = 0.50f; // dwell at MaxReach (retract = remainder)
                    WhipState whip = SystemAPI.GetComponent<WhipState>(projectileEntity);
                    float tt = whip.TotalLifetime > 0.01f
                        ? math.clamp((whip.TotalLifetime - projectile.ValueRO.Lifetime) / whip.TotalLifetime, 0f, 1f)
                        : 1f;
                    float dist;
                    if (tt < ExtendRatio)
                    {
                        dist = whip.MaxReach * math.sin(0.5f * math.PI * (tt / ExtendRatio));      // ease-out 0 → Max
                    }
                    else if (tt < ExtendRatio + HoldRatio)
                    {
                        dist = whip.MaxReach;                                                      // dwell at full reach
                    }
                    else
                    {
                        float p = (tt - ExtendRatio - HoldRatio) / (1f - ExtendRatio - HoldRatio);
                        dist = whip.MaxReach * math.cos(0.5f * math.PI * p);                       // ease-in Max → 0
                    }
                    projectileTransform.ValueRW.Position = playerPosition + new float3(whip.Direction.x * dist, whip.Direction.y * dist, 0f);
                }
                else if (behavior != (byte)ProjectileBehavior.GroundZone)
                {
                    // Homing (evolved dark lightning's first bolt): steer velocity toward
                    // the tracked target each frame; others have no HomingState and fly straight.
                    if (SystemAPI.HasComponent<HomingState>(projectileEntity))
                    {
                        HomingState hs = SystemAPI.GetComponent<HomingState>(projectileEntity);
                        // Read the target via EntityManager (not SystemAPI) to avoid a
                        // LocalTransform query-aliasing conflict with the projectile query.
                        if (state.EntityManager.Exists(hs.Target) && state.EntityManager.HasComponent<LocalTransform>(hs.Target))
                        {
                            float2 toTarget = state.EntityManager.GetComponentData<LocalTransform>(hs.Target).Position.xy - projectileTransform.ValueRO.Position.xy;
                            float2 v = velocity.ValueRO.Value;
                            float spd = math.length(v);
                            if (spd > 0.01f && math.lengthsq(toTarget) > 0.0001f)
                            {
                                float curAng = math.atan2(v.y, v.x);
                                float desAng = math.atan2(toTarget.y, toTarget.x);
                                float diff = math.atan2(math.sin(desAng - curAng), math.cos(desAng - curAng));
                                float step = math.clamp(diff, -hs.TurnSpeed * dt, hs.TurnSpeed * dt);
                                float na = curAng + step;
                                velocity.ValueRW.Value = new float2(math.cos(na), math.sin(na)) * spd;
                            }
                        }
                    }

                    projectileTransform.ValueRW.Position += new float3(
                        velocity.ValueRO.Value.x * dt,
                        velocity.ValueRO.Value.y * dt,
                        0f);
                }

                if (projectile.ValueRO.Lifetime <= 0f)
                {
                    // Landing AoE (e.g. star-fall meteor): full damage to the direct
                    // target in a small core, reduced splash out to SplashRadius.
                    if (projectile.ValueRO.IsPlayerOwned == 1 && projectile.ValueRO.SplashRadius > 0f)
                    {
                        const float coreSq = 0.49f; // 0.7^2 — direct-target core takes full damage
                        float splashSq = projectile.ValueRO.SplashRadius * projectile.ValueRO.SplashRadius;
                        float fullDmg = projectile.ValueRO.Damage;
                        float splashDmg = projectile.ValueRO.Damage * projectile.ValueRO.SplashFactor;
                        float3 impactPos = projectileTransform.ValueRO.Position;
                        foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                                     .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                                     .WithAll<EnemyTag>()
                                     .WithEntityAccess())
                        {
                            float3 enemyPos = enemyTransform.ValueRO.Position;
                            float dsq = math.lengthsq(enemyPos - impactPos);
                            if (dsq > splashSq) continue;

                            float dmg = dsq <= coreSq ? fullDmg : splashDmg;
                            CombatStats es = enemyStats.ValueRO;
                            es.CurrentHealth = math.max(0f, es.CurrentHealth - dmg);
                            enemyStats.ValueRW = es;
                            NightDashCombatEvents.FireEnemyDamaged(enemyPos, dmg);

                            if (es.CurrentHealth <= 0f && !CombatHelpers.ContainsEntity(deadEnemies, enemyEntity))
                            {
                                deadEnemies.Add(enemyEntity);
                                deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(enemyEntity) ? 1 : 0));
                                deadEnemyPositions.Add(enemyPos);
                            }
                        }
                    }
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

                // Persistent multi-hit weapons (orbit ring / spinning blades /
                // barrier / ground zone): on a fixed cadence, damage EVERY enemy
                // in radius and never get consumed by a hit — only Lifetime ends
                // them. Knockback shoves contacted enemies outward (barrier).
                if (projectile.ValueRO.TickInterval > 0f)
                {
                    // Hit-once sweep (pierce arrows/bolts/waves + the spiralling light
                    // ring): damage each enemy at most ONCE as it passes through,
                    // checked EVERY frame (not on a tick cadence a fast projectile could
                    // skip between), tracked via the PierceHitElement buffer. Never
                    // consumed; only Lifetime ends it. Applies to ANY projectile carrying
                    // the buffer (Linear pierce OR the Orbit spiral arms).
                    if (SystemAPI.HasBuffer<PierceHitElement>(projectileEntity))
                    {
                        DynamicBuffer<PierceHitElement> pierced = SystemAPI.GetBuffer<PierceHitElement>(projectileEntity);
                        float pierceRadiusSq = projectile.ValueRO.Radius * projectile.ValueRO.Radius;
                        float3 pierceCenter = projectileTransform.ValueRO.Position;

                        // Chain lightning (evolved dark lightning): collect each freshly
                        // struck enemy so we can arc reduced damage to their neighbours
                        // after the hit loop (a nested CombatStats write here would alias).
                        bool isChain = SystemAPI.HasComponent<ChainLightningState>(projectileEntity);
                        ChainLightningState chain = isChain
                            ? SystemAPI.GetComponent<ChainLightningState>(projectileEntity)
                            : default;
                        NativeList<float3> chainSources = isChain ? new NativeList<float3>(Allocator.Temp) : default;

                        foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                                     .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                                     .WithAll<EnemyTag>()
                                     .WithEntityAccess())
                        {
                            float3 enemyPos = enemyTransform.ValueRO.Position;
                            if (math.lengthsq(enemyPos - pierceCenter) > pierceRadiusSq)
                            {
                                continue;
                            }

                            bool alreadyHit = false;
                            for (int h = 0; h < pierced.Length; h++)
                            {
                                if (pierced[h].Value == enemyEntity) { alreadyHit = true; break; }
                            }
                            if (alreadyHit)
                            {
                                continue;
                            }
                            pierced.Add(new PierceHitElement { Value = enemyEntity });

                            CombatStats pierceEnemy = enemyStats.ValueRO;
                            float pierceDamage = projectile.ValueRO.Damage;
                            if (hasPlayer)
                            {
                                CombatStats playerForCrit = playerStatsRef.ValueRO;
                                if (playerForCrit.CritChance > 0f && playerForCrit.CritMultiplier > 1f &&
                                    _critRng.NextFloat() < playerForCrit.CritChance)
                                {
                                    pierceDamage *= playerForCrit.CritMultiplier;
                                }
                            }
                            pierceEnemy.CurrentHealth = math.max(0f, pierceEnemy.CurrentHealth - pierceDamage);
                            enemyStats.ValueRW = pierceEnemy;

                            if (SystemAPI.HasSingleton<StatusEffectConfig>())
                            {
                                QueueStatusOnHit(enemyEntity, SystemAPI.HasComponent<BossTag>(enemyEntity));
                            }

                            NightDashCombatEvents.FireEnemyDamaged(enemyPos, pierceDamage);
                            if (isChain) chainSources.Add(enemyPos);

                            if (pierceEnemy.CurrentHealth <= 0f && !CombatHelpers.ContainsEntity(deadEnemies, enemyEntity))
                            {
                                deadEnemies.Add(enemyEntity);
                                deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(enemyEntity) ? 1 : 0));
                                deadEnemyPositions.Add(enemyPos);
                            }
                        }

                        // Arc reduced damage from each struck enemy to nearby (non-pierced)
                        // enemies — the VFX bridge auto-spawns a hit spark on each as its
                        // health drops, reading as a chain-lightning crackle.
                        if (isChain)
                        {
                            float arcRadiusSq = chain.Radius * chain.Radius;
                            float arcDamage = projectile.ValueRO.Damage * chain.Factor;
                            for (int si = 0; si < chainSources.Length; si++)
                            {
                                float3 src = chainSources[si];
                                foreach (var (arcTransform, arcStats, arcEntity) in SystemAPI
                                             .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                                             .WithAll<EnemyTag>()
                                             .WithEntityAccess())
                                {
                                    float3 arcPos = arcTransform.ValueRO.Position;
                                    float ad = math.lengthsq(arcPos - src);
                                    if (ad > arcRadiusSq || ad < 0.04f) continue; // out of arc range / the source itself

                                    bool wasPierced = false;
                                    for (int h = 0; h < pierced.Length; h++)
                                    {
                                        if (pierced[h].Value == arcEntity) { wasPierced = true; break; }
                                    }
                                    if (wasPierced) continue; // already took the full bolt hit

                                    CombatStats arcEnemy = arcStats.ValueRO;
                                    arcEnemy.CurrentHealth = math.max(0f, arcEnemy.CurrentHealth - arcDamage);
                                    arcStats.ValueRW = arcEnemy;
                                    NightDashCombatEvents.FireEnemyDamaged(arcPos, arcDamage);
                                    NightDashCombatEvents.FireChainArc(src, arcPos); // visible lightning arc

                                    if (arcEnemy.CurrentHealth <= 0f && !CombatHelpers.ContainsEntity(deadEnemies, arcEntity))
                                    {
                                        deadEnemies.Add(arcEntity);
                                        deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(arcEntity) ? 1 : 0));
                                        deadEnemyPositions.Add(arcPos);
                                    }
                                }
                            }
                            chainSources.Dispose();
                        }

                        continue;
                    }

                    projectile.ValueRW.TickTimer -= dt;
                    if (projectile.ValueRO.TickTimer > 0f)
                    {
                        continue;
                    }
                    // Reset to a full interval (discard any accumulated debt)
                    // so a frame spike can't chain several ticks in a row.
                    projectile.ValueRW.TickTimer = projectile.ValueRO.TickInterval;

                    float tickRadiusSq = projectile.ValueRO.Radius * projectile.ValueRO.Radius;
                    float knockback = projectile.ValueRO.Knockback;
                    float3 center = projectileTransform.ValueRO.Position;

                    foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                                 .Query<RefRW<LocalTransform>, RefRW<CombatStats>>()
                                 .WithAll<EnemyTag>()
                                 .WithEntityAccess())
                    {
                        float3 enemyPos = enemyTransform.ValueRO.Position;
                        float distanceSq = math.lengthsq(enemyPos - center);
                        if (distanceSq > tickRadiusSq)
                        {
                            continue;
                        }

                        CombatStats tickEnemy = enemyStats.ValueRO;
                        float tickDamage = projectile.ValueRO.Damage;
                        if (hasPlayer)
                        {
                            CombatStats playerForCrit = playerStatsRef.ValueRO;
                            if (playerForCrit.CritChance > 0f && playerForCrit.CritMultiplier > 1f &&
                                _critRng.NextFloat() < playerForCrit.CritChance)
                            {
                                tickDamage *= playerForCrit.CritMultiplier;
                            }
                        }
                        tickEnemy.CurrentHealth = math.max(0f, tickEnemy.CurrentHealth - tickDamage);
                        enemyStats.ValueRW = tickEnemy;

                        if (SystemAPI.HasSingleton<StatusEffectConfig>())
                        {
                            QueueStatusOnHit(enemyEntity, SystemAPI.HasComponent<BossTag>(enemyEntity));
                        }

                        if (knockback > 0f && distanceSq > 0.0001f)
                        {
                            float3 pushDir;
                            if (SystemAPI.HasComponent<OrbitState>(projectileEntity))
                            {
                                OrbitState ob = SystemAPI.GetComponent<OrbitState>(projectileEntity);
                                if (ob.Radius > 0.1f)
                                {
                                    // Orbiting blade: shove along its rotation (tangential) direction.
                                    float s = ob.AngularSpeed >= 0f ? 1f : -1f;
                                    pushDir = new float3(-math.sin(ob.Angle) * s, math.cos(ob.Angle) * s, 0f);
                                }
                                else
                                {
                                    pushDir = math.normalize(enemyPos - center); // centered barrier → push outward
                                }
                            }
                            else
                            {
                                pushDir = math.normalize(enemyPos - center);
                            }
                            enemyTransform.ValueRW.Position += pushDir * (knockback * 0.12f);
                        }

                        NightDashCombatEvents.FireEnemyDamaged(enemyPos, tickDamage);

                        if (tickEnemy.CurrentHealth <= 0f && !CombatHelpers.ContainsEntity(deadEnemies, enemyEntity))
                        {
                            deadEnemies.Add(enemyEntity);
                            deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(enemyEntity) ? 1 : 0));
                            deadEnemyPositions.Add(enemyPos);
                        }
                    }

                    continue;
                }

                // Bounce / ricochet (evolved demon orb / 심연파열구): on contact damage
                // ONE enemy, then redirect toward the next nearest enemy not yet hit,
                // until Remaining hits run out or none is within Range. Tracked via the
                // PierceHitElement buffer so it never bounces back to the same enemy.
                if (projectile.ValueRO.Behavior == (byte)ProjectileBehavior.Linear &&
                    SystemAPI.HasComponent<BounceState>(projectileEntity))
                {
                    BounceState bounce = SystemAPI.GetComponent<BounceState>(projectileEntity);
                    bool hasHitList = SystemAPI.HasBuffer<PierceHitElement>(projectileEntity);
                    DynamicBuffer<PierceHitElement> hitList = hasHitList
                        ? SystemAPI.GetBuffer<PierceHitElement>(projectileEntity)
                        : default;
                    float bounceRadiusSq = projectile.ValueRO.Radius * projectile.ValueRO.Radius;
                    float3 orbPos = projectileTransform.ValueRO.Position;

                    bool didHit = false;
                    foreach (var (enemyTransform, enemyStats, enemyEntity) in SystemAPI
                                 .Query<RefRO<LocalTransform>, RefRW<CombatStats>>()
                                 .WithAll<EnemyTag>()
                                 .WithEntityAccess())
                    {
                        float3 enemyPos = enemyTransform.ValueRO.Position;
                        if (math.lengthsq(enemyPos - orbPos) > bounceRadiusSq)
                        {
                            continue;
                        }
                        if (hasHitList)
                        {
                            bool seen = false;
                            for (int h = 0; h < hitList.Length; h++)
                            {
                                if (hitList[h].Value == enemyEntity) { seen = true; break; }
                            }
                            if (seen) continue;
                        }

                        CombatStats bounceEnemy = enemyStats.ValueRO;
                        float bounceDamage = projectile.ValueRO.Damage;
                        if (hasPlayer)
                        {
                            CombatStats playerForCrit = playerStatsRef.ValueRO;
                            if (playerForCrit.CritChance > 0f && playerForCrit.CritMultiplier > 1f &&
                                _critRng.NextFloat() < playerForCrit.CritChance)
                            {
                                bounceDamage *= playerForCrit.CritMultiplier;
                            }
                        }
                        bounceEnemy.CurrentHealth = math.max(0f, bounceEnemy.CurrentHealth - bounceDamage);
                        enemyStats.ValueRW = bounceEnemy;
                        if (hasHitList) hitList.Add(new PierceHitElement { Value = enemyEntity });

                        if (SystemAPI.HasSingleton<StatusEffectConfig>())
                        {
                            QueueStatusOnHit(enemyEntity, SystemAPI.HasComponent<BossTag>(enemyEntity));
                        }
                        NightDashCombatEvents.FireEnemyDamaged(enemyPos, bounceDamage);
                        if (bounceEnemy.CurrentHealth <= 0f && !CombatHelpers.ContainsEntity(deadEnemies, enemyEntity))
                        {
                            deadEnemies.Add(enemyEntity);
                            deadEnemyBossFlags.Add((byte)(SystemAPI.HasComponent<BossTag>(enemyEntity) ? 1 : 0));
                            deadEnemyPositions.Add(enemyPos);
                        }
                        didHit = true;
                        break;
                    }

                    if (didHit)
                    {
                        bounce.Remaining -= 1;
                        if (bounce.Remaining <= 0)
                        {
                            ecb.DestroyEntity(projectileEntity);
                            continue;
                        }

                        // Redirect toward the nearest not-yet-hit enemy within Range.
                        Entity nextTarget = Entity.Null;
                        float bestSq = bounce.Range * bounce.Range;
                        float3 nextPos = float3.zero;
                        foreach (var (nt, ne) in SystemAPI
                                     .Query<RefRO<LocalTransform>>()
                                     .WithAll<EnemyTag>()
                                     .WithEntityAccess())
                        {
                            if (hasHitList)
                            {
                                bool seen = false;
                                for (int h = 0; h < hitList.Length; h++)
                                {
                                    if (hitList[h].Value == ne) { seen = true; break; }
                                }
                                if (seen) continue;
                            }
                            float3 ep = nt.ValueRO.Position;
                            float dsq = math.lengthsq(ep - orbPos);
                            if (dsq < bestSq)
                            {
                                bestSq = dsq;
                                nextTarget = ne;
                                nextPos = ep;
                            }
                        }

                        if (nextTarget != Entity.Null)
                        {
                            float spd = math.length(velocity.ValueRO.Value);
                            if (spd < 0.01f) spd = 8f;
                            float2 ndir = math.normalize((nextPos - orbPos).xy);
                            velocity.ValueRW.Value = ndir * spd;
                            SystemAPI.SetComponent(projectileEntity, bounce);
                        }
                        else
                        {
                            ecb.DestroyEntity(projectileEntity);
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
            if (FlushStatusQueue(ref state))
            {
                // ApplyEffect performed EntityManager structural changes, which
                // invalidate EVERY component handle / singleton RefRW captured
                // earlier this frame. Re-acquire the ones still used below.
                loop = SystemAPI.GetSingletonRW<GameLoopState>();
                result = SystemAPI.GetSingletonRW<RunResultStats>();

                hasPlayer = false;
                playerStatsRef = default;
                foreach (var (stats, entity) in SystemAPI.Query<RefRW<CombatStats>>().WithAll<PlayerTag>().WithEntityAccess())
                {
                    playerStatsRef = stats;
                    playerEntity = entity;
                    hasPlayer = true;
                    break;
                }
            }

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

        // Returns true if it applied any effects (i.e. performed EntityManager
        // structural changes) — callers must re-acquire invalidated handles then.
        private bool FlushStatusQueue(ref SystemState state)
        {
            if (_statusQueueScratch == null || _statusQueueScratch.Count == 0) return false;
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
            return true;
        }
    }
}
