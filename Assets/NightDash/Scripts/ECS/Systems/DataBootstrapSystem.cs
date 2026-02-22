using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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

            if (!registry.TryGetClass(classId, out ClassData classData))
            {
                NightDashLog.Warn($"[NightDash] ClassData not found for '{classId}', using baked defaults.");
            }
            else
            {
                bool hasWeapon = registry.TryGetWeapon(classData.startWeaponId, out WeaponData weaponData);
                float weaponDamage = hasWeapon ? classData.basePower * weaponData.basePowerCoeff : classData.basePower;
                float weaponCooldown = hasWeapon ? weaponData.baseCooldown : 1f;
                float weaponRange = hasWeapon ? weaponData.baseRange : 3f;

                foreach (var (stats, weapon) in SystemAPI.Query<RefRW<CombatStats>, RefRW<WeaponRuntimeData>>().WithAll<PlayerTag>())
                {
                    stats.ValueRW.MaxHealth = classData.baseHp;
                    stats.ValueRW.CurrentHealth = classData.baseHp;
                    stats.ValueRW.Damage = classData.basePower;
                    stats.ValueRW.MoveSpeed = classData.baseMoveSpeed;

                    weapon.ValueRW.Damage = weaponDamage;
                    weapon.ValueRW.Cooldown = weaponCooldown;
                    weapon.ValueRW.Range = weaponRange;
                    weapon.ValueRW.CooldownRemaining = 0f;
                }
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
            EnemySpawnConfig spawn = SystemAPI.GetSingleton<EnemySpawnConfig>();

            float baseSpawnPerMinute = 60f / math.max(0.1f, spawn.SpawnInterval);
            timeline.Clear();

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
            }

            NightDashLog.Info($"[NightDash] Stage spawn phases applied: stage='{stageData.id}', phases={timeline.Length}.");
        }
    }
}
