using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.ECS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace NightDash.Runtime
{
    /// <summary>
    /// GDD(ops/03_tutorial_onboarding_script.md) 6 트리거를 관측해 IMGUI 토스트로
    /// 띄우는 v1 브리지. 각 트리거는 런 당 1회 + 카테고리 쿨다운 준수.
    /// ECS 월드 상태(GameLoopState/BossSpawnState/CombatStats+PlayerTag) 폴링 +
    /// NightDashCombatEvents.OnEnemyKilled 구독 결합.
    /// </summary>
    public sealed class NightDashTutorialBridge : MonoBehaviour
    {
        [SerializeField] private TutorialConfig config;
        [SerializeField] private bool tutorialEnabled = true;

        private readonly HashSet<TutorialTrigger> _shownThisRun = new();
        private readonly Dictionary<TutorialTrigger, float> _lastShownTime = new();

        private int _enemyKillsThisRun;
        private int _lastLevelSeen = 1;
        private bool _wasRunActive;
        private float _runStartAt;

        private string _activeMessage;
        private float _messageExpiresAt;

        private void OnEnable()
        {
            NightDashCombatEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            NightDashCombatEvents.OnEnemyKilled -= HandleEnemyKilled;
        }

        private void Update()
        {
            if (!tutorialEnabled || config == null)
            {
                return;
            }

            PollWorldState();

            if (_activeMessage != null && Time.unscaledTime >= _messageExpiresAt)
            {
                _activeMessage = null;
            }
        }

        private void OnGUI()
        {
            if (_activeMessage == null)
            {
                return;
            }

            const float w = 520f;
            const float h = 70f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.18f;
            GUI.Box(new Rect(x, y, w, h), _activeMessage);
        }

        private void HandleEnemyKilled(float3 position, bool isBoss)
        {
            _enemyKillsThisRun++;
            if (_enemyKillsThisRun == 1)
            {
                TryFire(TutorialTrigger.FirstKill);
            }
        }

        private void PollWorldState()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager em = world.EntityManager;

            if (TryGetGameLoop(em, out GameLoopState loop))
            {
                bool isRunActive = loop.IsRunActive != 0;
                if (isRunActive && !_wasRunActive)
                {
                    ResetRunScopedState();
                    _runStartAt = Time.unscaledTime;
                    TryFire(TutorialTrigger.RunStart);
                }
                else if (!isRunActive && _wasRunActive)
                {
                    ResetRunScopedState();
                }
                _wasRunActive = isRunActive;

                if (loop.Level > _lastLevelSeen)
                {
                    _lastLevelSeen = loop.Level;
                    if (loop.Level == 2)
                    {
                        TryFire(TutorialTrigger.FirstLevelUp);
                    }
                }
            }

            if (TryGetBossSpawn(em, out BossSpawnState boss) && boss.ChestOpened == 1)
            {
                TryFire(TutorialTrigger.BossChestOpen);
            }

            if (TryGetPlayerHpRatio(em, out float hpRatio) && hpRatio < 0.4f)
            {
                TryFire(TutorialTrigger.LowHp);
            }

            if (TryDetectEliteSpawn(em))
            {
                TryFire(TutorialTrigger.EliteSpawn);
            }
        }

        private void TryFire(TutorialTrigger trigger)
        {
            if (_shownThisRun.Contains(trigger))
            {
                return;
            }

            if (_lastShownTime.TryGetValue(trigger, out float last) &&
                Time.unscaledTime - last < config.rePromptCooldownSeconds)
            {
                return;
            }

            string message = Resolve(trigger);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _activeMessage = message;
            _messageExpiresAt = Time.unscaledTime + config.displaySeconds;
            _shownThisRun.Add(trigger);
            _lastShownTime[trigger] = Time.unscaledTime;
        }

        private string Resolve(TutorialTrigger trigger)
        {
            foreach (TutorialEntry entry in config.entries)
            {
                if (entry.trigger == trigger)
                {
                    return entry.message;
                }
            }
            return null;
        }

        private void ResetRunScopedState()
        {
            _shownThisRun.Clear();
            _enemyKillsThisRun = 0;
            _lastLevelSeen = 1;
        }

        private static bool TryGetGameLoop(EntityManager em, out GameLoopState loop)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameLoopState>());
            if (query.IsEmptyIgnoreFilter)
            {
                loop = default;
                return false;
            }
            loop = em.GetComponentData<GameLoopState>(query.GetSingletonEntity());
            return true;
        }

        private static bool TryGetBossSpawn(EntityManager em, out BossSpawnState boss)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<BossSpawnState>());
            if (query.IsEmptyIgnoreFilter)
            {
                boss = default;
                return false;
            }
            boss = em.GetComponentData<BossSpawnState>(query.GetSingletonEntity());
            return true;
        }

        private static bool TryGetPlayerHpRatio(EntityManager em, out float ratio)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<CombatStats>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                CombatStats stats = em.GetComponentData<CombatStats>(entities[i]);
                if (stats.MaxHealth > 0f)
                {
                    ratio = stats.CurrentHealth / stats.MaxHealth;
                    return true;
                }
            }
            ratio = 1f;
            return false;
        }

        private static bool TryDetectEliteSpawn(EntityManager em)
        {
            // v1: EliteTag 컴포넌트가 아직 없으므로, 엘리트 전용 EnemyArchetype 플래그가
            // 붙는 시점이 S4 콘텐츠 단계. 현재는 폴링 비활성(구현 확장 포인트).
            return false;
        }
    }
}
