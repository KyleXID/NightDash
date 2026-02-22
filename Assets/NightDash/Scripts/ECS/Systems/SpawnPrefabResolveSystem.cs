using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(DataBootstrapSystem))]
    public partial struct SpawnPrefabResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemySpawnConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<EnemySpawnConfig> spawnConfig = SystemAPI.GetSingletonRW<EnemySpawnConfig>();
            bool needsEnemy = spawnConfig.ValueRO.EnemyPrefab == Entity.Null;
            bool needsBoss = spawnConfig.ValueRO.BossPrefab == Entity.Null;
            if (!needsEnemy && !needsBoss)
            {
                state.Enabled = false;
                return;
            }

            if (needsEnemy)
            {
                EntityQuery enemyPrefabQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, Prefab>().WithNone<BossTag>().Build();
                if (!enemyPrefabQuery.IsEmptyIgnoreFilter)
                {
                    using var entities = enemyPrefabQuery.ToEntityArray(Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        spawnConfig.ValueRW.EnemyPrefab = entities[0];
                        NightDashLog.Info("[NightDash] Enemy spawn prefab resolved from baked prefab entity.");
                        needsEnemy = false;
                    }
                }
                else
                {
                    EntityQuery enemyAnyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag>().WithNone<BossTag>().Build();
                    if (!enemyAnyQuery.IsEmptyIgnoreFilter)
                    {
                        using var entities = enemyAnyQuery.ToEntityArray(Allocator.Temp);
                        if (entities.Length > 0)
                        {
                            spawnConfig.ValueRW.EnemyPrefab = entities[0];
                            NightDashLog.Warn("[NightDash] Enemy spawn source resolved from non-prefab enemy entity fallback.");
                            needsEnemy = false;
                        }
                    }
                }
            }

            if (needsBoss)
            {
                EntityQuery bossPrefabQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, BossTag, Prefab>().Build();
                if (!bossPrefabQuery.IsEmptyIgnoreFilter)
                {
                    using var entities = bossPrefabQuery.ToEntityArray(Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        spawnConfig.ValueRW.BossPrefab = entities[0];
                        NightDashLog.Info("[NightDash] Boss spawn prefab resolved from baked prefab entity.");
                        needsBoss = false;
                    }
                }
                else
                {
                    EntityQuery bossAnyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, BossTag>().Build();
                    if (!bossAnyQuery.IsEmptyIgnoreFilter)
                    {
                        using var entities = bossAnyQuery.ToEntityArray(Allocator.Temp);
                        if (entities.Length > 0)
                        {
                            spawnConfig.ValueRW.BossPrefab = entities[0];
                            NightDashLog.Warn("[NightDash] Boss spawn source resolved from non-prefab boss entity fallback.");
                            needsBoss = false;
                        }
                    }
                }
            }

            if (!needsEnemy && !needsBoss)
            {
                state.Enabled = false;
            }
        }
    }
}
