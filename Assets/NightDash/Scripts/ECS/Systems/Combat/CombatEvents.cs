using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    /// <summary>
    /// Event bus for combat notifications consumed by the visual/audio
    /// bridge layer (see Docs/Architecture/bridges.md). Extracted from
    /// CombatSystem.cs during the S2-02 refactor.
    /// Namespace intentionally preserved to avoid churn in current and
    /// future subscribers.
    /// </summary>
    public static class NightDashCombatEvents
    {
        /// <summary>Fired when a projectile damages an enemy. Args: position, damage amount.</summary>
        public static event System.Action<float3, float> OnEnemyDamaged;

        /// <summary>Fired when an enemy's health reaches zero. Args: position, isBoss.</summary>
        public static event System.Action<float3, bool> OnEnemyKilled;

        /// <summary>Fired when the player takes damage (contact or projectile). Args: position, damage amount.</summary>
        public static event System.Action<float3, float> OnPlayerDamaged;

        internal static void FireEnemyDamaged(float3 position, float damage)
        {
            OnEnemyDamaged?.Invoke(position, damage);
        }

        internal static void FireEnemyKilled(float3 position, bool isBoss)
        {
            OnEnemyKilled?.Invoke(position, isBoss);
        }

        internal static void FirePlayerDamaged(float3 position, float damage)
        {
            OnPlayerDamaged?.Invoke(position, damage);
        }
    }
}
