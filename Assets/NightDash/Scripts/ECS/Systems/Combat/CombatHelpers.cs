using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    /// <summary>
    /// Pure helpers extracted from CombatSystem.cs during the S2-02 refactor.
    /// Kept in the NightDash.ECS.Systems namespace so existing call sites do
    /// not need using-directive churn.
    /// </summary>
    internal static class CombatHelpers
    {
        /// <summary>Radius of projectiles spawned by caster-type enemies (matches CombatSystem.CasterProjectileRadius).</summary>
        public const float CasterProjectileRadius = 0.32f;

        /// <summary>Speed of caster projectiles (matches CombatSystem.CasterProjectileSpeed).</summary>
        public const float CasterProjectileSpeed = 7.4f;

        /// <summary>Resolves gold / soul / xp rewards for an enemy kill by archetype id and boss flag.</summary>
        public static void ResolveEnemyRewards(
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

        /// <summary>Linear scan used to avoid double-registering a dead enemy in the per-frame dead-list.</summary>
        public static bool ContainsEntity(NativeList<Entity> entities, Entity target)
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

        /// <summary>Spawns an enemy-owned caster projectile aimed at the given target.</summary>
        public static void SpawnEnemyProjectile(
            ref EntityCommandBuffer ecb,
            float3 origin,
            float3 target,
            float damage)
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
