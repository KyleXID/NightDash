using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Transforms;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(NightDashRuntimeBootstrapSystem))]
    public partial struct NightDashPlayableFallbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemySpawnConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!NightDashRuntimeToggles.EnableFallbackBootstrapWhenBakingMissing)
            {
                state.Enabled = false;
                return;
            }

            bool changed = false;

            EntityQuery playerQuery = SystemAPI.QueryBuilder().WithAll<PlayerTag, CombatStats>().Build();
            if (playerQuery.IsEmptyIgnoreFilter)
            {
                CreateFallbackPlayer(ref state);
                changed = true;
            }

            RefRW<EnemySpawnConfig> spawn = SystemAPI.GetSingletonRW<EnemySpawnConfig>();
            if (spawn.ValueRO.EnemyPrefab == Entity.Null)
            {
                spawn.ValueRW.EnemyPrefab = CreateFallbackEnemyPrefab(ref state, false);
                changed = true;
            }

            if (spawn.ValueRO.BossPrefab == Entity.Null)
            {
                spawn.ValueRW.BossPrefab = CreateFallbackEnemyPrefab(ref state, true);
                changed = true;
            }

            if (changed)
            {
                NightDashLog.Warn("[NightDash] Fallback playable entities created for runtime preview (player/enemy prefabs).");
            }

            state.Enabled = false;
        }

        private static void CreateFallbackPlayer(ref SystemState state)
        {
            Entity player = state.EntityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(CombatStats),
                typeof(WeaponRuntimeData));

            state.EntityManager.SetComponentData(player, LocalTransform.FromPosition(new Unity.Mathematics.float3(0f, 0f, 0f)));
            state.EntityManager.SetComponentData(player, new CombatStats
            {
                CurrentHealth = 100f,
                MaxHealth = 100f,
                Damage = 12f,
                MoveSpeed = 5f
            });
            state.EntityManager.SetComponentData(player, new WeaponRuntimeData
            {
                Cooldown = 0.8f,
                CooldownRemaining = 0f,
                Damage = 12f,
                Range = 7f
            });
        }

        private static Entity CreateFallbackEnemyPrefab(ref SystemState state, bool boss)
        {
            Entity enemy = state.EntityManager.CreateEntity(
                typeof(Prefab),
                typeof(EnemyTag),
                typeof(LocalTransform),
                typeof(CombatStats));

            if (boss)
            {
                state.EntityManager.AddComponent<BossTag>(enemy);
            }

            state.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new Unity.Mathematics.float3(0f, 0f, 0f)));
            state.EntityManager.SetComponentData(enemy, new CombatStats
            {
                CurrentHealth = boss ? 300f : 30f,
                MaxHealth = boss ? 300f : 30f,
                Damage = boss ? 15f : 5f,
                MoveSpeed = boss ? 1.8f : 2.5f
            });

            return enemy;
        }
    }
}
