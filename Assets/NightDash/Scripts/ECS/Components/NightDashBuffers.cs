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
        public float EnemyHealthMultiplier;
        public float EnemySpeedMultiplier;
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
