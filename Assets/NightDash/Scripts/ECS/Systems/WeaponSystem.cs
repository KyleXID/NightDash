using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;
using NightDash.Runtime;
using NightDash.Data;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial struct WeaponSystem : ISystem
    {
        // Drives random scatter for the evolved star-fall meteor shower. Static so
        // the static SpawnWeapon can advance it (OnUpdate is managed, not Burst).
        private static Unity.Mathematics.Random _skyfallRng =
            Unity.Mathematics.Random.CreateFromIndex(0x5EED_1234u);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<WeaponRuntimeData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing)
            {
                return;
            }

            // Pause: the pause menu (and any overlay) creates a GameplayPauseTag
            // entity. Freeze weapon firing while it exists so the simulation is
            // truly halted behind the menu / confirm dialog.
            if (SystemAPI.HasSingleton<GameplayPauseTag>())
            {
                return;
            }

            var registry = DataRegistry.Instance;
            if (registry?.Catalog == null)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
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

            Entity targetEnemy = Entity.Null;
            float nearestDistanceSq = float.MaxValue;
            float3 targetPosition = float3.zero;
            // Also collect every enemy position so multi-target weapons (e.g. the
            // evolved meteor shower) can land ON actual enemies instead of empty ground.
            var enemyPositions = new Unity.Collections.NativeList<float3>(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                float3 ePos = transform.ValueRO.Position;
                enemyPositions.Add(ePos);
                float distanceSq = math.lengthsq(ePos - playerPosition);
                if (distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                targetEnemy = entity;
                targetPosition = ePos;
            }

            // NOTE: do NOT early-return when there is no enemy. Player-centered
            // persistent weapons (light ring / spinning blades / barrier) must
            // keep orbiting the player even with no targets on screen. Target-
            // seeking weapons are skipped per-weapon inside SpawnWeapon instead.
            bool hasTarget = targetEnemy != Entity.Null;

            string classId = SystemAPI.GetSingleton<RunSelection>().ClassId.ToString();
            if (!registry.TryGetClass(classId, out ClassData classData) || classData == null)
            {
                return;
            }

            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            PlayerRuntimeProfile playerProfile = RuntimeBalanceUtility.ResolvePlayerRuntimeProfile(registry, classData, ownedPassives);

            // Difficulty modifier: cooldown multiplier (positive pct = longer cooldown).
            float cooldownMultiplier = 1f;
            if (SystemAPI.HasSingleton<DifficultyState>())
            {
                cooldownMultiplier = SystemAPI.GetSingleton<DifficultyState>().CooldownMultiplier;
            }
            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                float3 origin = transform.ValueRO.Position;
                int firingIndex = 0;

                for (int i = 0; i < ownedWeapons.Length; i++)
                {
                    OwnedWeaponElement ownedWeapon = ownedWeapons[i];
                    if (!registry.TryGetWeapon(ownedWeapon.Id.ToString(), out WeaponData weaponData) || weaponData == null)
                    {
                        continue;
                    }

                    WeaponRuntimeProfile profile = RuntimeBalanceUtility.ResolveWeaponRuntimeProfile(
                        weaponData,
                        math.max(1, ownedWeapon.Level),
                        playerProfile);

                    ownedWeapon.CooldownRemaining -= dt;
                    if (ownedWeapon.CooldownRemaining <= 0f)
                    {
                        bool fired = SpawnWeapon(
                            ref ecb,
                            origin,
                            targetPosition,
                            hasTarget,
                            profile,
                            firingIndex,
                            ownedWeapons.Length,
                            ownedWeapon.Id,
                            weaponData.weaponType,
                            targetEnemy,
                            enemyPositions);
                        if (fired)
                        {
                            // Persistent orbit weapons (ring/blades/barrier) spawn ONCE
                            // and never re-fire (CooldownRemaining parked at MaxValue) so
                            // they don't flicker. Everything else consumes its cooldown
                            // normally (target-seekers hold ready until an enemy appears).
                            ownedWeapon.CooldownRemaining = IsPersistentOrbit(ownedWeapon.Id)
                                ? float.MaxValue
                                : profile.Cooldown * cooldownMultiplier;
                            firingIndex += 1;
                        }
                    }

                    ownedWeapons[i] = ownedWeapon;
                }
            }

            enemyPositions.Dispose();
        }

        // Per-weapon in-game archetype. Default falls back to weaponType
        // (Melee vs straight projectile). Specific weapons override into
        // Vampire-Survivors-style behaviors. Evolved/abyss variants inherit
        // their base weapon's behavior.
        private enum WeaponBehaviorKind
        {
            Linear, Melee, MeleeSweep, SlashStrike, HammerSlam, Whip, Skyfall, PiercingBolt, OrbitRing, OrbitBlades, Barrier, GroundZone
        }

        // Orbit weapons that spawn once and persist (never re-fire) for the run.
        private static bool IsPersistentOrbit(FixedString64Bytes weaponId)
        {
            string baseId = weaponId.ToString();
            if (baseId.EndsWith("_evolved")) baseId = baseId.Substring(0, baseId.Length - "_evolved".Length);
            else if (baseId.EndsWith("_abyss")) baseId = baseId.Substring(0, baseId.Length - "_abyss".Length);
            // light_ring is NO LONGER persistent — it now fires spiralling ring
            // projectiles on cooldown (see OrbitRing case).
            return baseId == "weapon_spinning_blade"
                || baseId == "weapon_dark_barrier";
        }

        private static WeaponBehaviorKind ResolveBehavior(FixedString64Bytes weaponId, WeaponType type)
        {
            string id = weaponId.ToString();           // OnUpdate is managed (not Burst) — string ops are safe
            string baseId = id;
            if (baseId.EndsWith("_evolved")) baseId = baseId.Substring(0, baseId.Length - "_evolved".Length);
            else if (baseId.EndsWith("_abyss")) baseId = baseId.Substring(0, baseId.Length - "_abyss".Length);

            switch (baseId)
            {
                case "weapon_starfall":
                case "weapon_void_starfall": return WeaponBehaviorKind.Skyfall;
                case "weapon_dark_lightning": return WeaponBehaviorKind.PiercingBolt;
                case "weapon_light_ring":     return WeaponBehaviorKind.OrbitRing;
                case "weapon_spinning_blade": return WeaponBehaviorKind.OrbitBlades;
                case "weapon_dark_barrier":   return WeaponBehaviorKind.Barrier;
                case "weapon_abyss_tentacle": return WeaponBehaviorKind.GroundZone;
                case "weapon_slash_combo":    return WeaponBehaviorKind.SlashStrike;
                case "weapon_hell_hammer":    return WeaponBehaviorKind.HammerSlam;
                case "weapon_chain_scythe":   return WeaponBehaviorKind.Whip;
                case "weapon_demon_greatsword": return WeaponBehaviorKind.MeleeSweep;
            }
            return type == WeaponType.Melee ? WeaponBehaviorKind.Melee : WeaponBehaviorKind.Linear;
        }

        // Returns true if a weapon entity was actually spawned (consumes the
        // cooldown), false if it was skipped (e.g. a target-seeking weapon with
        // no enemy on screen — keep the cooldown ready).
        private static bool SpawnWeapon(
            ref EntityCommandBuffer ecb,
            float3 origin,
            float3 target,
            bool hasTarget,
            WeaponRuntimeProfile weapon,
            int weaponIndex,
            int weaponCount,
            FixedString64Bytes weaponId,
            WeaponType type,
            Entity targetEnemy,
            Unity.Collections.NativeList<float3> enemyPositions)
        {
            float3 direction3 = target - origin;
            direction3.z = 0f;
            if (math.lengthsq(direction3) <= 0.0001f)
            {
                direction3 = new float3(1f, 0f, 0f);
            }

            float2 direction = math.normalize(direction3.xy);
            float2 perpendicular = new float2(-direction.y, direction.x);
            float offsetFactor = weaponCount > 1 ? weaponIndex - ((weaponCount - 1) * 0.5f) : 0f;
            float2 spawnOffset = perpendicular * (0.32f * offsetFactor);

            // First-tier evolution ("..._evolved"): every evolved weapon gets a
            // BEHAVIOR upgrade over its base — more projectiles, pierce, wider
            // area, extra blades/zones, faster ticks. Raw stat boosts (damage /
            // speed) already live in the evolved WeaponData; this is the on-field
            // mechanical step-up the player feels. Abyss variants reuse the base
            // behavior for now (no dedicated tuning yet).
            bool evolved = weaponId.ToString().EndsWith("_evolved");

            // Orbit weapons (ring / blades / barrier) spawn ONCE and persist for
            // the whole run (the OnUpdate cooldown gate stops them re-firing), so
            // they orbit continuously instead of flickering back to angle 0 on
            // each respawn. RunTeardownBridge clears them at run end.
            const float persistentLife = 99999f;

            WeaponBehaviorKind kind = ResolveBehavior(weaponId, type);
            bool playerCentered = kind == WeaponBehaviorKind.OrbitRing
                                || kind == WeaponBehaviorKind.OrbitBlades
                                || kind == WeaponBehaviorKind.Barrier;
            if (!hasTarget && !playerCentered)
            {
                return false; // target-seeking weapon with no enemy — hold the cooldown
            }

            switch (kind)
            {
                case WeaponBehaviorKind.Skyfall:
                {
                    // Meteor: streaks DIAGONALLY in from the upper-right toward the
                    // target (the star's tail is drawn diagonal, so it must come from
                    // the upper-right). Spawns a good distance away so the descent
                    // reads, and keeps its drawn orientation (no velocity rotation).
                    // Evolved (void starfall): a meteor SHOWER, but capped by the live
                    // enemy count and ONE meteor per DISTINCT enemy. 1 enemy on field →
                    // exactly 1 meteor, even when evolved. First meteor = initial target.
                    const float DiagDist = 3.0f; // up-right offset from the target
                    const float FallSpeed = 7f;
                    int enemyCount = math.max(1, enemyPositions.Length);
                    int meteors = math.min(evolved ? 3 : 1, enemyCount);
                    float splashRadius = evolved ? 2.4f : 1.8f;
                    float splashFactor = evolved ? 0.6f : 0.5f;
                    // Track already-targeted enemy positions so each meteor hits a different one.
                    float3 usedA = target, usedB = new float3(float.MaxValue, float.MaxValue, 0f);
                    for (int m = 0; m < meteors; m++)
                    {
                        float3 aimPos;
                        if (m == 0)
                        {
                            aimPos = new float3(target.x, target.y, 0f);
                        }
                        else
                        {
                            // Pick a random enemy distinct from the ones already targeted.
                            aimPos = target;
                            for (int tries = 0; tries < 12; tries++)
                            {
                                float3 cand = enemyPositions[_skyfallRng.NextInt(0, enemyPositions.Length)];
                                if (math.lengthsq(cand.xy - usedA.xy) < 0.01f) continue;
                                if (m >= 2 && math.lengthsq(cand.xy - usedB.xy) < 0.01f) continue;
                                aimPos = new float3(cand.x, cand.y, 0f);
                                break;
                            }
                            if (m == 1) usedB = aimPos;
                        }
                        float3 spawnPos = new float3(aimPos.x + DiagDist + spawnOffset.x, aimPos.y + DiagDist + spawnOffset.y, 0f);
                        float2 dir = math.normalize(new float2(aimPos.x - spawnPos.x, aimPos.y - spawnPos.y));
                        // Lifetime = exact travel time to the target so the (non-looping)
                        // VFX animation finishes right as the star lands.
                        float travelTime = math.distance(new float2(spawnPos.x, spawnPos.y), new float2(aimPos.x, aimPos.y)) / FallSpeed;
                        Entity e = ecb.CreateEntity();
                        ecb.AddComponent(e, LocalTransform.FromPosition(spawnPos));
                        ecb.AddComponent(e, new ProjectileData
                        {
                            Damage = weapon.Damage,
                            Lifetime = math.max(0.2f, travelTime),
                            IsPlayerOwned = 1,
                            Radius = 0f,          // no mid-flight hit — damage is dealt as a landing AoE
                            WeaponId = weaponId,
                            IsMelee = 0,
                            Behavior = (byte)ProjectileBehavior.Linear,
                            AlignToVelocity = 0,  // keep the star's drawn diagonal tail orientation
                            PlayOnce = 1,         // play the fall animation once, finishing on impact
                            SplashRadius = splashRadius, // landing AoE: full to the target, reduced to nearby
                            SplashFactor = splashFactor,
                        });
                        ecb.AddComponent(e, new PhysicsVelocity2D { Value = dir * FallSpeed });
                    }
                    break;
                }
                case WeaponBehaviorKind.PiercingBolt:
                {
                    // Lightning: emitted from the player TOWARD the target, travels
                    // slowly, and keeps a FIXED vertical sprite orientation (never
                    // rotates to lie flat). Pierces every enemy in its path, lingers.
                    // Evolved (hell thunder): three bolts struck SIMULTANEOUSLY, each
                    // aimed at a different enemy (not a single-direction fan).
                    const float BoltSpeed = 2.5f;
                    int bolts = evolved ? 3 : 1;
                    float boltRadius = evolved ? 0.75f : 0.6f;
                    float boltTick = evolved ? 0.1f : 0.15f;
                    for (int b = 0; b < bolts; b++)
                    {
                        // First bolt → the nearest target; the rest → other random enemies.
                        float2 bdir = direction;
                        if (b > 0 && enemyPositions.Length > 0)
                        {
                            float3 pick = enemyPositions[_skyfallRng.NextInt(0, enemyPositions.Length)];
                            float2 toEnemy = new float2(pick.x - origin.x, pick.y - origin.y);
                            if (math.lengthsq(toEnemy) > 0.0001f) bdir = math.normalize(toEnemy);
                        }
                        Entity e = ecb.CreateEntity();
                        ecb.AddComponent(e, LocalTransform.FromPosition(origin + new float3(spawnOffset.x, spawnOffset.y, 0f)));
                        ecb.AddComponent(e, new ProjectileData
                        {
                            Damage = weapon.Damage,
                            Lifetime = evolved ? 1.5f : 1.4f,
                            IsPlayerOwned = 1,
                            Radius = boltRadius,
                            WeaponId = weaponId,
                            IsMelee = 0,
                            Behavior = (byte)ProjectileBehavior.Linear,
                            TickInterval = boltTick, // pierce marker (Linear + tick>0 → hit each enemy once)
                            TickTimer = boltTick,
                            AlignToVelocity = 0,  // keep the bolt sprite upright — never rotate it to horizontal
                        });
                        ecb.AddComponent(e, new PhysicsVelocity2D { Value = bdir * BoltSpeed });
                        ecb.AddBuffer<PierceHitElement>(e); // reliable pierce: track already-hit enemies
                        if (evolved)
                        {
                            // Chain lightning: each struck enemy arcs reduced damage to nearby ones.
                            ecb.AddComponent(e, new ChainLightningState { Radius = 2.8f, Factor = 0.5f });
                            // The FIRST bolt HOMES on the initial target; the rest fly straight.
                            if (b == 0 && targetEnemy != Entity.Null)
                            {
                                ecb.AddComponent(e, new HomingState { Target = targetEnemy, TurnSpeed = 3.0f });
                            }
                        }
                    }
                    break;
                }
                case WeaponBehaviorKind.OrbitRing:
                {
                    // Light ring: small rings that SPIRAL OUTWARD from the caster
                    // (no longer a static protective aura). Each "arm" orbits the
                    // player while its radius grows, tracing a spiral, then expires.
                    // Evolved: more arms, spinning faster and reaching farther.
                    // Tuned DOWN from the first pass (was overpowered: too many arms
                    // sweeping the whole screen with fast multi-hit ticks). Fewer arms,
                    // smaller hitbox, slower tick cadence, and they start further out so
                    // they don't melt enemies hugging the player.
                    int arms = evolved ? 3 : 2;
                    const float ringLife = 2.4f;
                    const float startRadius = 0.6f;
                    float growth = evolved ? 5.0f : 4.2f;  // world units/sec the ring travels outward (faster = less dwell per enemy)
                    // Lower spin → the rings travel farther per revolution, so the spiral
                    // coils are spaced WIDER apart (less dense trail).
                    float spin = evolved ? 2.6f : 2.0f;    // rad/sec spin (spiral tightness)
                    for (int a = 0; a < arms; a++)
                    {
                        float ang = (2f * math.PI / arms) * a;
                        // hitOnce: each arm damages an enemy once as it sweeps past
                        // (reliable — a tick cadence skips enemies at the high tangential
                        // speed a spiral reaches at large radius). tick value is just the
                        // gate into the hit path; hit-once ignores its cadence.
                        CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, ringLife,
                            radius: startRadius, angularSpeed: spin, angle: ang, hitRadius: 0.65f,
                            tick: 0.2f, knockback: 0f, centerYOffset: 0.5f, radiusGrowth: growth, hitOnce: true);
                    }
                    break;
                }
                case WeaponBehaviorKind.OrbitBlades:
                {
                    // Blades orbiting the player on opposite sides. Evolved (abyssal
                    // vortex): twice the blades, faster spin and heavier knockback.
                    int blades = evolved ? 4 : 2;
                    float bladeSpeed = evolved ? 4.6f : 3.6f;
                    float bladeKnock = evolved ? 5f : 3f;
                    float bladeTick = evolved ? 0.18f : 0.25f;
                    for (int b = 0; b < blades; b++)
                    {
                        float a = (2f * math.PI / blades) * b;
                        CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                            radius: 1.8f, angularSpeed: bladeSpeed, angle: a, hitRadius: 0.7f,
                            tick: bladeTick, knockback: bladeKnock, centerYOffset: 0.5f);
                    }
                    break;
                }
                case WeaponBehaviorKind.Barrier:
                {
                    // Protective shield centered on the player (slightly raised); knocks enemies back on contact.
                    // Evolved (tainted sanctuary): larger guard radius, harder knockback, faster pulses.
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                        radius: 0f, angularSpeed: 1.2f, angle: 0f,
                        hitRadius: evolved ? 1.4f : 1.0f, tick: evolved ? 0.22f : 0.3f,
                        knockback: evolved ? 10f : 6f, centerYOffset: 0.5f);
                    break;
                }
                case WeaponBehaviorKind.GroundZone:
                {
                    // Erupts from the ground at the target spot, lingers and damages on contact.
                    // Evolved (great abyss): three tentacle zones around the target,
                    // bigger radius and a longer, faster-ticking dwell.
                    int zones = evolved ? 3 : 1;
                    float zoneRadius = evolved ? 1.3f : 1.1f;
                    float zoneLife = evolved ? 3.0f : 2.5f;
                    float zoneTick = evolved ? 0.3f : 0.4f;
                    for (int z = 0; z < zones; z++)
                    {
                        float2 scatter = zones > 1 ? perpendicular * ((z - (zones - 1) * 0.5f) * 1.2f) : float2.zero;
                        float3 spawnPos = new float3(target.x + scatter.x, target.y + scatter.y, 0f);
                        Entity e = ecb.CreateEntity();
                        ecb.AddComponent(e, LocalTransform.FromPosition(spawnPos));
                        ecb.AddComponent(e, new ProjectileData
                        {
                            Damage = weapon.Damage,
                            Lifetime = zoneLife,
                            IsPlayerOwned = 1,
                            Radius = zoneRadius,
                            WeaponId = weaponId,
                            IsMelee = 0,
                            Behavior = (byte)ProjectileBehavior.GroundZone,
                            TickInterval = zoneTick,
                            TickTimer = zoneTick,
                        });
                        ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
                    }
                    break;
                }
                case WeaponBehaviorKind.MeleeSweep:
                {
                    // Swings an arc AROUND the player, centered on the aim direction,
                    // over a short lifetime — pierces, never consumed by a hit.
                    // Evolved (hellfire arc): longer reach, wider/faster swing,
                    // bigger hit radius and knockback.
                    string sweepId = weaponId.ToString();
                    // Greatsword's blade art reads opposite the others, so it swings
                    // the other way (clockwise) to match its sprite.
                    float dir = sweepId.Contains("demon_greatsword") ? -1f : 1f;
                    // Chain scythe sweeps closer to the player's body.
                    float reachMul = sweepId.Contains("chain_scythe") ? 0.3f : 0.55f;
                    if (evolved) reachMul += 0.15f;
                    float reach = sweepId.Contains("chain_scythe")
                        ? math.max(0.6f, weapon.Range * reachMul)
                        : math.max(1.1f, weapon.Range * reachMul);
                    float aim = math.atan2(direction.y, direction.x);
                    float sweepLife = evolved ? 0.55f : 0.5f;
                    float sweepSpeed = evolved ? 7.5f : 6.3f; // rad/s → wider arc when evolved
                    float startAngle = aim - dir * sweepSpeed * sweepLife * 0.5f; // center the sweep on the aim
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, sweepLife,
                        radius: reach, angularSpeed: dir * sweepSpeed, angle: startAngle,
                        hitRadius: math.max(0.7f, weapon.Range * (evolved ? 0.4f : 0.3f)),
                        tick: evolved ? 0.06f : 0.08f, knockback: evolved ? 4f : 0f);
                    break;
                }
                case WeaponBehaviorKind.Whip:
                {
                    // Chain scythe: shoots out from the player toward the target,
                    // DWELLS at full reach, then snaps back like a rubber band,
                    // damaging everything along the path (extend → hold → retract
                    // over the lifetime; the phase split lives in CombatSystem). Pierces.
                    // Stays a single scythe head + a chain of 5 randomly-mixed links
                    // (rendered in the visual bridge). Evolved: longer reach, wider
                    // hit, faster pierce cadence and knockback on the chain.
                    float maxReach = math.max(1.8f, weapon.Range * (evolved ? 1.05f : 0.8f));
                    float whipLife = evolved ? 1.1f : 1.0f; // dwell at full reach is visible
                    float whipTick = evolved ? 0.045f : 0.06f;
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(origin)); // starts at the player (distance 0)
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = whipLife,
                        IsPlayerOwned = 1,
                        Radius = math.max(0.7f, weapon.Range * (evolved ? 0.34f : 0.25f)),
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.Whip,
                        TickInterval = whipTick, // pierce: repeatedly damages enemies along the path
                        TickTimer = whipTick,
                        Knockback = evolved ? 3f : 0f,
                        AlignToVelocity = 0,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
                    ecb.AddComponent(e, new WhipState { Direction = direction, MaxReach = maxReach, TotalLifetime = whipLife });
                    break;
                }
                case WeaponBehaviorKind.SlashStrike:
                {
                    // Continuous slashing: when an enemy is within range, the slash
                    // effect spawns ON the enemy (stationary) and rapidly multi-hits
                    // for a moment. Skips (holds cooldown) if no enemy is close.
                    // Evolved (frenzy flurry): bigger reach + radius, faster multi-hit.
                    float distToTarget = math.length(target - origin);
                    float strikeRange = math.max(2.5f, weapon.Range);
                    if (distToTarget > strikeRange)
                    {
                        return false; // nearest enemy is out of slashing range
                    }
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(new float3(target.x, target.y, 0f)));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = evolved ? 0.55f : 0.4f,
                        IsPlayerOwned = 1,
                        Radius = evolved ? 1.0f : 0.7f,
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.GroundZone, // stays on the spot it struck
                        TickInterval = evolved ? 0.06f : 0.1f, // rapid multi-hit ("난도질"), never consumed
                        TickTimer = evolved ? 0.06f : 0.1f,
                        AlignToVelocity = 0,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
                    break;
                }
                case WeaponBehaviorKind.HammerSlam:
                {
                    // Heavy bludgeon: like SlashStrike (lands ON a nearby enemy) but
                    // SLOW and WEIGHTY — wide impact, knockback, few hard hits.
                    // (Contrast vs SlashStrike: bigger radius, slower tick, knockback.)
                    // Evolved (doom smash): much wider crater, heavier knockback.
                    float distToTarget = math.length(target - origin);
                    float strikeRange = math.max(2.5f, weapon.Range);
                    if (distToTarget > strikeRange)
                    {
                        return false; // nearest enemy is out of slam range
                    }
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(new float3(target.x, target.y, 0f)));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = evolved ? 0.55f : 0.45f,
                        IsPlayerOwned = 1,
                        Radius = evolved ? 1.9f : 1.3f, // wide, heavy impact area
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.GroundZone, // stays on the spot it struck
                        TickInterval = evolved ? 0.3f : 0.35f, // slow, weighty cadence (few hard hits)
                        TickTimer = evolved ? 0.3f : 0.35f,
                        Knockback = evolved ? 10f : 6f,       // 묵직: knocks enemies back
                        AlignToVelocity = 0,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
                    break;
                }
                case WeaponBehaviorKind.Melee:
                {
                    float meleeOffset = math.min(weapon.Range * 0.4f, 1.2f);
                    float3 meleePosition = origin + new float3(direction.x * meleeOffset, direction.y * meleeOffset, 0f);
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(meleePosition));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = 0.25f,
                        IsPlayerOwned = 1,
                        Radius = math.max(0.6f, weapon.Range * (evolved ? 0.5f : 0.35f)),
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.Linear,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = direction * 1.5f });
                    break;
                }
                default: // Linear straight projectile
                {
                    // Base: a single straight shot consumed on first hit. Evolved
                    // gives each ranged weapon a distinct upgrade — multi-shot
                    // spread (rapid/split) and/or pierce (shadow/spear/orb/wave/…).
                    ResolveLinearEvolution(weaponId, evolved, out int shots, out float spreadDeg, out float linRadius, out float pierceTick, out int bounces, out float dmgFactor, out float lifeFactor);
                    float spread = math.radians(spreadDeg);
                    float speed = math.max(2f, weapon.ProjectileSpeed);
                    // Bouncing orbs need to live long enough to ricochet; otherwise range
                    // is scaled by lifeFactor (shotgun = short, revolver = long).
                    float life = bounces > 0 ? 3.5f : math.max(0.2f, (weapon.Range / math.max(1f, weapon.ProjectileSpeed)) * lifeFactor);
                    float shotDamage = weapon.Damage * dmgFactor;
                    for (int s = 0; s < shots; s++)
                    {
                        float a = shots > 1 ? (s - (shots - 1) * 0.5f) * (spread / math.max(1, shots - 1)) : 0f;
                        float2 sdir = Rotate2(direction, a);
                        Entity e = ecb.CreateEntity();
                        ecb.AddComponent(e, LocalTransform.FromPosition(origin + new float3(spawnOffset.x, spawnOffset.y, 0f)));
                        ecb.AddComponent(e, new ProjectileData
                        {
                            Damage = shotDamage,
                            Lifetime = life,
                            IsPlayerOwned = 1,
                            Radius = linRadius,
                            WeaponId = weaponId,
                            IsMelee = 0,
                            Behavior = (byte)ProjectileBehavior.Linear,
                            TickInterval = pierceTick, // >0 = pierce (hit each enemy once); 0 = single hit / bounce
                            TickTimer = pierceTick,
                            AlignToVelocity = 1, // bullets/arrows/spears face their travel direction
                        });
                        ecb.AddComponent(e, new PhysicsVelocity2D { Value = sdir * speed });
                        if (pierceTick > 0f)
                        {
                            ecb.AddBuffer<PierceHitElement>(e); // reliable pierce: track already-hit enemies
                        }
                        if (bounces > 0)
                        {
                            // Ricochet: hit one enemy, then redirect to the next nearest un-hit one.
                            ecb.AddComponent(e, new BounceState { Remaining = bounces, Range = 5.5f });
                            ecb.AddBuffer<PierceHitElement>(e); // track already-hit enemies (no repeats)
                        }
                    }
                    break;
                }
            }

            return true;
        }

        // Per-weapon config for the straight-projectile (Linear) family. Split-bullet
        // and revolver are differentiated even at BASE tier (shotgun vs precise single);
        // the rest stay a plain single shot until evolved. pierceTick > 0 = piercing
        // shot; damageFactor/lifeFactor scale per-pellet damage and range.
        private static void ResolveLinearEvolution(
            FixedString64Bytes weaponId, bool evolved,
            out int shots, out float spreadDeg, out float radius, out float pierceTick, out int bounces,
            out float damageFactor, out float lifeFactor)
        {
            shots = 1; spreadDeg = 0f; radius = 0.35f; pierceTick = 0f; bounces = 0;
            damageFactor = 1f; lifeFactor = 1f;

            string baseId = weaponId.ToString();
            if (baseId.EndsWith("_evolved")) baseId = baseId.Substring(0, baseId.Length - "_evolved".Length);

            switch (baseId)
            {
                // SHOTGUN — many weak pellets, wide spread, SHORT range (always, so it
                // differs from the revolver at base). Evolved = more pellets, wider.
                case "weapon_split_bullet":
                    shots = evolved ? 5 : 3; spreadDeg = evolved ? 58f : 40f;
                    radius = 0.4f; damageFactor = 0.5f; lifeFactor = 0.55f;
                    break;
                // REVOLVER — single PRECISE hard-hitting bullet, LONG range. Evolved pierces.
                case "weapon_revolver":
                    shots = 1; radius = 0.32f; damageFactor = 1.25f; lifeFactor = 1.35f;
                    if (evolved) pierceTick = 0.08f;
                    break;
                // The rest are a plain single shot until evolved.
                case "weapon_rapid_shot":   if (evolved) { shots = 3; spreadDeg = 24f; radius = 0.45f; } break; // storm triple-arrow
                case "weapon_shadow_arrow": if (evolved) { radius = 0.50f; pierceTick = 0.10f; } break;         // void-piercing arrow
                case "weapon_spear":        if (evolved) { radius = 0.55f; pierceTick = 0.10f; } break;         // dimension-piercing spear
                case "weapon_demon_orb":    if (evolved) { radius = 0.55f; bounces = 5; } break;                // abyssal orb — ricochets
                case "weapon_holy_wave":    if (evolved) { radius = 0.85f; pierceTick = 0.10f; } break;         // golden sacred wave
                default:                    if (evolved) { radius = 0.40f; pierceTick = 0.10f; } break;         // generic: pierce
            }
        }

        // Rotates a 2D vector by `radians` (CCW).
        private static float2 Rotate2(float2 v, float radians)
        {
            float c = math.cos(radians);
            float s = math.sin(radians);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Creates one orbiting weapon entity attached to the player. The orbit
        // center is recomputed each frame by CombatSystem from the live player
        // position. radius 0 = centered on the player (ring / barrier).
        private static void CreateOrbit(
            ref EntityCommandBuffer ecb,
            float3 playerPos,
            FixedString64Bytes weaponId,
            float damage,
            float lifetime,
            float radius,
            float angularSpeed,
            float angle,
            float hitRadius,
            float tick,
            float knockback,
            float centerYOffset = 0f,
            float radiusGrowth = 0f,
            bool hitOnce = false)
        {
            Entity e = ecb.CreateEntity();
            float3 pos = playerPos + new float3(math.cos(angle) * radius, centerYOffset + math.sin(angle) * radius, 0f);
            ecb.AddComponent(e, LocalTransform.FromPosition(pos));
            ecb.AddComponent(e, new ProjectileData
            {
                Damage = damage,
                Lifetime = lifetime,
                IsPlayerOwned = 1,
                Radius = hitRadius,
                WeaponId = weaponId,
                IsMelee = 0,
                Behavior = (byte)ProjectileBehavior.Orbit,
                TickInterval = tick,
                TickTimer = tick,
                Knockback = knockback,
            });
            ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
            ecb.AddComponent(e, new OrbitState { Radius = radius, AngularSpeed = angularSpeed, Angle = angle, CenterYOffset = centerYOffset, RadiusGrowth = radiusGrowth });
            if (hitOnce)
            {
                // Spiralling sweep that hits each enemy once (reliable, balanced) —
                // see CombatSystem's hit-once branch.
                ecb.AddBuffer<PierceHitElement>(e);
            }
        }
    }
}
