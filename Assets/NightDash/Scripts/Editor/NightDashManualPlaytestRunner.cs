using System;
using System.Reflection;
using NightDash.ECS.Components;
using NightDash.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;

namespace NightDash.Editor
{
    public static class NightDashManualPlaytestRunner
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ActiveKey = "NightDash.ManualPlaytest.Active";
        private const string StepKey = "NightDash.ManualPlaytest.Step";
        private const string DeadlineKey = "NightDash.ManualPlaytest.Deadline";
        private const string FailureKey = "NightDash.ManualPlaytest.Failure";

        private enum Step
        {
            Idle,
            StartRun,
            WaitForRunStart,
            TriggerLevelUp,
            WaitForLevelUp,
            ClickReroll,
            WaitForReroll,
            ClickLevelOption,
            WaitForLevelResume,
            TriggerPassiveLevelUp,
            WaitForPassiveLevelUp,
            ClickPassiveOption,
            WaitForPassiveResume,
            ForceBrutePhase,
            WaitForBrutePhaseEnemy,
            ForceCasterPhase,
            WaitForCasterPhaseEnemy,
            TriggerVictory,
            WaitForRewardModal,
            ConfirmReward,
            WaitForVictoryResult,
            ClickRetry,
            WaitForRetryStart,
            TriggerDefeat,
            WaitForDefeatResult,
            ClickReturnToLobby,
            WaitForLobby,
            Completed,
            Failed
        }

        private static Step _step;
        private static double _deadline;
        private static string _failureReason;
        private static int _ownedWeaponCountBeforeLevel;
        private static bool _selectedNewWeaponOption;
        private static float _passiveCheckMaxHealthBefore;
        private static float _passiveCheckDamageBefore;
        private static float _passiveCheckMoveSpeedBefore;
        private static float _passiveCheckCooldownBefore;
        private static float _passiveCheckRangeBefore;
        private static float _passiveCheckProjectileSpeedBefore;
        private static bool _hasSeenBrutePhaseEnemy;
        private static bool _hasSeenCasterPhaseEnemy;

        [InitializeOnLoadMethod]
        private static void RestoreRunner()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            _step = (Step)SessionState.GetInt(StepKey, (int)Step.Idle);
            _deadline = double.TryParse(SessionState.GetString(DeadlineKey, "0"), out double savedDeadline)
                ? savedDeadline
                : EditorApplication.timeSinceStartup + 10d;
            _failureReason = SessionState.GetString(FailureKey, string.Empty);
            Debug.Log($"[NightDash][Playtest] Restore runner at step {_step}");
            RegisterCallbacks();
        }

        [MenuItem("NightDash/QA/Run Manual Flow Playtest")]
        public static void RunFromMenu()
        {
            RunStage1ManualFlowPlaytest();
        }

        public static void RunStage1ManualFlowPlaytest()
        {
            _failureReason = string.Empty;
            _step = Step.StartRun;
            _deadline = EditorApplication.timeSinceStartup + 20d;
            PersistState();
            RegisterCallbacks();
            Debug.Log("[NightDash][Playtest] Scheduled manual flow runner.");
            EditorApplication.delayCall += BeginPlaytest;
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void BeginPlaytest()
        {
            EditorApplication.delayCall -= BeginPlaytest;
            Debug.Log("[NightDash][Playtest] Opening sample scene and entering Play Mode.");
            EditorSceneManager.OpenScene(SampleScenePath);
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(StepKey);
            SessionState.EraseString(DeadlineKey);
            SessionState.EraseString(FailureKey);
            EditorApplication.update -= Update;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (_step == Step.Completed)
            {
                Debug.Log("[NightDash][Playtest] All requested manual flow checks passed.");
                EditorApplication.Exit(0);
                return;
            }

            if (string.IsNullOrEmpty(_failureReason))
            {
                _failureReason = "Play mode exited before the requested flow checks completed.";
            }

            Debug.LogError($"[NightDash][Playtest] {_failureReason}");
            EditorApplication.Exit(1);
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                if (_step == Step.StartRun)
                {
                    BeginPlaytest();
                }
                return;
            }

            try
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    Fail($"Timed out while waiting for step '{_step}'.");
                    return;
                }

                switch (_step)
                {
                    case Step.StartRun:
                        StartRun();
                        Advance(Step.WaitForRunStart, 20d);
                        break;
                    case Step.WaitForRunStart:
                        if (TryGetSingletonEntity(out EntityManager em, out Entity singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            DataLoadState load = em.GetComponentData<DataLoadState>(singleton);
                            if (loop.IsRunActive == 1 && load.HasLoaded == 1 && loop.Status == RunStatus.Playing)
                            {
                                if (HasSpawnedStage1EnemyArchetype(em))
                                {
                                    Advance(Step.TriggerLevelUp, 6d);
                                }
                            }
                        }
                        break;
                    case Step.TriggerLevelUp:
                        _ownedWeaponCountBeforeLevel = GetOwnedWeaponCount();
                        _selectedNewWeaponOption = false;
                        TriggerLevelUp();
                        Advance(Step.WaitForLevelUp, 8d);
                        break;
                    case Step.WaitForLevelUp:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            if (loop.Status == RunStatus.LevelUpSelection && IsLevelUpVisible())
                            {
                                AssertUpgradeOptionCount(em, singleton, minimum: 1);
                                LogUpgradeOptions(em, singleton, "first level-up");
                                Advance(Step.ClickReroll, 4d);
                            }
                        }
                        break;
                    case Step.ClickReroll:
                        ClickLevelReroll();
                        Advance(Step.WaitForReroll, 4d);
                        break;
                    case Step.WaitForReroll:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            PlayerProgressionState progression = em.GetComponentData<PlayerProgressionState>(singleton);
                            if (progression.RerollsRemaining == 0)
                            {
                                AssertUpgradeOptionCount(em, singleton, minimum: 1);
                                Advance(Step.ClickLevelOption, 4d);
                            }
                        }
                        break;
                    case Step.ClickLevelOption:
                        int weaponOptionIndex = FindPreferredLevelOptionIndex();
                        _selectedNewWeaponOption = weaponOptionIndex >= 0;
                        ClickLevelOption(weaponOptionIndex >= 0 ? weaponOptionIndex : 0);
                        Advance(Step.WaitForLevelResume, 6d);
                        break;
                    case Step.WaitForLevelResume:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            int ownedWeaponCount = em.GetBuffer<OwnedWeaponElement>(singleton).Length;
                            if (loop.Status == RunStatus.Playing &&
                                !IsLevelUpVisible() &&
                                ownedWeaponCount >= _ownedWeaponCountBeforeLevel)
                            {
                                if (_selectedNewWeaponOption && ownedWeaponCount <= _ownedWeaponCountBeforeLevel)
                                {
                                    throw new InvalidOperationException("Expected a new weapon selection to increase owned weapon count.");
                                }

                                CapturePassiveBaseline();
                                Advance(Step.TriggerPassiveLevelUp, 4d);
                            }
                        }
                        break;
                    case Step.TriggerPassiveLevelUp:
                        TriggerLevelUp();
                        Advance(Step.WaitForPassiveLevelUp, 6d);
                        break;
                    case Step.WaitForPassiveLevelUp:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            if (loop.Status == RunStatus.LevelUpSelection && IsLevelUpVisible())
                            {
                                LogUpgradeOptions(em, singleton, "passive verification level-up");
                                int passiveIndex = FindPreferredPassiveOptionIndex();
                                if (passiveIndex < 0)
                                {
                                    throw new InvalidOperationException("No passive option was available for passive runtime verification.");
                                }

                                Advance(Step.ClickPassiveOption, 4d);
                            }
                        }
                        break;
                    case Step.ClickPassiveOption:
                        ClickLevelOption(FindPreferredPassiveOptionIndex());
                        Advance(Step.WaitForPassiveResume, 6d);
                        break;
                    case Step.WaitForPassiveResume:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            if (loop.Status == RunStatus.Playing && !IsLevelUpVisible())
                            {
                                AssertPassiveRuntimeChanged();
                                Advance(Step.ForceBrutePhase, 2d);
                            }
                        }
                        break;
                    case Step.ForceBrutePhase:
                        ForceSpawnPhase(elapsedTime: 240f);
                        _hasSeenBrutePhaseEnemy = false;
                        Advance(Step.WaitForBrutePhaseEnemy, 20d);
                        break;
                    case Step.WaitForBrutePhaseEnemy:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            if (loop.Status != RunStatus.Playing)
                            {
                                throw new InvalidOperationException("Run left Playing state while verifying brute spawn phase.");
                            }

                            _hasSeenBrutePhaseEnemy |= HasSpawnedEnemyArchetype(em, "wasteland_brute");
                            if (_hasSeenBrutePhaseEnemy)
                            {
                                Advance(Step.ForceCasterPhase, 2d);
                            }
                        }
                        break;
                    case Step.ForceCasterPhase:
                        ForceSpawnPhase(elapsedTime: 480f);
                        _hasSeenCasterPhaseEnemy = false;
                        Advance(Step.WaitForCasterPhaseEnemy, 20d);
                        break;
                    case Step.WaitForCasterPhaseEnemy:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            if (loop.Status != RunStatus.Playing)
                            {
                                throw new InvalidOperationException("Run left Playing state while verifying caster spawn phase.");
                            }

                            _hasSeenCasterPhaseEnemy |= HasSpawnedEnemyArchetype(em, "ash_caster");
                            if (_hasSeenCasterPhaseEnemy)
                            {
                                Advance(Step.TriggerVictory, 4d);
                            }
                        }
                        break;
                    case Step.TriggerVictory:
                        TriggerVictory();
                        Advance(Step.WaitForRewardModal, 6d);
                        break;
                    case Step.WaitForRewardModal:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            BossRewardState reward = em.GetComponentData<BossRewardState>(singleton);
                            if (loop.Status == RunStatus.Victory && reward.HasPendingReward == 1 && IsRewardVisible())
                            {
                                Advance(Step.ConfirmReward, 4d);
                            }
                        }
                        break;
                    case Step.ConfirmReward:
                        ClickRewardConfirm();
                        Advance(Step.WaitForVictoryResult, 6d);
                        break;
                    case Step.WaitForVictoryResult:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            ResultSnapshot snapshot = em.GetComponentData<ResultSnapshot>(singleton);
                            BossRewardState reward = em.GetComponentData<BossRewardState>(singleton);
                            if (loop.Status == RunStatus.Result &&
                                snapshot.HasSnapshot == 1 &&
                                snapshot.IsVictory == 1 &&
                                reward.HasPendingReward == 0 &&
                                IsResultVisible())
                            {
                                Advance(Step.ClickRetry, 4d);
                            }
                        }
                        break;
                    case Step.ClickRetry:
                        ClickResultButton("_retryButton");
                        SyncLobbyNavigation();
                        Advance(Step.WaitForRetryStart, 10d);
                        break;
                    case Step.WaitForRetryStart:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            DataLoadState load = em.GetComponentData<DataLoadState>(singleton);
                            ResultSnapshot snapshot = em.GetComponentData<ResultSnapshot>(singleton);
                            if (loop.IsRunActive == 1 &&
                                load.HasLoaded == 1 &&
                                loop.Status == RunStatus.Playing &&
                                snapshot.HasSnapshot == 0)
                            {
                                Advance(Step.TriggerDefeat, 4d);
                            }
                        }
                        break;
                    case Step.TriggerDefeat:
                        TriggerDefeat();
                        Advance(Step.WaitForDefeatResult, 6d);
                        break;
                    case Step.WaitForDefeatResult:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            ResultSnapshot snapshot = em.GetComponentData<ResultSnapshot>(singleton);
                            if (snapshot.HasSnapshot == 1 &&
                                snapshot.IsVictory == 0 &&
                                loop.Status == RunStatus.Result &&
                                IsResultVisible())
                            {
                                Advance(Step.ClickReturnToLobby, 4d);
                            }
                        }
                        break;
                    case Step.ClickReturnToLobby:
                        ClickResultButton("_menuButton");
                        SyncLobbyNavigation();
                        Advance(Step.WaitForLobby, 6d);
                        break;
                    case Step.WaitForLobby:
                        if (TryGetSingletonEntity(out em, out singleton))
                        {
                            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
                            RunSelectionLobbyUI lobby = RequireObject<RunSelectionLobbyUI>();
                            if (loop.IsRunActive == 0 && lobby.IsLobbyVisible)
                            {
                                _step = Step.Completed;
                                EditorApplication.isPlaying = false;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        private static void StartRun()
        {
            NightDashTitleScreenUI titleUi = UnityEngine.Object.FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (titleUi != null && titleUi.gameObject.activeInHierarchy)
            {
                MethodInfo titleStart = typeof(NightDashTitleScreenUI).GetMethod("OnStartClicked", BindingFlags.Instance | BindingFlags.NonPublic);
                if (titleStart == null)
                {
                    throw new InvalidOperationException("NightDashTitleScreenUI.OnStartClicked reflection target not found.");
                }

                titleStart.Invoke(titleUi, Array.Empty<object>());
            }

            RunSelectionLobbyUI lobby = RequireObject<RunSelectionLobbyUI>();
            MethodInfo method = typeof(RunSelectionLobbyUI).GetMethod("StartRun", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("RunSelectionLobbyUI.StartRun reflection target not found.");
            }

            method.Invoke(lobby, new object[] { "stage_01", "class_warrior" });
        }

        private static void TriggerLevelUp()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready.");
            }

            UpgradeSelectionRequest request = em.GetComponentData<UpgradeSelectionRequest>(singleton);
            request.SelectedOptionIndex = -1;
            request.HasSelection = 0;
            request.RerollRequested = 0;
            em.SetComponentData(singleton, request);
            em.GetBuffer<UpgradeOptionElement>(singleton).Clear();

            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
            loop.Status = RunStatus.Playing;
            loop.PendingLevelUps = 1;
            em.SetComponentData(singleton, loop);
        }

        private static void TriggerVictory()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready.");
            }

            BossSpawnState boss = em.GetComponentData<BossSpawnState>(singleton);
            boss.BossKilled = 1;
            boss.ChestPending = 1;
            boss.ChestOpened = 0;
            em.SetComponentData(singleton, boss);

            BossRewardState reward = em.GetComponentData<BossRewardState>(singleton);
            reward.HasPendingReward = 1;
            reward.EvolutionResolved = 0;
            em.SetComponentData(singleton, reward);

            BossRewardConfirmRequest confirm = em.GetComponentData<BossRewardConfirmRequest>(singleton);
            confirm.IsPending = 0;
            em.SetComponentData(singleton, confirm);

            ResultSnapshot snapshot = em.GetComponentData<ResultSnapshot>(singleton);
            snapshot.HasSnapshot = 0;
            em.SetComponentData(singleton, snapshot);

            RunResultStats result = em.GetComponentData<RunResultStats>(singleton);
            result.RewardCommitted = 0;
            result.KillCount = Math.Max(1, result.KillCount);
            result.GoldEarned = Math.Max(5, result.GoldEarned);
            result.SoulsEarned = Math.Max(2, result.SoulsEarned);
            em.SetComponentData(singleton, result);

            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
            loop.Status = RunStatus.Playing;
            loop.IsRunActive = 0;
            em.SetComponentData(singleton, loop);
        }

        private static void TriggerDefeat()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready.");
            }

            BossSpawnState boss = em.GetComponentData<BossSpawnState>(singleton);
            boss.BossKilled = 0;
            boss.ChestPending = 0;
            boss.ChestOpened = 0;
            em.SetComponentData(singleton, boss);

            BossRewardState reward = em.GetComponentData<BossRewardState>(singleton);
            reward.HasPendingReward = 0;
            reward.EvolutionResolved = 0;
            em.SetComponentData(singleton, reward);

            ResultSnapshot snapshot = em.GetComponentData<ResultSnapshot>(singleton);
            snapshot.HasSnapshot = 0;
            em.SetComponentData(singleton, snapshot);

            RunResultStats result = em.GetComponentData<RunResultStats>(singleton);
            result.RewardCommitted = 0;
            em.SetComponentData(singleton, result);

            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
            loop.Status = RunStatus.Playing;
            loop.IsRunActive = 0;
            em.SetComponentData(singleton, loop);
        }

        private static void ClickLevelReroll()
        {
            LevelUpSelectionUI ui = RequireObject<LevelUpSelectionUI>();
            Button rerollButton = GetField<Button>(ui, "_rerollButton");
            rerollButton.onClick.Invoke();
        }

        private static void ClickLevelOption(int index)
        {
            LevelUpSelectionUI ui = RequireObject<LevelUpSelectionUI>();
            Button[] buttons = GetField<Button[]>(ui, "_optionButtons");
            if (index < 0 || index >= buttons.Length || buttons[index] == null || !buttons[index].gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException($"Level option button {index} is not available.");
            }

            buttons[index].onClick.Invoke();
        }

        private static int FindPreferredLevelOptionIndex()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                return -1;
            }

            DynamicBuffer<UpgradeOptionElement> options = em.GetBuffer<UpgradeOptionElement>(singleton);
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == UpgradeKind.Weapon && options[i].CurrentLevel == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindPreferredPassiveOptionIndex()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                return -1;
            }

            DynamicBuffer<UpgradeOptionElement> options = em.GetBuffer<UpgradeOptionElement>(singleton);
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == UpgradeKind.Passive && options[i].CurrentLevel == 0)
                {
                    return i;
                }
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Kind == UpgradeKind.Passive)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ClickRewardConfirm()
        {
            NightDashHudResultUI ui = RequireObject<NightDashHudResultUI>();
            Button button = GetField<Button>(ui, "_rewardConfirmButton");
            button.onClick.Invoke();
        }

        private static void ClickResultButton(string fieldName)
        {
            NightDashHudResultUI ui = RequireObject<NightDashHudResultUI>();
            Button button = GetField<Button>(ui, fieldName);
            button.onClick.Invoke();
        }

        private static void SyncLobbyNavigation()
        {
            RunSelectionLobbyUI lobby = RequireObject<RunSelectionLobbyUI>();
            MethodInfo method = typeof(RunSelectionLobbyUI).GetMethod("SyncLobbyVisibilityFromNavigationRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("RunSelectionLobbyUI.SyncLobbyVisibilityFromNavigationRequest reflection target not found.");
            }

            method.Invoke(lobby, Array.Empty<object>());
        }

        private static bool IsLevelUpVisible()
        {
            LevelUpSelectionUI ui = RequireObject<LevelUpSelectionUI>();
            GameObject root = GetField<GameObject>(ui, "_levelRoot");
            return root != null && root.activeInHierarchy;
        }

        private static bool IsRewardVisible()
        {
            NightDashHudResultUI ui = RequireObject<NightDashHudResultUI>();
            GameObject root = GetField<GameObject>(ui, "_rewardRoot");
            return root != null && root.activeInHierarchy;
        }

        private static bool IsResultVisible()
        {
            NightDashHudResultUI ui = RequireObject<NightDashHudResultUI>();
            GameObject root = GetField<GameObject>(ui, "_resultRoot");
            return root != null && root.activeInHierarchy;
        }

        private static void AssertUpgradeOptionCount(EntityManager em, Entity singleton, int minimum)
        {
            DynamicBuffer<UpgradeOptionElement> options = em.GetBuffer<UpgradeOptionElement>(singleton);
            if (options.Length < minimum)
            {
                throw new InvalidOperationException($"Expected at least {minimum} level-up options but found {options.Length}.");
            }
        }

        private static void LogUpgradeOptions(EntityManager em, Entity singleton, string label)
        {
            DynamicBuffer<UpgradeOptionElement> options = em.GetBuffer<UpgradeOptionElement>(singleton);
            for (int i = 0; i < options.Length; i++)
            {
                UpgradeOptionElement option = options[i];
                Debug.Log(
                    $"[NightDash][Playtest] {label} option[{i}] kind={option.Kind} id={option.Id} current={option.CurrentLevel} next={option.NextLevel} max={option.MaxLevel}");
            }
        }

        private static bool TryGetSingletonEntity(out EntityManager entityManager, out Entity singleton)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                singleton = Entity.Null;
                return false;
            }

            entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameLoopState>(),
                ComponentType.ReadOnly<DataLoadState>(),
                ComponentType.ReadOnly<RunResultStats>(),
                ComponentType.ReadOnly<ResultSnapshot>(),
                ComponentType.ReadOnly<BossRewardState>(),
                ComponentType.ReadOnly<BossRewardConfirmRequest>(),
                ComponentType.ReadOnly<RunNavigationRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                singleton = Entity.Null;
                return false;
            }

            singleton = query.GetSingletonEntity();
            return true;
        }

        private static bool HasSpawnedStage1EnemyArchetype(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<EnemyArchetypeData>(),
                ComponentType.Exclude<BossTag>());

            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using var enemies = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyArchetypeData archetype = entityManager.GetComponentData<EnemyArchetypeData>(enemies[i]);
                string enemyId = archetype.Id.ToString();
                if (enemyId == "ghoul_scout" || enemyId == "ember_bat")
                {
                    return true;
                }
            }

            throw new InvalidOperationException("Stage 1 opening phase spawned an unexpected enemy archetype.");
        }

        private static bool HasSpawnedEnemyArchetype(EntityManager entityManager, string expectedEnemyId)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<EnemyArchetypeData>(),
                ComponentType.Exclude<BossTag>());

            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            using var enemies = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyArchetypeData archetype = entityManager.GetComponentData<EnemyArchetypeData>(enemies[i]);
                if (archetype.Id.ToString() == expectedEnemyId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ForceSpawnPhase(float elapsedTime)
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready for spawn phase forcing.");
            }

            using (var enemyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyTag>()))
            using (var projectileQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileData>()))
            using (var playerQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<PlayerTag>(),
                       ComponentType.ReadWrite<CombatStats>(),
                       ComponentType.ReadWrite<WeaponRuntimeData>()))
            {
                using var enemies = enemyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (em.Exists(enemies[i]))
                    {
                        em.DestroyEntity(enemies[i]);
                    }
                }

                using var projectiles = projectileQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < projectiles.Length; i++)
                {
                    if (em.Exists(projectiles[i]))
                    {
                        em.DestroyEntity(projectiles[i]);
                    }
                }

                if (!playerQuery.IsEmptyIgnoreFilter)
                {
                    Entity player = playerQuery.GetSingletonEntity();
                    CombatStats playerStats = em.GetComponentData<CombatStats>(player);
                    playerStats.MaxHealth = Mathf.Max(playerStats.MaxHealth, 999f);
                    playerStats.CurrentHealth = playerStats.MaxHealth;
                    em.SetComponentData(player, playerStats);

                    WeaponRuntimeData weapon = em.GetComponentData<WeaponRuntimeData>(player);
                    weapon.Damage = 0f;
                    weapon.Cooldown = Mathf.Max(weapon.Cooldown, 999f);
                    weapon.CooldownRemaining = weapon.Cooldown;
                    em.SetComponentData(player, weapon);
                }
            }

            GameLoopState loop = em.GetComponentData<GameLoopState>(singleton);
            loop.Status = RunStatus.Playing;
            loop.IsRunActive = 1;
            loop.ElapsedTime = elapsedTime;
            em.SetComponentData(singleton, loop);

            EnemySpawnConfig spawn = em.GetComponentData<EnemySpawnConfig>(singleton);
            spawn.SpawnInterval = Mathf.Min(spawn.SpawnInterval, 0.1f);
            spawn.SpawnTimer = 0f;
            em.SetComponentData(singleton, spawn);

            Debug.Log($"[NightDash][Playtest] Forced spawn phase at elapsed={elapsedTime:0.0}s");
        }

        private static int GetOwnedWeaponCount()
        {
            return TryGetSingletonEntity(out EntityManager em, out Entity singleton)
                ? em.GetBuffer<OwnedWeaponElement>(singleton).Length
                : 0;
        }

        private static void CapturePassiveBaseline()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready for passive baseline capture.");
            }

            EntityQuery playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<CombatStats>(),
                ComponentType.ReadOnly<WeaponRuntimeData>());
            if (playerQuery.IsEmptyIgnoreFilter)
            {
                throw new InvalidOperationException("Player entity not found for passive baseline capture.");
            }

            Entity player = playerQuery.GetSingletonEntity();
            CombatStats stats = em.GetComponentData<CombatStats>(player);
            WeaponRuntimeData weapon = em.GetComponentData<WeaponRuntimeData>(player);
            _passiveCheckMaxHealthBefore = stats.MaxHealth;
            _passiveCheckDamageBefore = stats.Damage;
            _passiveCheckMoveSpeedBefore = stats.MoveSpeed;
            _passiveCheckCooldownBefore = weapon.Cooldown;
            _passiveCheckRangeBefore = weapon.Range;
            _passiveCheckProjectileSpeedBefore = weapon.ProjectileSpeed;
        }

        private static void AssertPassiveRuntimeChanged()
        {
            if (!TryGetSingletonEntity(out EntityManager em, out Entity singleton))
            {
                throw new InvalidOperationException("Gameplay singleton not ready for passive runtime assertion.");
            }

            EntityQuery playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<CombatStats>(),
                ComponentType.ReadOnly<WeaponRuntimeData>());
            if (playerQuery.IsEmptyIgnoreFilter)
            {
                throw new InvalidOperationException("Player entity not found for passive runtime assertion.");
            }

            Entity player = playerQuery.GetSingletonEntity();
            CombatStats stats = em.GetComponentData<CombatStats>(player);
            WeaponRuntimeData weapon = em.GetComponentData<WeaponRuntimeData>(player);
            bool changed =
                !Mathf.Approximately(_passiveCheckMaxHealthBefore, stats.MaxHealth) ||
                !Mathf.Approximately(_passiveCheckDamageBefore, stats.Damage) ||
                !Mathf.Approximately(_passiveCheckMoveSpeedBefore, stats.MoveSpeed) ||
                !Mathf.Approximately(_passiveCheckCooldownBefore, weapon.Cooldown) ||
                !Mathf.Approximately(_passiveCheckRangeBefore, weapon.Range) ||
                !Mathf.Approximately(_passiveCheckProjectileSpeedBefore, weapon.ProjectileSpeed);

            if (!changed)
            {
                throw new InvalidOperationException("Passive selection did not change any player or weapon runtime stat.");
            }
        }

        private static T RequireObject<T>() where T : UnityEngine.Object
        {
            T instance = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (instance == null)
            {
                throw new InvalidOperationException($"Required runtime object '{typeof(T).Name}' was not found.");
            }

            return instance;
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"Field '{fieldName}' not found on '{instance.GetType().Name}'.");
            }

            if (field.GetValue(instance) is not T value)
            {
                throw new InvalidOperationException($"Field '{fieldName}' on '{instance.GetType().Name}' is not '{typeof(T).Name}'.");
            }

            return value;
        }

        private static void Advance(Step nextStep, double timeoutSeconds)
        {
            _step = nextStep;
            _deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            PersistState();
            Debug.Log($"[NightDash][Playtest] Step -> {_step}");
        }

        private static void Fail(string reason)
        {
            _failureReason = reason;
            _step = Step.Failed;
            PersistState();
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            else
            {
                EditorApplication.Exit(1);
            }
        }

        private static void PersistState()
        {
            SessionState.SetBool(ActiveKey, _step != Step.Completed && _step != Step.Failed);
            SessionState.SetInt(StepKey, (int)_step);
            SessionState.SetString(DeadlineKey, _deadline.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SessionState.SetString(FailureKey, _failureReason ?? string.Empty);
        }
    }
}
