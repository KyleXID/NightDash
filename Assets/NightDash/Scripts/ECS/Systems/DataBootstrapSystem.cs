using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DataBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<RunSelection>();
            state.RequireForUpdate<DataLoadState>();
            state.RequireForUpdate<EnemySpawnConfig>();
            state.RequireForUpdate<StageTimelineElement>();
            state.RequireForUpdate<SpawnArchetypeElement>();
            state.RequireForUpdate<PlayerProgressionState>();
            state.RequireForUpdate<RunResultStats>();
            state.RequireForUpdate<BossRewardState>();
            state.RequireForUpdate<BossRewardConfirmRequest>();
            state.RequireForUpdate<ResultSnapshot>();
            state.RequireForUpdate<RunNavigationRequest>();
            state.RequireForUpdate<OwnedWeaponElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<DataLoadState> loadState = SystemAPI.GetSingletonRW<DataLoadState>();
            if (loadState.ValueRO.HasLoaded == 1)
            {
                return;
            }

            var registry = DataRegistry.Instance;
            if (registry == null || registry.Catalog == null)
            {
                return;
            }

            var selection = SystemAPI.GetSingleton<RunSelection>();
            var stageId = selection.StageId.IsEmpty ? "stage_01" : selection.StageId.ToString();
            var classId = selection.ClassId.IsEmpty ? "class_warrior" : selection.ClassId.ToString();

            if (!registry.TryGetStage(stageId, out StageData stageData))
            {
                NightDashLog.Warn($"[NightDash] StageData not found for '{stageId}', using baked defaults.");
            }
            else
            {
                RefRW<StageRuntimeConfig> stageRuntime = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
                stageRuntime.ValueRW.StageDuration = stageData.durationSec;
                stageRuntime.ValueRW.BossSpawnTime = stageData.bossSpawnSec;
                stageRuntime.ValueRW.IsStageCleared = 0;
                stageRuntime.ValueRW.UseBounds = stageData.useBounds ? (byte)1 : (byte)0;

                float halfX = math.max(0.5f, stageData.boundsSize.x * 0.5f);
                float halfY = math.max(0.5f, stageData.boundsSize.y * 0.5f);
                stageRuntime.ValueRW.BoundsMin = new float2(stageData.boundsCenter.x - halfX, stageData.boundsCenter.y - halfY);
                stageRuntime.ValueRW.BoundsMax = new float2(stageData.boundsCenter.x + halfX, stageData.boundsCenter.y + halfY);

                ApplyStageSpawnPhases(stageData);

                RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
                meta.ValueRW.LastRunReward = 0;
            }

            RuntimeBalanceUtility.ResetRunBuffers(ref state);
            RuntimeBalanceUtility.PopulateAvailableUpgrades(ref state, registry);
            RuntimeBalanceUtility.AddStartingLoadout(ref state, registry, classId);

            RefRW<PlayerProgressionState> progression = SystemAPI.GetSingletonRW<PlayerProgressionState>();
            progression.ValueRW.WeaponSlotLimit = 6;
            progression.ValueRW.PassiveSlotLimit = 6;
            progression.ValueRW.RerollsRemaining = 1;

            RefRW<UpgradeSelectionRequest> selectionRequest = SystemAPI.GetSingletonRW<UpgradeSelectionRequest>();
            selectionRequest.ValueRW.SelectedOptionIndex = -1;
            selectionRequest.ValueRW.HasSelection = 0;
            selectionRequest.ValueRW.RerollRequested = 0;

            RefRW<RunResultStats> resultStats = SystemAPI.GetSingletonRW<RunResultStats>();
            resultStats.ValueRW.KillCount = 0;
            resultStats.ValueRW.GoldEarned = 0;
            resultStats.ValueRW.SoulsEarned = 0;
            resultStats.ValueRW.CurrentWave = 0;
            resultStats.ValueRW.RewardCommitted = 0;

            RefRW<BossSpawnState> bossState = SystemAPI.GetSingletonRW<BossSpawnState>();
            bossState.ValueRW.HasSpawnedBoss = 0;
            bossState.ValueRW.BossKilled = 0;
            bossState.ValueRW.ChestPending = 0;
            bossState.ValueRW.ChestOpened = 0;

            RefRW<BossRewardState> bossReward = SystemAPI.GetSingletonRW<BossRewardState>();
            bossReward.ValueRW.HasPendingReward = 0;
            bossReward.ValueRW.EvolutionResolved = 0;

            RefRW<BossRewardConfirmRequest> bossRewardConfirm = SystemAPI.GetSingletonRW<BossRewardConfirmRequest>();
            bossRewardConfirm.ValueRW.IsPending = 0;

            RefRW<ResultSnapshot> snapshot = SystemAPI.GetSingletonRW<ResultSnapshot>();
            snapshot.ValueRW.HasSnapshot = 0;
            snapshot.ValueRW.IsVictory = 0;
            snapshot.ValueRW.ElapsedTime = 0f;
            snapshot.ValueRW.FinalLevel = 1;
            snapshot.ValueRW.KillCount = 0;
            snapshot.ValueRW.GoldEarned = 0;
            snapshot.ValueRW.SoulsEarned = 0;
            snapshot.ValueRW.RewardGranted = 0;

            RefRW<RunNavigationRequest> navigation = SystemAPI.GetSingletonRW<RunNavigationRequest>();
            navigation.ValueRW.Action = RunNavigationAction.None;
            navigation.ValueRW.IsPending = 0;

            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();
            loop.ValueRW.ElapsedTime = 0f;
            loop.ValueRW.Level = 1;
            loop.ValueRW.Experience = 0f;
            loop.ValueRW.NextLevelExperience = 28f;
            loop.ValueRW.IsRunActive = 1;
            loop.ValueRW.Status = RunStatus.Playing;
            loop.ValueRW.PendingLevelUps = 0;

            if (!registry.TryGetClass(classId, out ClassData classData))
            {
                NightDashLog.Warn($"[NightDash] ClassData not found for '{classId}', using baked defaults.");
            }
            else
            {
                RuntimeBalanceUtility.RefreshPlayerRuntime(ref state, registry, classId);
            }

            loadState.ValueRW.HasLoaded = 1;
            NightDashLog.Info($"[NightDash] DataBootstrap loaded stage='{stageId}', class='{classId}'.");
        }

        private void ApplyStageSpawnPhases(StageData stageData)
        {
            if (stageData.spawnPhases == null || stageData.spawnPhases.Count == 0)
            {
                return;
            }

            DynamicBuffer<StageTimelineElement> timeline = SystemAPI.GetSingletonBuffer<StageTimelineElement>();
            DynamicBuffer<SpawnArchetypeElement> spawnArchetypes = SystemAPI.GetSingletonBuffer<SpawnArchetypeElement>();
            EnemySpawnConfig spawn = SystemAPI.GetSingleton<EnemySpawnConfig>();

            float baseSpawnPerMinute = 60f / math.max(0.1f, spawn.SpawnInterval);
            timeline.Clear();
            spawnArchetypes.Clear();

            for (int i = 0; i < stageData.spawnPhases.Count; i++)
            {
                SpawnPhase phase = stageData.spawnPhases[i];
                if (phase.toSec <= phase.fromSec)
                {
                    continue;
                }

                float totalSpawnPerMinute = 0f;
                if (phase.entries != null)
                {
                    for (int j = 0; j < phase.entries.Count; j++)
                    {
                        totalSpawnPerMinute += math.max(0, phase.entries[j].spawnPerMin);
                    }
                }

                if (totalSpawnPerMinute <= 0f)
                {
                    totalSpawnPerMinute = baseSpawnPerMinute;
                }

                timeline.Add(new StageTimelineElement
                {
                    StartTime = phase.fromSec,
                    EndTime = phase.toSec,
                    SpawnMultiplier = math.max(0.1f, totalSpawnPerMinute / baseSpawnPerMinute),
                    EnableBonusSpawn = 0
                });

                if (phase.entries == null)
                {
                    continue;
                }

                for (int j = 0; j < phase.entries.Count; j++)
                {
                    SpawnEntry entry = phase.entries[j];
                    if (string.IsNullOrWhiteSpace(entry.enemyId))
                    {
                        continue;
                    }

                    spawnArchetypes.Add(new SpawnArchetypeElement
                    {
                        StartTime = phase.fromSec,
                        EndTime = phase.toSec,
                        EnemyId = entry.enemyId.Trim(),
                        Weight = math.max(1, entry.weight),
                        SpawnPerMinute = math.max(0, entry.spawnPerMin),
                        IsBoss = (byte)(entry.enemyId.Trim() == stageData.bossId ? 1 : 0)
                    });
                }
            }

            NightDashLog.Info($"[NightDash] Stage spawn phases applied: stage='{stageData.id}', phases={timeline.Length}, entries={spawnArchetypes.Length}.");
        }
    }
}
