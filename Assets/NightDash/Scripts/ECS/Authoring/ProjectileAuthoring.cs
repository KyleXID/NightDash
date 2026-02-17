using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Authoring
{
    public class ProjectileAuthoring : MonoBehaviour
    {
        public class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ProjectileTag>(entity);
                AddComponent(entity, new ProjectileData
                {
                    Speed = 0f,
                    Damage = 0f,
                    LifeTime = 1f,
                    Age = 0f
                });
                AddComponent(entity, new PhysicsVelocity2D());
            }
        }
    }
}
