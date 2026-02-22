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
                loop.ValueRW.IsRunActive = 1;
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
            }

            Debug.Log($"[NightDash] RunSelection override applied: stage='{stageId}', class='{classId}'.");
        }
    }
}
