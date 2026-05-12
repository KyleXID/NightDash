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
using Unity.Mathematics;
using Unity.Transforms;
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

            // 1) BEFORE destroying enemies, reset VFXBridge tracking so its
            //    next LateUpdate doesn't fire OnEnemyDeath for every entity
            //    we're about to remove. Without this, XPDropBridge would
            //    spawn a fresh gem per zombie enemy → those pickups inherit
            //    into the next run.
            var vfxBridge = Object.FindFirstObjectByType<NightDashVFXBridge>();
            if (vfxBridge != null)
            {
                vfxBridge.ResetTrackingForRunTeardown();
            }

            // 2) Sweep run-spawned entities. Persistent singletons don't
            //    carry these tags so they're untouched.
            //
            // PlayerTag is intentionally preserved — NightDashPlayableFallbackSystem
            // is a one-shot bootstrap (InitializationSystemGroup with Enabled=false
            // self-disable) so destroying the Player entity here would leave the
            // next run with no Player and no respawn path. We reset its
            // CombatStats and LocalTransform instead.
            DestroyEntitiesWith<EnemyTag>(em);
            DestroyEntitiesWith<BossTag>(em);
            DestroyEntitiesWith<ProjectileData>(em);
            ResetPlayerForNextRun(em);

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

            // 6) Drop GameObject-based pickups (XP gems, health orbs) — these
            //    are spawned by NightDashXPDropBridge as plain GameObjects, NOT
            //    ECS entities, so the entity sweep above doesn't catch them.
            //    Without this, abandoned pickups from the previous run would
            //    fly to the new player on the very first frame.
            var xpBridge = Object.FindFirstObjectByType<NightDashXPDropBridge>();
            if (xpBridge != null)
            {
                xpBridge.ClearAllPickups();
            }

            // 7) Snap the camera to the (now-reset) player position and zero
            //    its smoothing velocity. Otherwise the next run starts with a
            //    visible 0.08s slide from the previous camera position back
            //    to origin.
            var camera = Object.FindFirstObjectByType<NightDashCameraFollow>();
            if (camera != null)
            {
                camera.SnapToPlayer();
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

        // Restores HP to MaxHealth and snaps position to origin so the next
        // run starts from a clean state without rebuilding the Player entity.
        private static void ResetPlayerForNextRun(EntityManager em)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerTag>(),
                ComponentType.ReadWrite<CombatStats>(),
                ComponentType.ReadWrite<LocalTransform>());
            if (q.IsEmptyIgnoreFilter) return;

            Entity player = q.GetSingletonEntity();
            CombatStats stats = em.GetComponentData<CombatStats>(player);
            stats.CurrentHealth = stats.MaxHealth > 0f ? stats.MaxHealth : 100f;
            stats.CurrentShield = stats.MaxShield;
            stats.TimeSinceLastHit = 0f;
            stats.DashTimer = 0f;
            stats.DashCooldownRemaining = 0f;
            stats.PotionCount = stats.MaxPotionCount > 0 ? stats.MaxPotionCount : 3;
            em.SetComponentData(player, stats);
            em.SetComponentData(player, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
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
