using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashObstacleBridge : MonoBehaviour
    {
        private const float PushStrength = 20f;
        private const float RescanInterval = 2f;

        // Single foot-AABB per prop:
        // sample alpha in the bottom FootRatio of the sprite, take the horizontal span of
        // visible pixels (alpha >= AlphaThreshold), and build one rectangle from that.
        // - Avoids tunneling between sub-cells (no gaps)
        // - Provides perspective: only the prop's foot blocks; tops sort behind via
        //   the existing y-based sortingOrder logic so characters appear to walk past.
        private const float FootRatio = 0.25f;
        private const float AlphaThreshold = 0.4f;

        private struct ObstacleCell
        {
            public Vector2 center;
            public Vector2 halfSize;
        }

        private struct ObstacleProp
        {
            public SpriteRenderer renderer;
            public float bottomY;
        }

        // FootBounds = normalized rect (0..1) of the prop's foot collider in sprite-local space.
        private struct FootBounds
        {
            public bool hasContent;
            public float minXNorm;
            public float maxXNorm;
            public float minYNorm;
            public float maxYNorm;
        }

        private readonly List<ObstacleCell> _cells = new();
        private readonly List<ObstacleProp> _props = new();
        private static readonly Dictionary<Sprite, FootBounds> _footBoundsCache = new();

        private World _world;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private float _lastScanTime = -999f;
        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var existing = FindFirstObjectByType<NightDashObstacleBridge>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("[NightDash] ObstacleBridge");
            go.AddComponent<NightDashObstacleBridge>();
            DontDestroyOnLoad(go);
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized()) return;

            if (Time.time - _lastScanTime > RescanInterval)
            {
                ScanObstacles();
                _lastScanTime = Time.time;
            }

            if (_cells.Count == 0) return;

            UpdatePropSorting();
            PushPlayer();
            PushEnemies();
        }

        private bool EnsureInitialized()
        {
            // If the cached world was disposed (PlayMode reload, World swap),
            // drop _initialized so the queries get rebuilt against the new
            // EntityManager. Otherwise PushPlayer/PushEnemies will NRE on
            // stale EntityQuery internals.
            if (_initialized && (_world == null || !_world.IsCreated))
            {
                _initialized = false;
            }
            if (_initialized) return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return false;

            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.Exclude<Prefab>());
            _enemyQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            _initialized = true;
            NightDashLog.Info("[ObstacleBridge] Initialized.");
            return true;
        }

        private void ScanObstacles()
        {
            _cells.Clear();
            _props.Clear();

            var root = GameObject.Find("Stage01Environment");
            if (root == null) return;

            var propsRoot = root.transform.Find("Props");
            if (propsRoot == null) return;

            for (int i = 0; i < propsRoot.childCount; i++)
            {
                var child = propsRoot.GetChild(i);
                if (!child.name.StartsWith("Prop_")) continue;

                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;

                var bounds = sr.bounds;
                var foot = GetOrComputeFootBounds(sr.sprite);
                if (!foot.hasContent) continue;

                // Map normalized foot rect (sprite-local) into world-space AABB.
                Vector2 worldMin = new Vector2(
                    bounds.min.x + foot.minXNorm * bounds.size.x,
                    bounds.min.y + foot.minYNorm * bounds.size.y);
                Vector2 worldMax = new Vector2(
                    bounds.min.x + foot.maxXNorm * bounds.size.x,
                    bounds.min.y + foot.maxYNorm * bounds.size.y);

                Vector2 center = (worldMin + worldMax) * 0.5f;
                Vector2 halfSize = (worldMax - worldMin) * 0.5f;

                _cells.Add(new ObstacleCell { center = center, halfSize = halfSize });
                _props.Add(new ObstacleProp { renderer = sr, bottomY = bounds.min.y });
            }

            if (_cells.Count > 0)
                NightDashLog.Info($"[ObstacleBridge] Scanned {_props.Count} props ({_cells.Count} foot AABBs).");
        }

        // Computes the foot AABB (in normalized 0..1 sprite coords) by scanning the
        // bottom FootRatio of the sprite and finding the horizontal span of opaque pixels.
        private static FootBounds GetOrComputeFootBounds(Sprite sprite)
        {
            if (_footBoundsCache.TryGetValue(sprite, out var cached)) return cached;

            FootBounds result = default;
            var tex = sprite.texture;

            // Fallback: if texture isn't readable, use full width and the bottom FootRatio band.
            if (tex == null || !tex.isReadable)
            {
                result.hasContent = true;
                result.minXNorm = 0f;
                result.maxXNorm = 1f;
                result.minYNorm = 0f;
                result.maxYNorm = FootRatio;
                _footBoundsCache[sprite] = result;
                return result;
            }

            int rectX = (int)sprite.rect.x;
            int rectY = (int)sprite.rect.y;
            int rectW = (int)sprite.rect.width;
            int rectH = (int)sprite.rect.height;
            int footRows = math.max(1, (int)(rectH * FootRatio));

            Color[] pixels;
            try
            {
                pixels = tex.GetPixels(rectX, rectY, rectW, footRows);
            }
            catch
            {
                result.hasContent = true;
                result.minXNorm = 0f;
                result.maxXNorm = 1f;
                result.minYNorm = 0f;
                result.maxYNorm = FootRatio;
                _footBoundsCache[sprite] = result;
                return result;
            }

            int minPx = int.MaxValue;
            int maxPx = -1;
            int minPy = int.MaxValue;
            int maxPy = -1;

            for (int py = 0; py < footRows; py++)
            {
                int rowOffset = py * rectW;
                for (int px = 0; px < rectW; px++)
                {
                    if (pixels[rowOffset + px].a < AlphaThreshold) continue;
                    if (px < minPx) minPx = px;
                    if (px > maxPx) maxPx = px;
                    if (py < minPy) minPy = py;
                    if (py > maxPy) maxPy = py;
                }
            }

            if (maxPx < 0)
            {
                result.hasContent = false;
                _footBoundsCache[sprite] = result;
                return result;
            }

            // Use inclusive pixel span -> normalized rect within the full sprite height.
            result.hasContent = true;
            result.minXNorm = minPx / (float)rectW;
            result.maxXNorm = (maxPx + 1) / (float)rectW;
            result.minYNorm = minPy / (float)rectH;
            result.maxYNorm = (maxPy + 1) / (float)rectH;
            _footBoundsCache[sprite] = result;
            return result;
        }

        private void UpdatePropSorting()
        {
            for (int i = 0; i < _props.Count; i++)
            {
                var p = _props[i];
                if (p.renderer != null)
                    p.renderer.sortingOrder = 200 + (int)(-p.bottomY * 10f);
            }
        }

        private void PushPlayer()
        {
            // EntityQuery becomes invalid if the world it was created against
            // is disposed (e.g. PlayMode reload). Touching IsEmptyIgnoreFilter
            // in that state throws NRE — re-init guards against that.
            if (_world == null || !_world.IsCreated)
            {
                _initialized = false;
                return;
            }
            if (_playerQuery.IsEmptyIgnoreFilter) return;

            var em = _world.EntityManager;
            using var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0) return;

            var entity = entities[0];
            var t = em.GetComponentData<LocalTransform>(entity);
            var pos = new float2(t.Position.x, t.Position.y);

            float2 push = ComputePush(pos);
            if (math.lengthsq(push) > 0.0001f)
            {
                t.Position += new float3(push.x, push.y, 0f);
                em.SetComponentData(entity, t);
            }
        }

        private void PushEnemies()
        {
            if (_world == null || !_world.IsCreated)
            {
                _initialized = false;
                return;
            }
            if (_enemyQuery.IsEmptyIgnoreFilter) return;

            var em = _world.EntityManager;
            using var entities = _enemyQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var t = em.GetComponentData<LocalTransform>(entities[i]);
                var pos = new float2(t.Position.x, t.Position.y);

                float2 push = ComputePush(pos);
                if (math.lengthsq(push) > 0.0001f)
                {
                    t.Position += new float3(push.x, push.y, 0f);
                    em.SetComponentData(entities[i], t);
                }
            }
        }

        private float2 ComputePush(float2 entityPos)
        {
            float dt = Time.deltaTime;
            float2 totalPush = float2.zero;

            for (int o = 0; o < _cells.Count; o++)
            {
                var cell = _cells[o];
                float2 center = new float2(cell.center.x, cell.center.y);
                float2 halfSize = new float2(cell.halfSize.x, cell.halfSize.y);
                float2 diff = entityPos - center;
                float2 absDiff = math.abs(diff);

                if (absDiff.x >= halfSize.x || absDiff.y >= halfSize.y) continue;

                float2 penetration = halfSize - absDiff;
                if (penetration.x < penetration.y)
                {
                    float sign = diff.x >= 0f ? 1f : -1f;
                    totalPush += new float2(sign * math.max(penetration.x, 0.05f), 0f) * PushStrength * dt;
                }
                else
                {
                    float sign = diff.y >= 0f ? 1f : -1f;
                    totalPush += new float2(0f, sign * math.max(penetration.y, 0.05f)) * PushStrength * dt;
                }
            }

            return totalPush;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_cells == null) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                var center = new Vector3(c.center.x, c.center.y, 0f);
                var size = new Vector3(c.halfSize.x * 2f, c.halfSize.y * 2f, 0f);
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif
    }
}
