using Unity.Entities;
using Unity.Collections;

namespace NightDash.ECS.Components
{
    public struct PlayerTag : IComponentData { }
    public struct EnemyTag : IComponentData { }
    public struct BossTag : IComponentData { }

    public struct GameLoopState : IComponentData
    {
        public float ElapsedTime;
        public int Level;
        public float Experience;
        public float NextLevelExperience;
        public byte IsRunActive;
    }

    public struct StageRuntimeConfig : IComponentData
    {
        public float StageDuration;
        public float BossSpawnTime;
        public float SpawnRateMultiplier;
        public byte IsStageCleared;
    }

    public struct BossSpawnState : IComponentData
    {
        public byte HasSpawnedBoss;
    }

    public struct DifficultyState : IComponentData
    {
        public int RiskScore;
        public float RewardMultiplier;
    }

    public struct EvolutionState : IComponentData
    {
        public byte HasNormalEvolution;
        public byte HasAbyssEvolution;
        public byte CanAttemptAbyss;
    }

    public struct MetaProgress : IComponentData
    {
        public int ConquestPoints;
        public int LastRunReward;
    }

    public struct SaveState : IComponentData
    {
        public int LastSavedConquestPoints;
    }

    public struct RunSelection : IComponentData
    {
        public FixedString64Bytes StageId;
        public FixedString64Bytes ClassId;
    }

    public struct DataLoadState : IComponentData
    {
        public byte HasLoaded;
    }

    public struct CombatStats : IComponentData
    {
        public float CurrentHealth;
        public float MaxHealth;
        public float Damage;
        public float MoveSpeed;
    }

    public struct EnemySpawnConfig : IComponentData
    {
        public Entity EnemyPrefab;
        public Entity BossPrefab;
        public float SpawnInterval;
        public float SpawnTimer;
        public uint RandomSeed;
    }

    public struct WeaponRuntimeData : IComponentData
    {
        public float Cooldown;
        public float CooldownRemaining;
        public float Damage;
        public float Range;
    }

    public struct ProjectileData : IComponentData
    {
        public float Damage;
        public float Lifetime;
        public byte IsPlayerOwned;
    }

    public struct PhysicsVelocity2D : IComponentData
    {
        public Unity.Mathematics.float2 Value;
    }
}
