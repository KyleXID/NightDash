using Unity.Entities;
using Unity.Collections;

namespace NightDash.ECS.Components
{
    [InternalBufferCapacity(8)]
    public struct StageTimelineElement : IBufferElementData
    {
        public float StartTime;
        public float EndTime;
        public float SpawnMultiplier;
        public byte EnableBonusSpawn;
    }

    [InternalBufferCapacity(24)]
    public struct SpawnArchetypeElement : IBufferElementData
    {
        public float StartTime;
        public float EndTime;
        public FixedString64Bytes EnemyId;
        public int Weight;
        public int SpawnPerMinute;
        public byte IsBoss;
    }

    [InternalBufferCapacity(16)]
    public struct DifficultyModifierElement : IBufferElementData
    {
        public int RiskScore;
        public float RewardMultiplierBonus;

        // pct values mirror DifficultyModifierData. Stored additively so DifficultySystem
        // can sum them into DifficultyState. (multiplier = 1 + sum(pct))
        public float HpPct;
        public float MoveSpeedPct;
        public float SpawnRatePct;
        public float HealRatePct;
        public float CooldownPct;
        public float HazardMultiplier;
        public byte OnKillExplosion;
    }

    [InternalBufferCapacity(8)]
    public struct OwnedWeaponElement : IBufferElementData
    {
        public FixedString64Bytes Id;
        public int Level;
        public int MaxLevel;
        public float CooldownRemaining;
    }

    [InternalBufferCapacity(8)]
    public struct OwnedPassiveElement : IBufferElementData
    {
        public FixedString64Bytes Id;
        public int Level;
        public int MaxLevel;
    }

    [InternalBufferCapacity(16)]
    public struct UpgradeOptionElement : IBufferElementData
    {
        public UpgradeKind Kind;
        public FixedString64Bytes Id;
        public int CurrentLevel;
        public int NextLevel;
        public int MaxLevel;
    }

    [InternalBufferCapacity(16)]
    public struct AvailableWeaponElement : IBufferElementData
    {
        public FixedString64Bytes Id;
    }

    [InternalBufferCapacity(16)]
    public struct AvailablePassiveElement : IBufferElementData
    {
        public FixedString64Bytes Id;
    }
}
