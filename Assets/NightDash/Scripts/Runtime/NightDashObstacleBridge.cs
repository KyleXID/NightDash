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
        private const float CollisionHeightRatio = 0.3f;
        private const float RescanInterval = 2f;

        private struct ObstacleData
        {
            public Vector2 collisionCenter;
            public float collisionRadius;
            public SpriteRenderer renderer;
            public float bottomY;
        }

        private readonly List<ObstacleData> _obstacles = new();
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

            // Rescan periodically in case scene changed
            if (Time.time - _lastScanTime > RescanInterval)
            {
                ScanObstacles();
                _lastScanTime = Time.time;
            }

            if (_obstacles.Count == 0) return;

            UpdatePropSorting();
            PushPlayer();
            PushEnemies();
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return false;

            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());
            _enemyQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            _initialized = true;
            NightDashLog.Info("[ObstacleBridge] Initialized.");
            return true;
        }

        private void ScanObstacles()
        {
            _obstacles.Clear();
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

                // Use actual rendered bounds instead of raw localScale
                var bounds = sr.bounds;
                float renderW = bounds.size.x;
                float renderH = bounds.size.y;

                float spriteHalfH = renderH * 0.5f;
                float collisionCenterY = child.position.y - spriteHalfH + (renderH * CollisionHeightRatio * 0.5f);
                float collisionRadius = renderW * 0.35f;

                _obstacles.Add(new ObstacleData
                {
                    collisionCenter = new Vector2(child.position.x, collisionCenterY),
                    collisionRadius = collisionRadius,
                    renderer = sr,
                    bottomY = bounds.min.y
                });
            }

            if (_obstacles.Count > 0)
                NightDashLog.Info($"[ObstacleBridge] Scanned {_obstacles.Count} obstacles.");
        }

        private void UpdatePropSorting()
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.renderer != null)
                    obs.renderer.sortingOrder = 200 + (int)(-obs.bottomY * 10f);
            }
        }

        private void PushPlayer()
        {
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

            for (int o = 0; o < _obstacles.Count; o++)
            {
                var obs = _obstacles[o];
                float2 center = new float2(obs.collisionCenter.x, obs.collisionCenter.y);
                float2 diff = entityPos - center;
                float distSq = math.lengthsq(diff);
                float radius = obs.collisionRadius;
                float radiusSq = radius * radius;

                if (distSq < radiusSq && distSq > 0.0001f)
                {
                    float dist = math.sqrt(distSq);
                    float overlap = radius - dist;
                    // Hard push - immediately move out of collision
                    totalPush += (diff / dist) * math.max(overlap, 0.05f) * PushStrength * dt;
                }
                else if (distSq < 0.0001f)
                {
                    // Exactly on center - push in random direction
                    totalPush += new float2(0.1f, 0.1f) * PushStrength * dt;
                }
            }

            return totalPush;
        }
    }
}
