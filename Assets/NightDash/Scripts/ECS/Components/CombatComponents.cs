using Unity.Entities;

namespace NightDash.ECS.Components
{
    public struct CombatStats : IComponentData
    {
        public float Health;
        public float MaxHealth;
        public float MoveSpeed;
        public float Damage;
        public float HitRadius;
    }

    public struct EnemyTag : IComponentData { }
    public struct PlayerTag : IComponentData { }
    public struct BossTag : IComponentData { }

    public struct ProjectileTag : IComponentData { }

    public struct ProjectileData : IComponentData
    {
        public float Speed;
        public float Damage;
        public float LifeTime;
        public float Age;
    }
}
