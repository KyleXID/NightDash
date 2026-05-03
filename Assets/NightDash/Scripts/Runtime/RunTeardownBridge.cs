// Sprint B / M3 — Run teardown bridge.
// Cleans up the active gameplay run so the lobby can be re-entered without
// leaving zombie enemies, projectiles, or stale GameLoopState behind.
// Called by NightDashPauseMenuUI when "Return to Lobby" is selected.
//
// Persistent singletons (GameLoopState, RunSelection, DataLoadState,
// MetaProgress, SaveState, BossSpawnState, DifficultyState) are NOT
// destroyed — they're reset in place. Run-spawned entities (Enemy, Boss,
// Player, Projectile) are destroyed wholesale.

using Unity.Entities;
using UnityEngine;
using NightDash.ECS.Components;

namespace NightDash.Runtime
{
    public static class RunTeardownBridge
    {
        public static void DestroyCurrentRun()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                NightDashLog.Warn("[NightDash] RunTeardownBridge: no default world; teardown skipped.");
                return;
            }
            EntityManager em = world.EntityManager;

            // 1) Sweep run-spawned entities. Persistent singletons don't
            //    carry these tags so they're untouched.
            DestroyEntitiesWith<EnemyTag>(em);
            DestroyEntitiesWith<BossTag>(em);
            DestroyEntitiesWith<PlayerTag>(em);
            DestroyEntitiesWith<ProjectileData>(em);

            // 2) Reset gameplay state singleton (zero IsRunActive / Elapsed /
            //    Level / Experience) via the existing lobby bridge helper.
            RunSelectionLobbyWorldBridge.SetRunActiveInCurrentWorld(false);

            // 3) Clear any pending navigation request so the next StartRun
            //    isn't shadowed by stale routing state.
            ClearRunNavigationRequest(em);

            // 4) Drop the pause tag defensively. The Pause Menu's OnDisable
            //    also removes it, but ordering between OnDisable and this
            //    teardown isn't guaranteed if callers reorder; this prevents
            //    a lingering pause tag from freezing the next run.
            DestroyEntitiesWith<GameplayPauseTag>(em);

            // 5) Drop pooled view objects (SpriteRenderers + sub-instances)
            //    so the lobby returns to a clean visual state. Lobby's own
            //    HideGameplayViews provides a second safety net.
            var bridge = Object.FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (bridge != null)
            {
                bridge.DestroyAllViewsImmediate();
            }
        }

        private static void DestroyEntitiesWith<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!q.IsEmptyIgnoreFilter)
            {
                em.DestroyEntity(q);
            }
        }

        private static void ClearRunNavigationRequest(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<RunNavigationRequest>());
            if (q.IsEmptyIgnoreFilter) return;
            Entity e = q.GetSingletonEntity();
            var nav = em.GetComponentData<RunNavigationRequest>(e);
            nav.Action = RunNavigationAction.None;
            nav.IsPending = 0;
            em.SetComponentData(e, nav);
        }
    }
}
