using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public sealed class PlayerAuthoring : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float damage = 12f;
        public float moveSpeed = 5f;
        public float weaponCooldown = 0.8f;
        public float weaponRange = 7f;

        private sealed class PlayerBaker : Unity.Entities.Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);

                AddComponent(entity, new CombatStats
                {
                    CurrentHealth = authoring.maxHealth,
                    MaxHealth = authoring.maxHealth,
                    Damage = authoring.damage,
                    MoveSpeed = authoring.moveSpeed
                });

                AddComponent(entity, new WeaponRuntimeData
                {
                    Cooldown = authoring.weaponCooldown,
                    CooldownRemaining = 0f,
                    Damage = authoring.damage,
                    Range = authoring.weaponRange
                });
            }
        }
    }
}
