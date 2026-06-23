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

        // Evolution level-up card state (set by UpgradeApplySystem once this weapon
        // is at max level AND all of an evolution's required passives are maxed):
        //   0 = not eligible
        //   1 = eligible, GUARANTEED in the very next level-up card set
        //   2 = eligible but the guaranteed offer passed → now appears by chance
        public byte EvolutionOffer;
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
        public byte Rarity; // UpgradeRarity: 0 Common / 1 Rare / 2 Legendary (rolled per card)
    }

    // Enemies a piercing projectile has already damaged, so a fast pierce shot
    // hits each enemy exactly once as it passes through (instead of relying on a
    // tick cadence that fast projectiles can skip between).
    [InternalBufferCapacity(8)]
    public struct PierceHitElement : IBufferElementData
    {
        public Entity Value;
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
