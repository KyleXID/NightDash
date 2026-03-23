// ============================================================================
// NightDashVFXBridge.cs
// Spawns hit-flash and death-explosion VFX when enemy health changes or
// enemies are destroyed. Also notifies XPDropBridge of death positions.
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
    public sealed class NightDashVFXBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------ VFX tuning
        private const float HitDuration   = 0.15f;
        private const float HitScale      = 0.30f;
        private const int   HitSortOrder  = 110;

        private const float DeathDuration    = 0.40f;
        private const float DeathScaleStart  = 0.30f;
        private const float DeathScaleEnd    = 0.60f;
        private const int   DeathSortOrder   = 105;

        // ------------------------------------------------------------------ sprite paths
        private const string PathHit       = "NightDash/Art/Stage01/VFX/spr_vfx_enemy_hit";
        private const string PathDeath     = "NightDash/Art/Stage01/VFX/spr_vfx_enemy_death";
        private const string PathBossAtk   = "NightDash/Art/Stage01/VFX/spr_vfx_boss_attack";

        // ------------------------------------------------------------------ state
        private World _world;
        private EntityQuery _enemyQuery;

        /// <summary>Entity → (previousHealth, lastWorldPosition)</summary>
        private readonly Dictionary<Entity, (float health, float3 pos)> _tracked = new();

        private Sprite _hitSprite;
        private Sprite _deathSprite;

        // XP drop callback - set by NightDashXPDropBridge
        public static System.Action<Vector3> OnEnemyDeath;

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] VFXBridge");
            go.AddComponent<NightDashVFXBridge>();
            DontDestroyOnLoad(go);
            NightDashLog.Info("[VFXBridge] Auto-created.");
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

            var alive = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var pos    = transforms[i].Position;
                var hp     = stats[i].CurrentHealth;

                alive.Add(entity);

                if (_tracked.TryGetValue(entity, out var prev))
                {
                    // Health decreased → hit effect
                    float delta = prev.health - hp;
                    if (delta > 0.01f)
                    {
                        SpawnHitEffect(pos);
                    }

                    _tracked[entity] = (hp, pos);
                }
                else
                {
                    // First time seeing this entity - just track it
                    _tracked[entity] = (hp, pos);
                }
            }

            // Detect deaths: entities that were tracked but no longer alive
            var dead = new List<Entity>();
            foreach (var kvp in _tracked)
            {
                if (!alive.Contains(kvp.Key))
                    dead.Add(kvp.Key);
            }

            foreach (var entity in dead)
            {
                var info = _tracked[entity];
                SpawnDeathEffect(info.pos);

                // Notify XP drop system
                OnEnemyDeath?.Invoke(new Vector3(info.pos.x, info.pos.y, 0f));

                _tracked.Remove(entity);
            }
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

            _hitSprite   = LoadSprite(PathHit);
            _deathSprite = LoadSprite(PathDeath);

            NightDashLog.Info("[VFXBridge] Queries initialised.");
            return true;
        }

        // ====================================================================
        // VFX spawners
        // ====================================================================
        private void SpawnHitEffect(float3 pos)
        {
            var go = new GameObject("[VFX] Hit");
            go.transform.position   = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.one * HitScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _hitSprite;
            sr.sortingOrder  = HitSortOrder;
            sr.color         = new Color(1f, 1f, 0.7f, 1f); // white-yellow flash

            go.AddComponent<VFXAutoDestroy>().Init(HitDuration, fadeOut: true);
        }

        private void SpawnDeathEffect(float3 pos)
        {
            var go = new GameObject("[VFX] Death");
            go.transform.position   = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.one * DeathScaleStart;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _deathSprite;
            sr.sortingOrder  = DeathSortOrder;
            sr.color         = new Color(1f, 0.6f, 0.2f, 1f); // warm ember colour

            go.AddComponent<VFXAutoDestroy>().Init(DeathDuration, fadeOut: true,
                scaleFrom: DeathScaleStart, scaleTo: DeathScaleEnd);
        }

        // ====================================================================
        // Sprite loader
        // ====================================================================
        private static Sprite LoadSprite(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                NightDashLog.Info($"[VFXBridge] Sprite not found: '{path}', using placeholder.");
                sprite = CreatePlaceholder();
            }
            return sprite;
        }

        private static Sprite CreatePlaceholder()
        {
            var tex = new Texture2D(8, 8);
            var px  = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = Color.yellow;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
        }
    }

    // ========================================================================
    // Helper component: auto-destroys a VFX GameObject after a duration,
    // optionally fading alpha and scaling.
    // ========================================================================
    public sealed class VFXAutoDestroy : MonoBehaviour
    {
        private float _duration;
        private float _elapsed;
        private bool  _fadeOut;
        private float _scaleFrom;
        private float _scaleTo;
        private SpriteRenderer _sr;

        public void Init(float duration, bool fadeOut = false,
                         float scaleFrom = -1f, float scaleTo = -1f)
        {
            _duration  = duration;
            _fadeOut    = fadeOut;
            _scaleFrom = scaleFrom;
            _scaleTo   = scaleTo;
            _sr        = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Fade alpha
            if (_fadeOut && _sr != null)
            {
                var c = _sr.color;
                c.a = 1f - t;
                _sr.color = c;
            }

            // Scale interpolation
            if (_scaleFrom > 0f && _scaleTo > 0f)
            {
                float s = Mathf.Lerp(_scaleFrom, _scaleTo, t);
                transform.localScale = Vector3.one * s;
            }

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
