using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(DataBootstrapSystem))]
    public partial struct RunSelectionOverrideSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunSelection>();
            state.RequireForUpdate<DataLoadState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RunSelectionSession.TryConsumePending(out string stageId, out string classId))
            {
                return;
            }

            RefRW<RunSelection> selection = SystemAPI.GetSingletonRW<RunSelection>();
            selection.ValueRW.StageId = new FixedString64Bytes(stageId);
            selection.ValueRW.ClassId = new FixedString64Bytes(classId);

            RefRW<DataLoadState> load = SystemAPI.GetSingletonRW<DataLoadState>();
            load.ValueRW.HasLoaded = 0;

            if (SystemAPI.HasSingleton<GameLoopState>())
            {
                RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
                loop.ValueRW.ElapsedTime = 0f;
                loop.ValueRW.Level = 1;
                loop.ValueRW.Experience = 0f;
                loop.ValueRW.NextLevelExperience = 10f;
                loop.ValueRW.IsRunActive = 0;
                loop.ValueRW.Status = RunStatus.Loading;
                loop.ValueRW.PendingLevelUps = 0;
            }

            if (SystemAPI.HasSingleton<StageRuntimeConfig>())
            {
                RefRW<StageRuntimeConfig> stage = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
                stage.ValueRW.IsStageCleared = 0;
            }

            if (SystemAPI.HasSingleton<BossSpawnState>())
            {
                RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();
                bossState.ValueRW.HasSpawnedBoss = 0;
                bossState.ValueRW.BossKilled = 0;
                bossState.ValueRW.ChestPending = 0;
                bossState.ValueRW.ChestOpened = 0;
            }

            if (SystemAPI.HasSingleton<RunResultStats>())
            {
                RefRW<RunResultStats> result = SystemAPI.GetSingletonRW<RunResultStats>();
                result.ValueRW.KillCount = 0;
                result.ValueRW.GoldEarned = 0;
                result.ValueRW.SoulsEarned = 0;
                result.ValueRW.CurrentWave = 0;
                result.ValueRW.RewardCommitted = 0;
            }

            if (SystemAPI.HasSingleton<BossRewardState>())
            {
                RefRW<BossRewardState> reward = SystemAPI.GetSingletonRW<BossRewardState>();
                reward.ValueRW.HasPendingReward = 0;
                reward.ValueRW.EvolutionResolved = 0;
            }

            if (SystemAPI.HasSingleton<BossRewardConfirmRequest>())
            {
                RefRW<BossRewardConfirmRequest> rewardConfirm = SystemAPI.GetSingletonRW<BossRewardConfirmRequest>();
                rewardConfirm.ValueRW.IsPending = 0;
            }

            if (SystemAPI.HasSingleton<ResultSnapshot>())
            {
                RefRW<ResultSnapshot> snapshot = SystemAPI.GetSingletonRW<ResultSnapshot>();
                snapshot.ValueRW.HasSnapshot = 0;
                snapshot.ValueRW.IsVictory = 0;
                snapshot.ValueRW.ElapsedTime = 0f;
                snapshot.ValueRW.FinalLevel = 1;
                snapshot.ValueRW.KillCount = 0;
                snapshot.ValueRW.GoldEarned = 0;
                snapshot.ValueRW.SoulsEarned = 0;
                snapshot.ValueRW.RewardGranted = 0;
            }

            if (SystemAPI.HasSingleton<UpgradeSelectionRequest>())
            {
                RefRW<UpgradeSelectionRequest> upgrade = SystemAPI.GetSingletonRW<UpgradeSelectionRequest>();
                upgrade.ValueRW.SelectedOptionIndex = -1;
                upgrade.ValueRW.HasSelection = 0;
                upgrade.ValueRW.RerollRequested = 0;
            }

            if (SystemAPI.HasSingleton<RunNavigationRequest>())
            {
                RefRW<RunNavigationRequest> navigation = SystemAPI.GetSingletonRW<RunNavigationRequest>();
                navigation.ValueRW.Action = RunNavigationAction.None;
                navigation.ValueRW.IsPending = 0;
            }

            NightDashLog.Info($"[NightDash] RunSelection override applied: stage='{stageId}', class='{classId}'.");
        }
    }
}
