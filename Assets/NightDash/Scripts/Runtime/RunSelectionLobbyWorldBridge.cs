using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;

namespace NightDash.Runtime
{
    internal static class RunSelectionLobbyWorldBridge
    {
        public static bool TryApplySelectionToCurrentWorld(string stageId, string classId)
        {
            int worldCount = World.All.Count;
            if (worldCount == 0)
            {
                NightDashLog.Warn("[NightDash] No ECS world exists yet.");
                return false;
            }

            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<RunSelection>(),
                    ComponentType.ReadWrite<DataLoadState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                ResetRunSelectionEntity(entityManager, singleton, stageId, classId);

                NightDashLog.Info($"[NightDash] RunSelection applied directly to ECS world '{world.Name}': stage='{stageId}', class='{classId}'.");
                return true;
            }

            NightDashLog.Warn("[NightDash] No entity with RunSelection + DataLoadState found in any ECS world.");
            return false;
        }

        public static void SetRunActiveInCurrentWorld(bool active)
        {
            int worldCount = World.All.Count;
            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameLoopState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                GameLoopState loop = entityManager.GetComponentData<GameLoopState>(singleton);
                loop.IsRunActive = active ? (byte)1 : (byte)0;
                if (!active)
                {
                    loop.ElapsedTime = 0f;
                    loop.Level = 1;
                    loop.Experience = 0f;
                    if (loop.NextLevelExperience <= 0f)
                    {
                        loop.NextLevelExperience = 10f;
                    }
                }
                entityManager.SetComponentData(singleton, loop);
            }
        }

        public static bool TryGetPendingNavigation(out RunNavigationAction action, out string stageId, out string classId)
        {
            action = RunNavigationAction.None;
            stageId = null;
            classId = null;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<RunNavigationRequest>(),
                ComponentType.ReadOnly<GameLoopState>(),
                ComponentType.ReadOnly<RunSelection>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            Entity singleton = query.GetSingletonEntity();
            RunNavigationRequest navigation = entityManager.GetComponentData<RunNavigationRequest>(singleton);
            if (navigation.IsPending == 0 || navigation.Action == RunNavigationAction.None)
            {
                return false;
            }

            RunSelection selection = entityManager.GetComponentData<RunSelection>(singleton);
            action = navigation.Action;
            stageId = selection.StageId.IsEmpty ? "stage_01" : selection.StageId.ToString();
            classId = selection.ClassId.IsEmpty ? "class_warrior" : selection.ClassId.ToString();

            navigation.IsPending = 0;
            navigation.Action = RunNavigationAction.None;
            entityManager.SetComponentData(singleton, navigation);
            return true;
        }

        private static void ResetRunSelectionEntity(EntityManager entityManager, Entity singleton, string stageId, string classId)
        {
            entityManager.SetComponentData(singleton, new RunSelection
            {
                StageId = new FixedString64Bytes(stageId),
                ClassId = new FixedString64Bytes(classId)
            });
            entityManager.SetComponentData(singleton, new DataLoadState { HasLoaded = 0 });

            if (entityManager.HasComponent<GameLoopState>(singleton))
            {
                entityManager.SetComponentData(singleton, new GameLoopState
                {
                    ElapsedTime = 0f,
                    Level = 1,
                    Experience = 0f,
                    NextLevelExperience = 10f,
                    IsRunActive = 0,
                    Status = RunStatus.Loading,
                    PendingLevelUps = 0
                });
            }

            if (entityManager.HasComponent<StageRuntimeConfig>(singleton))
            {
                StageRuntimeConfig stageRuntime = entityManager.GetComponentData<StageRuntimeConfig>(singleton);
                stageRuntime.IsStageCleared = 0;
                entityManager.SetComponentData(singleton, stageRuntime);
            }

            if (entityManager.HasComponent<BossSpawnState>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossSpawnState
                {
                    HasSpawnedBoss = 0,
                    BossKilled = 0,
                    ChestPending = 0,
                    ChestOpened = 0
                });
            }

            if (entityManager.HasComponent<RunResultStats>(singleton))
            {
                entityManager.SetComponentData(singleton, new RunResultStats
                {
                    KillCount = 0,
                    GoldEarned = 0,
                    SoulsEarned = 0,
                    CurrentWave = 0,
                    RewardCommitted = 0
                });
            }

            if (entityManager.HasComponent<BossRewardState>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossRewardState
                {
                    HasPendingReward = 0,
                    EvolutionResolved = 0
                });
            }

            if (entityManager.HasComponent<BossRewardConfirmRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new BossRewardConfirmRequest
                {
                    IsPending = 0
                });
            }

            if (entityManager.HasComponent<ResultSnapshot>(singleton))
            {
                entityManager.SetComponentData(singleton, new ResultSnapshot
                {
                    HasSnapshot = 0,
                    IsVictory = 0,
                    ElapsedTime = 0f,
                    FinalLevel = 1,
                    KillCount = 0,
                    GoldEarned = 0,
                    SoulsEarned = 0,
                    RewardGranted = 0
                });
            }

            if (entityManager.HasComponent<UpgradeSelectionRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new UpgradeSelectionRequest
                {
                    SelectedOptionIndex = -1,
                    HasSelection = 0,
                    RerollRequested = 0
                });
            }

            if (entityManager.HasComponent<RunNavigationRequest>(singleton))
            {
                entityManager.SetComponentData(singleton, new RunNavigationRequest
                {
                    Action = RunNavigationAction.None,
                    IsPending = 0
                });
            }
        }
    }
}
