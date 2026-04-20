// S4-02: Stage 1 신규 콘텐츠 PlayMode 시나리오 3종.
// 기존 ManualFlowPlayModeTests 패턴 (SampleScene load + reflection-assisted control) 확장.
// 주의: batchmode EditMode harness에서는 PlayMode 시나리오가 scene-load timing 제약으로
// skip/fail 할 수 있음 — Unity Editor Test Runner 권장 실행 경로.

using System;
using System.Collections;
using System.Reflection;
using NightDash.ECS.Components;
using NightDash.ECS.Systems;
using NightDash.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NightDash.Tests.PlayMode
{
    public class Stage1ContentPlayModeTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [SetUp]
        public void ClearSaveKeys()
        {
            PlayerPrefs.DeleteKey(SaveDataHelper.ConquestPointsKey);
            PlayerPrefs.DeleteKey(SaveDataHelper.ConquestChecksumKey);
            PlayerPrefs.DeleteKey(SaveDataHelper.SaveVersionKey);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------------
        // S4-02-A: 신규 클래스(paladin)로 런 시작 시 RunSelection/DataLoadState에
        // 올바른 classId가 반영되는지 검증.
        // ---------------------------------------------------------------------
        [UnityTest]
        public IEnumerator NewClass_Paladin_Loads_With_Correct_ClassId()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            StartRun("stage_01", "class_paladin");

            yield return WaitUntil(
                () => TryGetSingleton(out EntityManager em, out Entity singleton) &&
                      em.GetComponentData<DataLoadState>(singleton).HasLoaded == 1 &&
                      em.GetComponentData<RunSelection>(singleton).ClassId.ToString() == "class_paladin",
                10f,
                "class_paladin 선택이 RunSelection 싱글톤에 반영되지 않음");
        }

        // ---------------------------------------------------------------------
        // S4-02-B: SaveDataHelper round-trip — 런 없이 직접 호출로 세이브 영속 확인.
        // PlayMode 씬 부트 후 SaveDataHelper.Save → TryLoad 동일성 보장.
        // ---------------------------------------------------------------------
        [UnityTest]
        public IEnumerator SaveDataHelper_ConquestPoints_Persists_Across_Session()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            const int testPoints = 250;
            SaveDataHelper.Save(testPoints);
            yield return null;

            bool ok = SaveDataHelper.TryLoad(out int loaded);
            Assert.That(ok, Is.True, "저장 직후 TryLoad 성공해야 함");
            Assert.That(loaded, Is.EqualTo(testPoints));
        }

        // ---------------------------------------------------------------------
        // S4-02-C: RunSelection 지속성 — 스테이지/클래스 ID가 PlayerPrefs에
        // 유효 범위로 저장되고 재로드 시 복원되는지.
        // RunSelectionSession.Normalize 60-byte 가드도 간접 검증.
        // ---------------------------------------------------------------------
        [UnityTest]
        public IEnumerator RunSelection_Session_Preserves_Valid_Ids()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                SampleScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            RunSelectionSession.SetCurrent("stage_01", "class_archer");
            yield return null;

            RunSelectionSession.GetCurrent(out string stage, out string cls);
            Assert.That(stage, Is.EqualTo("stage_01"));
            Assert.That(cls, Is.EqualTo("class_archer"));

            // 과도한 길이 입력이 fallback으로 교체되는지 (S4-07 guard)
            string oversized = new string('a', 200);
            RunSelectionSession.SetCurrent(oversized, oversized);
            yield return null;

            RunSelectionSession.GetCurrent(out string stage2, out string cls2);
            Assert.That(stage2, Is.EqualTo("stage_01"), "60 bytes 초과 stageId는 stage_01 기본값으로 fallback");
            Assert.That(cls2, Is.EqualTo("class_warrior"), "60 bytes 초과 classId는 class_warrior 기본값으로 fallback");
        }

        // ---------------------------------------------------------------------
        // 공통 유틸 (ManualFlowPlayModeTests와 동일 헬퍼 로컬 복제)
        // ---------------------------------------------------------------------
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
            RunSelectionLobbyUI lobby = UnityEngine.Object.FindFirstObjectByType<RunSelectionLobbyUI>(FindObjectsInactive.Include);
            Assert.That(lobby, Is.Not.Null, "RunSelectionLobbyUI 인스턴스가 씬에 필요함");

            MethodInfo method = typeof(RunSelectionLobbyUI).GetMethod(
                "StartRun",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "StartRun(string,string) 리플렉션 타겟 필요");
            method.Invoke(lobby, new object[] { stageId, classId });
        }

        private static bool TryGetSingleton(out EntityManager em, out Entity singleton)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                em = default;
                singleton = Entity.Null;
                return false;
            }

            em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<RunSelection>(),
                ComponentType.ReadOnly<DataLoadState>());
            if (query.IsEmptyIgnoreFilter)
            {
                singleton = Entity.Null;
                return false;
            }

            singleton = query.GetSingletonEntity();
            return true;
        }
    }
}
