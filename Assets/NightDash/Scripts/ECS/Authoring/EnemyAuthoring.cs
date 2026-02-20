using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        public bool isBoss;
        public float maxHealth = 25f;
        public float damage = 5f;
        public float moveSpeed = 2.5f;

        private sealed class EnemyBaker : Unity.Entities.Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);

                if (authoring.isBoss)
                {
                    AddComponent<BossTag>(entity);
                }

                AddComponent(entity, new CombatStats
                {
                    CurrentHealth = authoring.maxHealth,
                    MaxHealth = authoring.maxHealth,
                    Damage = authoring.damage,
                    MoveSpeed = authoring.moveSpeed
                });
            }
        }
    }
}
