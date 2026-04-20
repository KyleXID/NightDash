using System;
using System.Collections;
using System.Reflection;
using NightDash.ECS.Components;
using NightDash.Runtime;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NightDash.Tests.PlayMode
{
    public class ManualFlowPlayModeTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator Stage1_VerticalSlice_ManualFlowChecks()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("PlayMode scene-load는 batchmode에서 'Enter play mode before...' 제약으로 불가 — Unity Editor Test Runner 경로에서 실행 필요.");
            }

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            StartRun("stage_01", "class_warrior");
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).IsRunActive == 1 &&
                      em.GetComponentData<DataLoadState>(singleton).HasLoaded == 1 &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Playing,
                10f,
                "Run did not enter Playing after start.");

            TriggerLevelUp();
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.LevelUpSelection &&
                      GetUpgradeOptions(em, singleton).Length > 0 &&
                      IsActive<LevelUpSelectionUI>("_levelRoot"),
                6f,
                "LevelUpSelection did not appear.");

            ClickButton<LevelUpSelectionUI>("_rerollButton");
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<PlayerProgressionState>(singleton).RerollsRemaining == 0 &&
                      GetUpgradeOptions(em, singleton).Length > 0,
                4f,
                "Reroll did not consume correctly.");

            ClickIndexedButton<LevelUpSelectionUI>("_optionButtons", 0);
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Playing &&
                      !IsActive<LevelUpSelectionUI>("_levelRoot"),
                6f,
                "Selecting a level-up card did not return to Playing.");

            TriggerVictory();
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Victory &&
                      em.GetComponentData<BossRewardState>(singleton).HasPendingReward == 1 &&
                      IsActive<NightDashHudResultUI>("_rewardRoot"),
                6f,
                "Victory reward confirmation did not appear.");

            ClickButton<NightDashHudResultUI>("_rewardConfirmButton");
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Result &&
                      em.GetComponentData<ResultSnapshot>(singleton).HasSnapshot == 1 &&
                      em.GetComponentData<ResultSnapshot>(singleton).IsVictory == 1 &&
                      em.GetComponentData<BossRewardState>(singleton).HasPendingReward == 0 &&
                      IsActive<NightDashHudResultUI>("_resultRoot"),
                6f,
                "Victory did not progress from reward confirm to Result.");

            ClickButton<NightDashHudResultUI>("_retryButton");
            SyncLobbyNavigation();
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).IsRunActive == 1 &&
                      em.GetComponentData<DataLoadState>(singleton).HasLoaded == 1 &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Playing &&
                      em.GetComponentData<ResultSnapshot>(singleton).HasSnapshot == 0,
                10f,
                "Retry did not start a fresh run.");

            TriggerDefeat();
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).Status == RunStatus.Result &&
                      em.GetComponentData<ResultSnapshot>(singleton).HasSnapshot == 1 &&
                      em.GetComponentData<ResultSnapshot>(singleton).IsVictory == 0 &&
                      IsActive<NightDashHudResultUI>("_resultRoot"),
                6f,
                "Defeat did not progress to Result.");

            ClickButton<NightDashHudResultUI>("_menuButton");
            SyncLobbyNavigation();
            yield return WaitUntil(
                () => TryGetSingletonEntity(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<GameLoopState>(singleton).IsRunActive == 0 &&
                      RequireObject<RunSelectionLobbyUI>().IsLobbyVisible,
                6f,
                "ReturnToLobby did not bring the lobby back.");
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string failureMessage)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (predicate())
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
                yield return null;
            }

            Assert.Fail(failureMessage);
        }

        private static void StartRun(string stageId, string classId)
        {
            RunSelectionLobbyUI lobby = RequireObject<RunSelectionLobbyUI>();
            MethodInfo method = typeof(RunSelectionLobbyUI).GetMethod("StartRun", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "RunSelectionLobbyUI.StartRun reflection target not found.");
            method.Invoke(lobby, new object[] { stageId, classId });
        }

        private static void TriggerLevelUp()
        {
            Assert.That(TryGetSingletonEntity(out EntityManager em, out Entity singleton), Is.True, "Gameplay singleton not ready.");

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
            Assert.That(TryGetSingletonEntity(out EntityManager em, out Entity singleton), Is.True, "Gameplay singleton not ready.");

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
            Assert.That(TryGetSingletonEntity(out EntityManager em, out Entity singleton), Is.True, "Gameplay singleton not ready.");

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

        private static DynamicBuffer<UpgradeOptionElement> GetUpgradeOptions(EntityManager entityManager, Entity singleton)
        {
            return entityManager.GetBuffer<UpgradeOptionElement>(singleton);
        }

        private static void ClickButton<T>(string fieldName) where T : UnityEngine.Object
        {
            T instance = RequireObject<T>();
            Button button = GetField<Button>(instance, fieldName);
            Assert.That(button, Is.Not.Null, $"Button field '{fieldName}' not found.");
            button.onClick.Invoke();
        }

        private static void ClickIndexedButton<T>(string fieldName, int index) where T : UnityEngine.Object
        {
            T instance = RequireObject<T>();
            Button[] buttons = GetField<Button[]>(instance, fieldName);
            Assert.That(buttons, Is.Not.Null, $"Button array field '{fieldName}' not found.");
            Assert.That(index, Is.LessThan(buttons.Length));
            Assert.That(buttons[index], Is.Not.Null);
            buttons[index].onClick.Invoke();
        }

        private static void SyncLobbyNavigation()
        {
            RunSelectionLobbyUI lobby = RequireObject<RunSelectionLobbyUI>();
            MethodInfo method = typeof(RunSelectionLobbyUI).GetMethod("SyncLobbyVisibilityFromNavigationRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "RunSelectionLobbyUI.SyncLobbyVisibilityFromNavigationRequest reflection target not found.");
            method.Invoke(lobby, Array.Empty<object>());
        }

        private static bool IsActive<T>(string fieldName) where T : UnityEngine.Object
        {
            T instance = RequireObject<T>();
            GameObject root = GetField<GameObject>(instance, fieldName);
            return root != null && root.activeInHierarchy;
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
                ComponentType.ReadOnly<PlayerProgressionState>(),
                ComponentType.ReadOnly<UpgradeSelectionRequest>(),
                ComponentType.ReadOnly<RunResultStats>(),
                ComponentType.ReadOnly<ResultSnapshot>(),
                ComponentType.ReadOnly<BossRewardState>(),
                ComponentType.ReadOnly<BossRewardConfirmRequest>(),
                ComponentType.ReadOnly<RunNavigationRequest>(),
                ComponentType.ReadWrite<UpgradeOptionElement>());
            if (query.IsEmptyIgnoreFilter)
            {
                singleton = Entity.Null;
                return false;
            }

            singleton = query.GetSingletonEntity();
            return true;
        }

        private static T RequireObject<T>() where T : UnityEngine.Object
        {
            T instance = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            Assert.That(instance, Is.Not.Null, $"Required runtime object '{typeof(T).Name}' was not found.");
            return instance;
        }

        private static TField GetField<TField>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{instance.GetType().Name}'.");
            return (TField)field.GetValue(instance);
        }
    }
}
