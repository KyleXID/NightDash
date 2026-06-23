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
        Passive = 2,
        Evolution = 3 // upgrade card that evolves an owned weapon to its "_evolved" form
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

        // Aggregated effect multipliers (1.0 = no effect). Cached by DifficultySystem
        // from the DifficultyModifierElement buffer each frame so consumers don't
        // re-sum the buffer.
        public float EnemyHpMultiplier;
        public float EnemySpeedMultiplier;
        public float SpawnRateMultiplier;
        public float HealRateMultiplier;
        public float CooldownMultiplier;
        public float HazardMultiplier;
        public byte OnKillExplosionEnabled;
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
        // Shield acts as a temporary HP buffer that absorbs damage before
        // CurrentHealth. Regenerates while the player is out of combat
        // (see NightDash.ECS.Systems.ShieldSystem).
        public float CurrentShield;
        public float MaxShield;
        // Seconds since the last hit. Drives shield regen — shield only
        // ticks back up after the player has stayed clear for a grace
        // window. Updated by ShieldSystem.
        public float TimeSinceLastHit;
        // Crit roll: chance is 0..1, multiplier scales the damage on a
        // successful roll. Applied to player → enemy damage in CombatSystem.
        public float CritChance;
        public float CritMultiplier;
        // Dash: short-burst speed boost. Speed multiplier is active while
        // DashTimer > 0, after which DashCooldownRemaining ticks back to 0
        // before the next dash can fire. Triggered by Space.
        public float DashTimer;
        public float DashCooldownRemaining;
        // Potion stock: discrete charges that the player can spend to
        // restore HP via Q. Refills on run reset.
        public int PotionCount;
        public int MaxPotionCount;
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
        public FixedString64Bytes WeaponId;
        public byte IsMelee;

        // Behavior extension (all default 0 = original linear single-hit).
        public byte Behavior;        // ProjectileBehavior
        public float TickInterval;   // >0 → persistent multi-hit aura: damages every interval, never destroyed by a hit
        public float TickTimer;      // countdown to the next damaging tick
        public float Knockback;      // outward push applied to enemies on a damaging tick (0 = none)
        public byte AlignToVelocity; // 1 = view rotates to face travel direction (bullets/arrows/spears); 0 = fixed orientation (sky-fall, melee, orbit)
        public byte PlayOnce;        // 1 = the VFX animation plays through ONCE over Lifetime (non-looping) so it finishes on impact; 0 = loop at default fps
        public float SplashRadius;   // >0 = on landing (Lifetime end) deal an AoE: full damage in a small core, reduced in the splash ring
        public float SplashFactor;   // splash damage multiplier (0..1) applied to enemies outside the core but inside SplashRadius
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
