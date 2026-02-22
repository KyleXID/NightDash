using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashDebugVisualBridge : MonoBehaviour
    {
        private readonly Dictionary<Entity, Transform> _playerViews = new();
        private readonly Dictionary<Entity, Transform> _enemyViews = new();
        private readonly Dictionary<Entity, Transform> _bossViews = new();

        private EntityManager _entityManager;
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private EntityQuery _bossQuery;
        private bool _initialized;
        private Material _fallbackMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashDebugVisualBridge>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("NightDashDebugVisualBridge");
            go.AddComponent<NightDashDebugVisualBridge>();
            DontDestroyOnLoad(go);
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            SyncGroup(_playerQuery, _playerViews, "Player", new Color(0.2f, 0.95f, 0.5f), 0.5f);
            SyncGroup(_enemyQuery, _enemyViews, "Enemy", new Color(1f, 0.25f, 0.25f), 0.38f);
            SyncGroup(_bossQuery, _bossViews, "Boss", new Color(1f, 0.75f, 0.1f), 0.8f);
        }

        private void OnDisable()
        {
            CleanupViews(_playerViews);
            CleanupViews(_enemyViews);
            CleanupViews(_bossViews);
        }

        private void SyncGroup(EntityQuery query, Dictionary<Entity, Transform> views, string prefix, Color color, float scale)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            var alive = new HashSet<Entity>(entities.Length);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity e = entities[i];
                alive.Add(e);

                if (!views.TryGetValue(e, out Transform view))
                {
                    view = CreateView(prefix, color, scale);
                    views[e] = view;
                }

                LocalTransform transform = _entityManager.GetComponentData<LocalTransform>(e);
                view.position = new Vector3(transform.Position.x, transform.Position.y, -0.1f);
            }

            var remove = new List<Entity>();
            foreach (var kv in views)
            {
                if (!alive.Contains(kv.Key))
                {
                    if (kv.Value != null)
                    {
                        Destroy(kv.Value.gameObject);
                    }

                    remove.Add(kv.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                views.Remove(remove[i]);
            }
        }

        private Transform CreateView(string prefix, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Debug_{prefix}";
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // Keep debug visuals on top of gameplay plane.
            go.transform.position = new Vector3(0f, 0f, -0.1f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                Material mat = shader != null ? new Material(shader) : _fallbackMaterial;
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }
                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", color);
                    }
                    renderer.sharedMaterial = mat;
                }
            }

            // Remove collider noise.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            return go.transform;
        }

        private static void CleanupViews(Dictionary<Entity, Transform> views)
        {
            foreach (var kv in views)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value.gameObject);
                }
            }

            views.Clear();
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return true;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            _entityManager = world.EntityManager;
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _enemyQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>(),
                ComponentType.Exclude<BossTag>());
            _bossQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<BossTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            Shader fallbackShader = Shader.Find("Unlit/Color");
            if (fallbackShader != null)
            {
                _fallbackMaterial = new Material(fallbackShader);
            }

            _initialized = true;
            NightDashLog.Info("[NightDash] DebugVisualBridge initialized.");
            return true;
        }
    }
}
