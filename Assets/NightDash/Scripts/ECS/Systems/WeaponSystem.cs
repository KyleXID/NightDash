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

            if (targetEnemy == Entity.Null)
            {
                return;
            }

            string classId = SystemAPI.GetSingleton<RunSelection>().ClassId.ToString();
            if (!registry.TryGetClass(classId, out ClassData classData) || classData == null)
            {
                return;
            }

            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            PlayerRuntimeProfile playerProfile = RuntimeBalanceUtility.ResolvePlayerRuntimeProfile(registry, classData, ownedPassives);
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
                        bool isMelee = weaponData.weaponType == WeaponType.Melee;
                        SpawnProjectile(
                            ref ecb,
                            origin,
                            targetPosition,
                            profile,
                            firingIndex,
                            ownedWeapons.Length,
                            ownedWeapon.Id,
                            isMelee);
                        ownedWeapon.CooldownRemaining = profile.Cooldown;
                        firingIndex += 1;
                    }

                    ownedWeapons[i] = ownedWeapon;
                }
            }
        }

        private static void SpawnProjectile(
            ref EntityCommandBuffer ecb,
            float3 origin,
            float3 target,
            WeaponRuntimeProfile weapon,
            int weaponIndex,
            int weaponCount,
            FixedString64Bytes weaponId,
            bool isMelee)
        {
            float3 direction3 = target - origin;
            direction3.z = 0f;
            float lengthSq = math.lengthsq(direction3);
            if (lengthSq <= 0.0001f)
            {
                direction3 = new float3(1f, 0f, 0f);
            }

            float2 direction = math.normalize(direction3.xy);
            float2 perpendicular = new float2(-direction.y, direction.x);
            float offsetFactor = weaponCount > 1 ? weaponIndex - ((weaponCount - 1) * 0.5f) : 0f;
            float2 spawnOffset = perpendicular * (0.32f * offsetFactor);

            if (isMelee)
            {
                // Melee: spawn near player toward target, short lifetime, large radius, no travel
                float meleeOffset = math.min(weapon.Range * 0.4f, 1.2f);
                float3 meleePosition = origin + new float3(direction.x * meleeOffset, direction.y * meleeOffset, 0f);
                Entity projectile = ecb.CreateEntity();
                ecb.AddComponent(projectile, LocalTransform.FromPosition(meleePosition));
                ecb.AddComponent(projectile, new ProjectileData
                {
                    Damage = weapon.Damage,
                    Lifetime = 0.25f,
                    IsPlayerOwned = 1,
                    Radius = math.max(0.6f, weapon.Range * 0.35f),
                    WeaponId = weaponId,
                    IsMelee = 1
                });
                ecb.AddComponent(projectile, new PhysicsVelocity2D
                {
                    Value = direction * 1.5f
                });
            }
            else
            {
                Entity projectile = ecb.CreateEntity();
                ecb.AddComponent(projectile, LocalTransform.FromPosition(origin + new float3(spawnOffset.x, spawnOffset.y, 0f)));
                ecb.AddComponent(projectile, new ProjectileData
                {
                    Damage = weapon.Damage,
                    Lifetime = math.max(0.2f, weapon.Range / math.max(1f, weapon.ProjectileSpeed)),
                    IsPlayerOwned = 1,
                    Radius = 0.35f,
                    WeaponId = weaponId,
                    IsMelee = 0
                });
                ecb.AddComponent(projectile, new PhysicsVelocity2D
                {
                    Value = direction * math.max(2f, weapon.ProjectileSpeed)
                });
            }
        }
    }
}
