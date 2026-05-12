// Sprint B / M3 — Dash afterimage trail.
// Spawns short-lived ghost sprites at the player's position while the dash
// burst is active (CombatStats.DashTimer > 0). Each ghost copies the
// player's current sprite + flip state and fades out via a coroutine-free
// MonoBehaviour that scales its alpha to zero before self-destructing.
// Mage / Astrologer use a teleport dash — they still set DashTimer for a
// frame, so the trail bridge will drop a single afterimage at the start
// point on that frame and another at the new position the next frame.

using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;

namespace NightDash.Runtime
{
    public sealed class NightDashDashTrailBridge : MonoBehaviour
    {
        // Spawn cadence: a new afterimage every SpawnInterval seconds while
        // DashTimer > 0. Lower = denser trail. 0.04s gives ~4 ghosts per
        // 0.18s dash burst.
        private const float SpawnInterval = 0.04f;
        // Lifetime of each ghost. Fades alpha from StartAlpha to 0 over
        // this window then auto-destroys.
        private const float GhostLifetime = 0.32f;
        private const float StartAlpha = 0.55f;

        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private float _spawnAccumulator;
        private NightDashDebugVisualBridge _visualBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashDashTrailBridge>(FindObjectsInactive.Include);
            if (existing != null) return;
            var go = new GameObject("[NightDash] DashTrailBridge");
            go.AddComponent<NightDashDashTrailBridge>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            NightDashTeleportEvents.OnTeleport += HandleTeleport;
        }

        private void OnDisable()
        {
            NightDashTeleportEvents.OnTeleport -= HandleTeleport;
        }

        // Caster teleport: lay down a fan of 5 ghosts from the start position
        // toward the end position so the move reads as a magical streak
        // instead of an instant pop. Per-ghost alpha decays along the path
        // so the trail visually "points" from start to end.
        private void HandleTeleport(Vector3 start, Vector3 end)
        {
            const int GhostCount = 5;
            for (int i = 0; i < GhostCount; i++)
            {
                float t = i / (float)(GhostCount - 1);
                Vector3 pos = Vector3.Lerp(start, end, t);
                // Older ghost (closer to start) = more transparent so the
                // last ghost near the destination is the brightest cue.
                float alpha = Mathf.Lerp(StartAlpha * 0.4f, StartAlpha, t);
                SpawnGhost(pos, alpha);
            }
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized()) return;

            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;

            var em = _world.EntityManager;
            CombatStats stats = em.GetComponentData<CombatStats>(entities[0]);
            if (stats.DashTimer <= 0f)
            {
                _spawnAccumulator = SpawnInterval; // primed for next dash.
                return;
            }

            _spawnAccumulator += Time.deltaTime;
            if (_spawnAccumulator < SpawnInterval) return;
            _spawnAccumulator = 0f;

            LocalTransform t = em.GetComponentData<LocalTransform>(entities[0]);
            SpawnGhost(new Vector3(t.Position.x, t.Position.y, t.Position.z));
        }

        private void SpawnGhost(Vector3 worldPos) => SpawnGhost(worldPos, StartAlpha);

        private void SpawnGhost(Vector3 worldPos, float alpha)
        {
            if (_visualBridge == null)
            {
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
                if (_visualBridge == null) return;
            }

            SpriteRenderer src = _visualBridge.GetAnyPlayerRenderer();
            if (src == null || src.sprite == null) return;

            var go = new GameObject("[DashTrail] Ghost");
            go.transform.position = worldPos;
            go.transform.localScale = src.transform.lossyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.flipX = src.flipX;
            sr.flipY = src.flipY;
            // Cool ghost tint so the afterimage reads as a magical streak,
            // not just a duplicate sprite. Tuned light blue-violet.
            sr.color = new Color(0.55f, 0.72f, 1f, alpha);
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1; // sit behind the live player

            go.AddComponent<DashTrailGhost>().Init(GhostLifetime, alpha);
        }

        private bool EnsureInitialized()
        {
            if (_initialized && _world != null && _world.IsCreated) return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return false;

            _playerQuery = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<CombatStats>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());
            _initialized = true;
            return true;
        }
    }

    // Tiny self-destroying fade-out controller. Lives on each spawned ghost
    // and drives the SpriteRenderer's alpha from StartAlpha → 0 over
    // Lifetime seconds, then Destroy()s itself.
    public sealed class DashTrailGhost : MonoBehaviour
    {
        private float _lifetime;
        private float _startAlpha;
        private float _age;
        private SpriteRenderer _sr;

        public void Init(float lifetime, float startAlpha)
        {
            _lifetime = lifetime;
            _startAlpha = startAlpha;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _lifetime);
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = Mathf.Lerp(_startAlpha, 0f, t);
                _sr.color = c;
            }
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
