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
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                float distanceSq = math.lengthsq(transform.ValueRO.Position - playerPosition);
                if (distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                targetEnemy = entity;
                targetPosition = transform.ValueRO.Position;
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
                            weaponData.weaponType);
                        if (fired)
                        {
                            // Only consume the cooldown when the weapon actually
                            // fired (target-seeking weapons hold ready until an
                            // enemy appears).
                            ownedWeapon.CooldownRemaining = profile.Cooldown * cooldownMultiplier;
                            firingIndex += 1;
                        }
                    }

                    ownedWeapons[i] = ownedWeapon;
                }
            }
        }

        // Per-weapon in-game archetype. Default falls back to weaponType
        // (Melee vs straight projectile). Specific weapons override into
        // Vampire-Survivors-style behaviors. Evolved/abyss variants inherit
        // their base weapon's behavior.
        private enum WeaponBehaviorKind
        {
            Linear, Melee, MeleeArc, Skyfall, OrbitRing, OrbitBlades, Barrier, GroundZone
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
                case "weapon_void_starfall":
                case "weapon_dark_lightning": return WeaponBehaviorKind.Skyfall;
                case "weapon_light_ring":     return WeaponBehaviorKind.OrbitRing;
                case "weapon_spinning_blade": return WeaponBehaviorKind.OrbitBlades;
                case "weapon_dark_barrier":   return WeaponBehaviorKind.Barrier;
                case "weapon_abyss_tentacle": return WeaponBehaviorKind.GroundZone;
                case "weapon_chain_scythe":   return WeaponBehaviorKind.MeleeArc;
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
            WeaponType type)
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

            // Persistent (orbit / zone) weapons live ~one cooldown so the next
            // cast seamlessly replaces them instead of stacking indefinitely.
            float persistentLife = math.max(0.5f, weapon.Cooldown) * 1.1f;

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
                    // Falls straight down from above the target enemy (meteor / lightning strike).
                    const float SkyHeight = 6f;
                    const float FallSpeed = 16f;
                    float3 spawnPos = new float3(target.x + spawnOffset.x, target.y + SkyHeight, 0f);
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(spawnPos));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = (SkyHeight / FallSpeed) + 0.4f,
                        IsPlayerOwned = 1,
                        Radius = 0.7f,
                        WeaponId = weaponId,
                        IsMelee = 0,
                        Behavior = (byte)ProjectileBehavior.Linear,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = new float2(0f, -FallSpeed) });
                    break;
                }
                case WeaponBehaviorKind.OrbitRing:
                {
                    // Big ring centered on the player; damages enemies within the ring radius.
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                        radius: 0f, angularSpeed: 1.6f, angle: 0f, hitRadius: 2.4f, tick: 0.4f, knockback: 0f);
                    break;
                }
                case WeaponBehaviorKind.OrbitBlades:
                {
                    // Two blades orbiting the player on opposite sides.
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                        radius: 1.8f, angularSpeed: 3.6f, angle: 0f, hitRadius: 0.7f, tick: 0.25f, knockback: 0f);
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                        radius: 1.8f, angularSpeed: 3.6f, angle: math.PI, hitRadius: 0.7f, tick: 0.25f, knockback: 0f);
                    break;
                }
                case WeaponBehaviorKind.Barrier:
                {
                    // Protective shield centered on the player; knocks enemies back on contact.
                    CreateOrbit(ref ecb, origin, weaponId, weapon.Damage, persistentLife,
                        radius: 0f, angularSpeed: 1.2f, angle: 0f, hitRadius: 1.3f, tick: 0.3f, knockback: 6f);
                    break;
                }
                case WeaponBehaviorKind.GroundZone:
                {
                    // Erupts from the ground at the target spot, lingers and damages on contact.
                    float3 spawnPos = new float3(target.x, target.y, 0f);
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(spawnPos));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = 2.5f,
                        IsPlayerOwned = 1,
                        Radius = 1.1f,
                        WeaponId = weaponId,
                        IsMelee = 0,
                        Behavior = (byte)ProjectileBehavior.GroundZone,
                        TickInterval = 0.4f,
                        TickTimer = 0.4f,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = float2.zero });
                    break;
                }
                case WeaponBehaviorKind.MeleeArc:
                {
                    // Wide fan sweep in front of the player (large-radius hemisphere approximation).
                    float arcOffset = math.min(weapon.Range * 0.5f, 1.4f);
                    float3 pos = origin + new float3(direction.x * arcOffset, direction.y * arcOffset, 0f);
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(pos));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = 0.3f,
                        IsPlayerOwned = 1,
                        Radius = math.max(1.3f, weapon.Range * 0.55f),
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.Linear,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = direction * 1.0f });
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
                        Radius = math.max(0.6f, weapon.Range * 0.35f),
                        WeaponId = weaponId,
                        IsMelee = 1,
                        Behavior = (byte)ProjectileBehavior.Linear,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = direction * 1.5f });
                    break;
                }
                default: // Linear straight projectile
                {
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, LocalTransform.FromPosition(origin + new float3(spawnOffset.x, spawnOffset.y, 0f)));
                    ecb.AddComponent(e, new ProjectileData
                    {
                        Damage = weapon.Damage,
                        Lifetime = math.max(0.2f, weapon.Range / math.max(1f, weapon.ProjectileSpeed)),
                        IsPlayerOwned = 1,
                        Radius = 0.35f,
                        WeaponId = weaponId,
                        IsMelee = 0,
                        Behavior = (byte)ProjectileBehavior.Linear,
                    });
                    ecb.AddComponent(e, new PhysicsVelocity2D { Value = direction * math.max(2f, weapon.ProjectileSpeed) });
                    break;
                }
            }

            return true;
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
            float knockback)
        {
            Entity e = ecb.CreateEntity();
            float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0f);
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
            ecb.AddComponent(e, new OrbitState { Radius = radius, AngularSpeed = angularSpeed, Angle = angle });
        }
    }
}
