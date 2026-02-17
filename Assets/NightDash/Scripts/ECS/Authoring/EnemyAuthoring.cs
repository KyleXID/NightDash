using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        public bool isBoss;
        public float health = 20f;
        public float moveSpeed = 2.2f;
        public float damage = 8f;
        public float hitRadius = 0.35f;

        public class Baker : Baker<EnemyAuthoring>
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
                    Health = math.max(1f, authoring.health),
                    MaxHealth = math.max(1f, authoring.health),
                    MoveSpeed = math.max(0.1f, authoring.moveSpeed),
                    Damage = math.max(1f, authoring.damage),
                    HitRadius = math.max(0.1f, authoring.hitRadius)
                });
            }
        }
    }
}
