using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(DataBootstrapSystem))]
    public partial struct RunSelectionOverrideSystem : ISystem
    {
        // Scratch storage so we don't allocate every override.
        private static readonly List<(string id, int level)> s_ModifierStageScratch = new();
        private static readonly List<DifficultyModifierData> s_ModifierSoScratch = new();
        private static readonly List<int> s_ModifierLevelScratch = new();

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
            Entity selectionEntity = SystemAPI.GetSingletonEntity<RunSelection>();

            ApplyDifficultyModifiers(ref state, selectionEntity);

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

        // Replace the entity's DifficultyModifierElement buffer with the lobby's
        // selected modifiers (resolved via DataRegistry + stage level lookup).
        // Also pushes the SO+level pairs into the HUD strip so the gameplay UI
        // mirrors the actual active modifiers (instead of the Resources fallback).
        private static void ApplyDifficultyModifiers(ref SystemState state, Entity entity)
        {
            RunSelectionSession.GetCurrentModifierStages(s_ModifierStageScratch);

            DataRegistry registry = DataRegistry.Instance;
            s_ModifierSoScratch.Clear();
            s_ModifierLevelScratch.Clear();
            var stageResolved = new List<DifficultyStage>(s_ModifierStageScratch.Count);
            if (registry != null && s_ModifierStageScratch.Count > 0)
            {
                for (int i = 0; i < s_ModifierStageScratch.Count; i++)
                {
                    (string id, int level) entry = s_ModifierStageScratch[i];
                    if (!registry.TryGetDifficulty(entry.id, out DifficultyModifierData data) || data == null) continue;
                    if (!data.TryGetStage(entry.level, out DifficultyStage stage)) continue;
                    s_ModifierSoScratch.Add(data);
                    s_ModifierLevelScratch.Add(entry.level);
                    stageResolved.Add(stage);
                }
            }

            if (!state.EntityManager.HasBuffer<DifficultyModifierElement>(entity))
            {
                state.EntityManager.AddBuffer<DifficultyModifierElement>(entity);
            }
            DynamicBuffer<DifficultyModifierElement> buffer =
                state.EntityManager.GetBuffer<DifficultyModifierElement>(entity);
            buffer.Clear();

            for (int i = 0; i < stageResolved.Count; i++)
            {
                DifficultyStage stage = stageResolved[i];
                buffer.Add(new DifficultyModifierElement
                {
                    RiskScore = stage.riskPoint,
                    RewardMultiplierBonus = stage.rewardBonusPct,
                    HpPct = stage.enemyModifiers.hpPct,
                    MoveSpeedPct = stage.enemyModifiers.moveSpeedPct,
                    SpawnRatePct = stage.enemyModifiers.spawnRatePct,
                    HealRatePct = stage.playerModifiers.healRatePct,
                    CooldownPct = stage.playerModifiers.cooldownPct,
                    HazardMultiplier = stage.runtimeEffects.hazardMultiplier,
                    OnKillExplosion = stage.runtimeEffects.onKillExplosion ? (byte)1 : (byte)0
                });
            }

            // If the player picked nothing, leave a single null entry so
            // DifficultySystem still sees a buffer (its update loop iterates
            // and just produces all-1x multipliers).
            if (buffer.Length == 0)
            {
                buffer.Add(default);
            }

            PushModifiersToHud();
        }

        private static void PushModifiersToHud()
        {
            NightDashHudResultUI hud =
                Object.FindFirstObjectByType<NightDashHudResultUI>(FindObjectsInactive.Include);
            if (hud == null) return;
            hud.SetActiveDifficultyModifiers(s_ModifierSoScratch, s_ModifierLevelScratch);
        }
    }
}
