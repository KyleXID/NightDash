using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public sealed class ProjectileAuthoring : MonoBehaviour
    {
        public float damage = 10f;
        public float lifetime = 2f;
        public bool isPlayerOwned = true;

        private sealed class ProjectileBaker : Unity.Entities.Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ProjectileData
                {
                    Damage = authoring.damage,
                    Lifetime = authoring.lifetime,
                    IsPlayerOwned = authoring.isPlayerOwned ? (byte)1 : (byte)0
                });
            }
        }
    }
}
