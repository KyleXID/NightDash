using Unity.Entities;

namespace NightDash.ECS.Components
{
    public struct EnemySpawnConfig : IComponentData
    {
        public Entity EnemyPrefab;
        public float BaseInterval;
        public float Timer;
        public int BaseSpawnCount;
        public float RuntimeIntervalMultiplier;
        public int RuntimeSpawnCountBonus;
    }

    public struct WeaponRuntimeData : IComponentData
    {
        public Entity ProjectilePrefab;
        public float Damage;
        public float Cooldown;
        public float Timer;
        public float ProjectileSpeed;
        public float ProjectileLifeTime;
    }

    public struct RandomState : IComponentData
    {
        public Unity.Mathematics.Random Value;
    }

    public struct PhysicsVelocity2D : IComponentData
    {
        public Unity.Mathematics.float3 Value;
    }
}
