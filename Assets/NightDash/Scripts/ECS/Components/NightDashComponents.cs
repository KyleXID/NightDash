using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace NightDash.ECS.Components
{
    public enum RunStatus : byte
    {
        Loading = 0,
        Playing = 1,
        Paused = 2,
        LevelUpSelection = 3,
        Victory = 4,
        Defeat = 5,
        Result = 6
    }

    public enum UpgradeKind : byte
    {
        None = 0,
        Weapon = 1,
        Passive = 2
    }

    public enum RunNavigationAction : byte
    {
        None = 0,
        Retry = 1,
        ReturnToLobby = 2
    }

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
        public RunStatus Status;
        public int PendingLevelUps;
    }

    public struct StageRuntimeConfig : IComponentData
    {
        public float StageDuration;
        public float BossSpawnTime;
        public float SpawnRateMultiplier;
        public byte IsStageCleared;
        public byte UseBounds;
        public float2 BoundsMin;
        public float2 BoundsMax;
    }

    public struct BossSpawnState : IComponentData
    {
        public byte HasSpawnedBoss;
        public byte BossKilled;
        public byte ChestPending;
        public byte ChestOpened;
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

    public struct RunResultStats : IComponentData
    {
        public int KillCount;
        public int GoldEarned;
        public int SoulsEarned;
        public int CurrentWave;
        public byte RewardCommitted;
    }

    public struct BossRewardState : IComponentData
    {
        public byte HasPendingReward;
        public byte EvolutionResolved;
    }

    public struct BossRewardConfirmRequest : IComponentData
    {
        public byte IsPending;
    }

    public struct ResultSnapshot : IComponentData
    {
        public byte HasSnapshot;
        public byte IsVictory;
        public float ElapsedTime;
        public int FinalLevel;
        public int KillCount;
        public int GoldEarned;
        public int SoulsEarned;
        public int RewardGranted;
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

    public struct PlayerProgressionState : IComponentData
    {
        public int WeaponSlotLimit;
        public int PassiveSlotLimit;
        public int RerollsRemaining;
    }

    public struct UpgradeSelectionRequest : IComponentData
    {
        public int SelectedOptionIndex;
        public byte HasSelection;
        public byte RerollRequested;
    }

    public struct RunNavigationRequest : IComponentData
    {
        public RunNavigationAction Action;
        public byte IsPending;
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
        public float ProjectileSpeed;
    }

    public struct ProjectileData : IComponentData
    {
        public float Damage;
        public float Lifetime;
        public byte IsPlayerOwned;
        public float Radius;
    }

    public struct PhysicsVelocity2D : IComponentData
    {
        public Unity.Mathematics.float2 Value;
    }

    public struct EnemyArchetypeData : IComponentData
    {
        public FixedString64Bytes Id;
    }
}
