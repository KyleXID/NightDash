using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
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
                Debug.LogWarning($"[NightDash] StageData not found for '{stageId}', using baked defaults.");
            }
            else
            {
                RefRW<StageRuntimeConfig> stageRuntime = SystemAPI.GetSingletonRW<StageRuntimeConfig>();
                stageRuntime.ValueRW.StageDuration = stageData.durationSec;
                stageRuntime.ValueRW.BossSpawnTime = stageData.bossSpawnSec;

                RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
                meta.ValueRW.LastRunReward = 0;
            }

            if (!registry.TryGetClass(classId, out ClassData classData))
            {
                Debug.LogWarning($"[NightDash] ClassData not found for '{classId}', using baked defaults.");
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
            Debug.Log($"[NightDash] DataBootstrap loaded stage='{stageId}', class='{classId}'.");
        }
    }
}
