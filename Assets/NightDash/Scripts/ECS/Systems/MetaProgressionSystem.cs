using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StageProgressSystem))]
    public partial struct MetaProgressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaProgress>();
            state.RequireForUpdate<DifficultyState>();
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<RunResultStats>();
            state.RequireForUpdate<BossSpawnState>();
            state.RequireForUpdate<BossRewardState>();
            state.RequireForUpdate<ResultSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
            DifficultyState difficulty = SystemAPI.GetSingleton<DifficultyState>();
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            RefRW<RunResultStats> result = SystemAPI.GetSingletonRW<RunResultStats>();
            BossSpawnState bossState = SystemAPI.GetSingleton<BossSpawnState>();
            BossRewardState bossReward = SystemAPI.GetSingleton<BossRewardState>();
            RefRW<ResultSnapshot> snapshot = SystemAPI.GetSingletonRW<ResultSnapshot>();

            if (loop.ValueRO.IsRunActive == 0 &&
                (loop.ValueRO.Status == RunStatus.Playing || loop.ValueRO.Status == RunStatus.LevelUpSelection))
            {
                loop.ValueRW.Status = bossState.BossKilled == 1 ? RunStatus.Victory : RunStatus.Defeat;
            }

            if (loop.ValueRO.Status != RunStatus.Victory &&
                loop.ValueRO.Status != RunStatus.Defeat &&
                loop.ValueRO.Status != RunStatus.Result)
            {
                return;
            }

            if (result.ValueRO.RewardCommitted == 0)
            {
                int rewardAmount = CalculateRunReward(loop.ValueRO.Status, result.ValueRO, difficulty);
                meta.ValueRW.ConquestPoints += rewardAmount;
                meta.ValueRW.LastRunReward = rewardAmount;

                RunResultStats updatedResult = result.ValueRO;
                updatedResult.RewardCommitted = 1;
                result.ValueRW = updatedResult;
            }

            if (snapshot.ValueRO.HasSnapshot == 0 && result.ValueRO.RewardCommitted == 1)
            {
                snapshot.ValueRW = new ResultSnapshot
                {
                    HasSnapshot = 1,
                    IsVictory = loop.ValueRO.Status == RunStatus.Victory ? (byte)1 : (byte)0,
                    ElapsedTime = loop.ValueRO.ElapsedTime,
                    FinalLevel = loop.ValueRO.Level,
                    KillCount = result.ValueRO.KillCount,
                    GoldEarned = result.ValueRO.GoldEarned,
                    SoulsEarned = result.ValueRO.SoulsEarned,
                    RewardGranted = meta.ValueRO.LastRunReward
                };
            }

            if (loop.ValueRO.Status == RunStatus.Victory && bossReward.HasPendingReward == 1)
            {
                return;
            }

            if (loop.ValueRO.Status != RunStatus.Result && result.ValueRO.RewardCommitted == 1)
            {
                loop.ValueRW.Status = RunStatus.Result;
            }
        }

        private static int CalculateRunReward(RunStatus status, RunResultStats result, DifficultyState difficulty)
        {
            int outcomeBaseReward = status == RunStatus.Victory ? 10 : 4;
            float performanceReward = result.KillCount * 0.1f + result.GoldEarned * 0.25f + result.SoulsEarned;
            float totalReward = (outcomeBaseReward + performanceReward) * math.max(1f, difficulty.RewardMultiplier);
            return math.max(1, (int)math.floor(totalReward));
        }
    }
}
