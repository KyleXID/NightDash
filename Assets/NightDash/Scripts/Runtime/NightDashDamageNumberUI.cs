// ============================================================================
// NightDashDamageNumberUI.cs
// Spawns floating damage numbers in world space when enemies take damage.
// Uses TextMesh (no external font dependencies).
// ============================================================================

using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashDamageNumberUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning
        private const float FloatDistance = 0.5f;   // units upward
        private const float Duration     = 0.8f;    // seconds
        private const float FontSize     = 32f;
        private const int   SortOrder    = 200;

        // ------------------------------------------------------------------ state
        private World _world;
        private EntityQuery _enemyQuery;

        /// <summary>Entity → (previousHealth, lastWorldPosition)</summary>
        private readonly Dictionary<Entity, (float health, float3 pos)> _tracked = new();

        // S4-05: per-frame allocation 제거용 캐시.
        private readonly HashSet<Entity> _aliveBuffer = new();
        private readonly List<Entity> _deadBuffer = new();

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] DamageNumberUI");
            go.AddComponent<NightDashDamageNumberUI>();
            DontDestroyOnLoad(go);
            NightDashLog.Info("[DamageNumberUI] Auto-created.");
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================
        private void LateUpdate()
        {
            if (!TryGetWorld()) return;

            using var entities   = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var stats      = _enemyQuery.ToComponentDataArray<CombatStats>(Allocator.Temp);

            _aliveBuffer.Clear();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var pos    = transforms[i].Position;
                var hp     = stats[i].CurrentHealth;

                _aliveBuffer.Add(entity);

                if (_tracked.TryGetValue(entity, out var prev))
                {
                    float delta = prev.health - hp;
                    if (delta > 0.01f)
                    {
                        SpawnDamageNumber(pos, delta);
                    }
                    _tracked[entity] = (hp, pos);
                }
                else
                {
                    _tracked[entity] = (hp, pos);
                }
            }

            // Clean up entries for dead entities
            _deadBuffer.Clear();
            foreach (var kvp in _tracked)
            {
                if (!_aliveBuffer.Contains(kvp.Key))
                    _deadBuffer.Add(kvp.Key);
            }
            foreach (var e in _deadBuffer)
                _tracked.Remove(e);
        }

        private void OnDestroy()
        {
            _tracked.Clear();
        }

        // ====================================================================
        // World init
        // ====================================================================
        private bool TryGetWorld()
        {
            if (_world != null && _world.IsCreated) return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return false;

            var em = _world.EntityManager;
            _enemyQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<CombatStats>(),
                ComponentType.Exclude<Prefab>());

            NightDashLog.Info("[DamageNumberUI] Queries initialised.");
            return true;
        }

        // ====================================================================
        // Damage number spawner
        // ====================================================================
        private static void SpawnDamageNumber(float3 worldPos, float damage)
        {
            var go = new GameObject("[UI] DmgNum");
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.2f, 0f);

            // TextMesh for world-space rendering (no canvas / external font needed)
            var tm = go.AddComponent<TextMesh>();
            tm.text          = Mathf.RoundToInt(damage).ToString();
            tm.fontSize      = 128;
            tm.characterSize = 0.15f;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.fontStyle     = FontStyle.Bold;
            tm.color         = Color.white;

            // Renderer setup
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = SortOrder;
            }

            // Scale for readability
            go.transform.localScale = Vector3.one * 0.12f;

            // Outline effect via a slightly offset darker copy behind
            var outline = new GameObject("[UI] DmgNumOutline");
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0.3f, -0.3f, 0.01f);
            outline.transform.localScale    = Vector3.one;

            var tmOutline = outline.AddComponent<TextMesh>();
            tmOutline.text          = tm.text;
            tmOutline.fontSize      = tm.fontSize;
            tmOutline.characterSize = tm.characterSize;
            tmOutline.anchor        = TextAnchor.MiddleCenter;
            tmOutline.alignment     = TextAlignment.Center;
            tmOutline.fontStyle     = FontStyle.Bold;
            tmOutline.color         = Color.black;

            var mrOutline = outline.GetComponent<MeshRenderer>();
            if (mrOutline != null)
            {
                mrOutline.sortingOrder = SortOrder - 1;
            }

            // Attach the float-and-fade behaviour
            go.AddComponent<DamageNumberAnimator>().Init(Duration, FloatDistance);
        }
    }

    // ========================================================================
    // Animator component: floats up, fades out, self-destructs.
    // ========================================================================
    public sealed class DamageNumberAnimator : MonoBehaviour
    {
        private float _duration;
        private float _floatDist;
        private float _elapsed;
        private Vector3 _startPos;
        private TextMesh _tm;
        private TextMesh _tmOutline;

        public void Init(float duration, float floatDistance)
        {
            _duration  = duration;
            _floatDist = floatDistance;
            _startPos  = transform.position;
            _tm        = GetComponent<TextMesh>();

            // Find outline child
            if (transform.childCount > 0)
                _tmOutline = transform.GetChild(0).GetComponent<TextMesh>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Float upward
            transform.position = _startPos + new Vector3(0f, _floatDist * t, 0f);

            // Fade out
            float alpha = 1f - t;
            if (_tm != null)
            {
                var c = _tm.color;
                c.a = alpha;
                _tm.color = c;
            }
            if (_tmOutline != null)
            {
                var c = _tmOutline.color;
                c.a = alpha;
                _tmOutline.color = c;
            }

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
