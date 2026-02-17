using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float health = 100f;
        public float moveSpeed = 4f;
        public float damage = 10f;
        public float hitRadius = 0.4f;

        [Header("Weapon")]
        public GameObject projectilePrefab;
        public float weaponDamage = 10f;
        public float weaponCooldown = 1.0f;
        public float projectileSpeed = 8f;
        public float projectileLifeTime = 2f;

        public class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<PlayerTag>(entity);
                AddComponent(entity, new CombatStats
                {
                    Health = math.max(1f, authoring.health),
                    MaxHealth = math.max(1f, authoring.health),
                    MoveSpeed = math.max(0.1f, authoring.moveSpeed),
                    Damage = math.max(1f, authoring.damage),
                    HitRadius = math.max(0.1f, authoring.hitRadius)
                });

                AddComponent(entity, new WeaponRuntimeData
                {
                    ProjectilePrefab = authoring.projectilePrefab != null
                        ? GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    Damage = math.max(1f, authoring.weaponDamage),
                    Cooldown = math.max(0.05f, authoring.weaponCooldown),
                    Timer = 0f,
                    ProjectileSpeed = math.max(0.1f, authoring.projectileSpeed),
                    ProjectileLifeTime = math.max(0.1f, authoring.projectileLifeTime)
                });
            }
        }
    }
}
