using Unity.Entities;

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

    [InternalBufferCapacity(16)]
    public struct DifficultyModifierElement : IBufferElementData
    {
        public int RiskScore;
        public float RewardMultiplierBonus;
        public float EnemyHealthMultiplier;
        public float EnemySpeedMultiplier;
    }
}
