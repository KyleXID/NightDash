using Unity.Entities;

namespace NightDash.ECS.Components
{
    public struct GameLoopState : IComponentData
    {
        public int StageIndex;
        public float ElapsedTime;
        public int Level;
        public float Experience;
        public float NextLevelExperience;
        public int RiskScore;
        public byte IsBossSpawned;
        public byte IsBossDefeated;
        public byte RunEnded;
        public byte RewardGranted;
    }

    public struct EvolutionState : IComponentData
    {
        public int NormalEvolutionCount;
        public int AbyssEvolutionCount;
        public byte CanAbyssEvolution;
    }

    public struct MetaProgress : IComponentData
    {
        public int ConquestPoint;
        public int AttackNodeLevel;
        public int SurvivalNodeLevel;
        public int AbyssNodeLevel;
    }

    public struct DifficultyModifierElement : IBufferElementData
    {
        public int ModifierId;
        public float RiskValue;
        public float RewardMultiplier;
        public byte Enabled;
    }

    public struct StageTimelineElement : IBufferElementData
    {
        public float StartTime;
        public float SpawnIntervalMultiplier;
        public int SpawnCountBonus;
    }

    public struct StageRuntimeConfig : IComponentData
    {
        public float BossSpawnTime;
        public float ClearTime;
    }
}
