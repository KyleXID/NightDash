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
        private const float PushStrength = 15f;
        private const float CollisionHeightRatio = 0.3f; // collision at bottom 30% of sprite

        private struct ObstacleData
        {
            public Vector2 collisionCenter; // bottom part of the sprite
            public float collisionRadius;   // based on sprite scale
            public SpriteRenderer renderer; // for Y-sort update
            public float bottomY;           // bottom edge Y for sorting
        }

        private readonly List<ObstacleData> _obstacles = new();
        private World _world;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private bool _scanned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] ObstacleBridge");
            go.AddComponent<NightDashObstacleBridge>();
            DontDestroyOnLoad(go);
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsCreated)
            {
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world == null || !_world.IsCreated) return;

                var em = _world.EntityManager;
                _playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadWrite<LocalTransform>(),
                    ComponentType.Exclude<Prefab>());
                _enemyQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<EnemyTag>(),
                    ComponentType.ReadWrite<LocalTransform>(),
                    ComponentType.Exclude<Prefab>());
            }

            if (!_scanned)
            {
                ScanObstacles();
                _scanned = true;
            }

            if (_obstacles.Count == 0) return;

            // Update sorting order for all env props every frame (Y-sort)
            UpdatePropSorting();

            PushEntitiesFromObstacles(_playerQuery);
            PushEntitiesFromObstacles(_enemyQuery);
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
                float scaleY = child.localScale.y;
                float scaleX = child.localScale.x;

                // Collision center is at bottom portion of the sprite
                float spriteHalfH = scaleY * 0.5f;
                float collisionCenterY = child.position.y - spriteHalfH + (scaleY * CollisionHeightRatio * 0.5f);
                float collisionRadius = scaleX * 0.35f; // 35% of width as collision radius

                _obstacles.Add(new ObstacleData
                {
                    collisionCenter = new Vector2(child.position.x, collisionCenterY),
                    collisionRadius = collisionRadius,
                    renderer = sr,
                    bottomY = child.position.y - spriteHalfH
                });
            }

            NightDashLog.Info($"[ObstacleBridge] Scanned {_obstacles.Count} obstacles with bottom-based collision.");
        }

        private void UpdatePropSorting()
        {
            // Y-sort all env props based on their bottom edge, matching entity Y-sort
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (obs.renderer != null)
                {
                    obs.renderer.sortingOrder = 200 + (int)(-obs.bottomY * 10f);
                }
            }
        }

        private void PushEntitiesFromObstacles(EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter) return;

            var em = _world.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            float dt = Time.deltaTime;

            for (int i = 0; i < entities.Length; i++)
            {
                var pos = new float2(transforms[i].Position.x, transforms[i].Position.y);
                float2 totalPush = float2.zero;

                for (int o = 0; o < _obstacles.Count; o++)
                {
                    var obs = _obstacles[o];
                    float2 center = new float2(obs.collisionCenter.x, obs.collisionCenter.y);
                    float2 diff = pos - center;
                    float distSq = math.lengthsq(diff);
                    float radiusSq = obs.collisionRadius * obs.collisionRadius;

                    if (distSq < radiusSq && distSq > 0.0001f)
                    {
                        float dist = math.sqrt(distSq);
                        float overlap = obs.collisionRadius - dist;
                        totalPush += (diff / dist) * overlap * PushStrength * dt;
                    }
                }

                if (math.lengthsq(totalPush) > 0.0001f)
                {
                    var t = transforms[i];
                    t.Position += new float3(totalPush.x, totalPush.y, 0f);
                    em.SetComponentData(entities[i], t);
                }
            }
        }
    }
}
