// ============================================================================
// NightDashDebugVisualBridge.cs
// Runtime visual bridge that syncs ECS entities to Unity GameObjects with
// SpriteRenderers. Handles player, enemies (per-archetype), bosses, and
// projectiles.
//
// Note: class/file/GUID intentionally preserved during the sprite-animation
// pipeline upgrade so NightDashMain's serialized component reference stays
// intact. Rename pass deferred to a pre-release cleanup PR.
// ============================================================================

using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashDebugVisualBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------ constants
        private const float DefaultPlayerScale = 1.8f;
        private const float DefaultBossScale   = 3.6f;
        private const float ProjectilePlayerScale = 0.8f;
        private const float ProjectileEnemyScale  = 0.6f;

        // Base sorting orders are well above Stage01 environment sprites
        // (tilesets ~1, cracks ~5, props ~100..150) so Y-sort offsets cannot
        // push characters behind translucent decoration.
        private const int SortPlayer     = 1000;
        private const int SortEnemy      =  500;
        private const int SortBoss       =  800;
        private const int SortProjectile =  700;

        // Animation clip names — must match SO clip definitions.
        private const string ClipWalk = "Walk";
        private const string ClipIdle = "Idle";

        // Movement → Walk vs Idle threshold, in (units/second)². Squared to avoid sqrt.
        // Compared against per-second velocity so the deadzone is framerate-invariant —
        // high framerates make per-frame deltas tiny (e.g. 600fps: 4u/s × 0.0017s = 0.0068
        // per frame, squared = 4.6e-5) which would fall under a per-frame threshold and
        // incorrectly read as "not moving". Per-second velocity stays at the expected
        // magnitude (4u/s)² = 16 regardless of framerate.
        private const float MoveDeadzoneSq = 0.25f;

        // ------------------------------------------------------------------ id mappings
        // Player class id (RunSelection.ClassId) is used directly as animation set id.
        // Default player sprite when no class selected (e.g. tutorial).
        private const string DefaultPlayerAnimId = "class_warrior";
        private const string BossAnimId = "boss_agron";

        // ------------------------------------------------------------------ projectile VFX paths (Resources)
        // Kept as Resources for now — VFX SO migration is a follow-up task.
        private const string PathProjectile = "NightDash/Art/Stage01/VFX/spr_vfx_demon_orb";
        private static readonly Dictionary<string, (string path, float scale)> WeaponVfxMap =
            new Dictionary<string, (string, float)>
            {
                { "weapon_hellflame_slash",       ("NightDash/Art/Stage01/VFX/spr_vfx_hellflame_slash",   1.2f) },
                { "weapon_abyss_hellflame_slash", ("NightDash/Art/Stage01/VFX/spr_vfx_hellflame_slash",   1.5f) },
                { "weapon_demon_greatsword",      ("NightDash/Art/Stage01/VFX/spr_vfx_hellflame_slash",   1.6f) },
                { "weapon_demon_orb",             ("NightDash/Art/Stage01/VFX/spr_vfx_demon_orb",         0.8f) },
                { "weapon_starfall",              ("NightDash/Art/Stage01/VFX/spr_vfx_starfall",           0.8f) },
                { "weapon_void_starfall",         ("NightDash/Art/Stage01/VFX/spr_vfx_starfall",           1.0f) },
            };

        // Exposed for the dash-trail bridge so it can pull the player's
        // current sprite + flip state and clone an afterimage. Returns null
        // when the player view hasn't been spawned yet.
        public SpriteRenderer GetAnyPlayerRenderer()
        {
            foreach (var kv in _playerViews)
            {
                if (kv.Value.Renderer != null) return kv.Value.Renderer;
            }
            return null;
        }

        // ------------------------------------------------------------------ view dictionaries
        private readonly Dictionary<Entity, ViewState> _playerViews     = new();
        private readonly Dictionary<Entity, ViewState> _enemyViews      = new();
        private readonly Dictionary<Entity, ViewState> _bossViews       = new();
        private readonly Dictionary<Entity, GameObject> _projectileViews = new();

        private readonly Dictionary<Entity, float3> _lastPositions = new();

        // Reusable scratch sets — avoid per-frame heap allocation in LateUpdate.
        private readonly HashSet<Entity> _alivePlayer     = new();
        private readonly HashSet<Entity> _aliveEnemy      = new();
        private readonly HashSet<Entity> _aliveBoss       = new();
        private readonly HashSet<Entity> _aliveProjectile = new();
        private readonly List<Entity>    _staleScratch    = new();

        // ------------------------------------------------------------------ Resources fallback cache (projectiles only)
        private readonly Dictionary<string, Sprite> _spriteCache = new();

        // ------------------------------------------------------------------ ECS queries
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private EntityQuery _bossQuery;
        private EntityQuery _projectileQuery;
        private EntityQuery _runSelectionQuery;

        private World _world;

        // Per-entity animation runtime state.
        private struct ViewState
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public SpriteAnimationSetSO AnimSet;
            public AnimationClipDef CurrentClip;
            public string CurrentClipName;
            public float ClipTime;
            public float RenderScale;
            public bool SourceFacesLeft;
            public int BaseSortOrder;
            public string AnimSetId; // tracks which class/archetype this view was built for
        }

        // ====================================================================
        // Bootstrap
        // ====================================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Skip if a NightDashMain-bound instance already exists.
            if (FindAnyObjectByType<NightDashDebugVisualBridge>() != null) return;

            var go = new GameObject("[NightDash] DebugVisualBridge");
            go.AddComponent<NightDashDebugVisualBridge>();
            DontDestroyOnLoad(go);
            NightDashLog.Info("[VisualBridge] Auto-created (fallback).");
        }
#endif

        // ====================================================================
        // Lifecycle
        // ====================================================================
        private void LateUpdate()
        {
            if (!TryGetWorld()) return;

            float dt = Time.deltaTime;
            SyncPlayers(dt);
            SyncEnemies(dt);
            SyncBosses(dt);
            SyncProjectiles();
            CleanupAnimationState();
        }

        private void OnDestroy()
        {
            DestroyAllViews(_playerViews);
            DestroyAllViews(_enemyViews);
            DestroyAllViews(_bossViews);
            DestroyAllProjectiles(_projectileViews);
        }

        // Clears every spawned view GameObject. Called by Title/Lobby UI when
        // the gameplay world should be visually invisible (entities may still
        // exist in the ECS world, they just have no rendered counterpart).
        public void DestroyAllViewsImmediate()
        {
            DestroyAllViews(_playerViews);
            DestroyAllViews(_enemyViews);
            DestroyAllViews(_bossViews);
            DestroyAllProjectiles(_projectileViews);
            _lastPositions.Clear();
        }

        // ====================================================================
        // World / Query helpers
        // ====================================================================
        private bool TryGetWorld()
        {
            if (_world != null && _world.IsCreated) return true;

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
                ComponentType.Exclude<Prefab>(),
                ComponentType.Exclude<BossTag>());

            _bossQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<BossTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            _projectileQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProjectileData>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            _runSelectionQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<RunSelection>());

            NightDashLog.Info("[VisualBridge] Queries initialised.");
            return true;
        }

        // ====================================================================
        // Sync: Players
        // ====================================================================
        private void SyncPlayers(float dt)
        {
            using var entities   = _playerQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            _alivePlayer.Clear();
            string currentClassId = ReadCurrentClassId();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                _alivePlayer.Add(entity);

                bool exists = _playerViews.TryGetValue(entity, out var view);

                // Re-create view when class id has changed (lobby class swap),
                // since sprites and clips are baked into the ViewState.
                if (exists && view.AnimSetId != currentClassId)
                {
                    if (view.Go != null) Destroy(view.Go);
                    exists = false;
                    NightDashLog.Info($"[VisualBridge] Player class changed '{view.AnimSetId}' -> '{currentClassId}', rebuilding view.");
                }

                if (!exists)
                {
                    var animSet = LookupAnimSet(currentClassId);
                    view = CreateAnimatedView("Player", animSet, DefaultPlayerScale, SortPlayer);
                    view.AnimSetId = currentClassId;
                    _playerViews[entity] = view;
                    NightDashLog.Info($"[VisualBridge] Player view created for {entity} (classId='{currentClassId}', animSet={(animSet != null ? animSet.name : "null")})");
                }

                StepAnimation(ref view, entity, transforms[i].Position, dt);
                _playerViews[entity] = view;
                SetPosition(view.Go, transforms[i].Position, view.BaseSortOrder);
            }

            RemoveStaleViews(_playerViews, _alivePlayer);
        }

        private string ReadCurrentClassId()
        {
            // TryGetWorld() must have run before this — query is initialised.
            if (_runSelectionQuery.CalculateEntityCount() > 0)
            {
                var sel = _runSelectionQuery.GetSingleton<RunSelection>();
                var raw = sel.ClassId.ToString();
                if (!string.IsNullOrEmpty(raw)) return raw;
            }
            return DefaultPlayerAnimId;
        }

        private SpriteAnimationSetSO ResolvePlayerAnimSet()
        {
            return LookupAnimSet(ReadCurrentClassId());
        }

        // ====================================================================
        // Sync: Enemies
        // ====================================================================
        private void SyncEnemies(float dt)
        {
            using var entities   = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var em = _world.EntityManager;

            _aliveEnemy.Clear();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                _aliveEnemy.Add(entity);

                if (!_enemyViews.TryGetValue(entity, out var view))
                {
                    string archetypeId = "ghoul_scout"; // fallback
                    if (em.HasComponent<EnemyArchetypeData>(entity))
                    {
                        archetypeId = em.GetComponentData<EnemyArchetypeData>(entity).Id.ToString();
                    }

                    var animSet = LookupAnimSet(archetypeId);
                    view = CreateAnimatedView($"Enemy_{archetypeId}", animSet, 1f, SortEnemy);
                    _enemyViews[entity] = view;
                }

                StepAnimation(ref view, entity, transforms[i].Position, dt);
                _enemyViews[entity] = view;
                SetPosition(view.Go, transforms[i].Position, view.BaseSortOrder);
            }

            RemoveStaleViews(_enemyViews, _aliveEnemy);
        }

        // ====================================================================
        // Sync: Bosses
        // ====================================================================
        private void SyncBosses(float dt)
        {
            using var entities   = _bossQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _bossQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            _aliveBoss.Clear();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                _aliveBoss.Add(entity);

                if (!_bossViews.TryGetValue(entity, out var view))
                {
                    var animSet = LookupAnimSet(BossAnimId);
                    view = CreateAnimatedView("Boss", animSet, DefaultBossScale, SortBoss);
                    _bossViews[entity] = view;
                    NightDashLog.Info($"[VisualBridge] Boss view created for {entity}");
                }

                StepAnimation(ref view, entity, transforms[i].Position, dt);
                _bossViews[entity] = view;
                SetPosition(view.Go, transforms[i].Position, view.BaseSortOrder);
            }

            RemoveStaleViews(_bossViews, _aliveBoss);
        }

        // ====================================================================
        // Sync: Projectiles (legacy Resources path — VFX SO migration is a follow-up)
        // ====================================================================
        private void SyncProjectiles()
        {
            using var entities     = _projectileQuery.ToEntityArray(Allocator.Temp);
            using var transforms   = _projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var projectiles  = _projectileQuery.ToComponentDataArray<ProjectileData>(Allocator.Temp);

            _aliveProjectile.Clear();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                _aliveProjectile.Add(entity);

                if (!_projectileViews.TryGetValue(entity, out var go))
                {
                    bool isPlayer = projectiles[i].IsPlayerOwned != 0;
                    var tint  = isPlayer ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);

                    string weaponId = projectiles[i].WeaponId.ToString();
                    bool isMelee = projectiles[i].IsMelee != 0;
                    Sprite sprite;
                    float scale;

                    if (isPlayer && !string.IsNullOrEmpty(weaponId) && WeaponVfxMap.TryGetValue(weaponId, out var vfxInfo))
                    {
                        sprite = LoadSpriteFromResources(vfxInfo.path);
                        scale = vfxInfo.scale;
                        if (isMelee) tint = new Color(1f, 0.9f, 0.6f, 0.9f);
                    }
                    else
                    {
                        sprite = LoadSpriteFromResources(PathProjectile);
                        scale = isPlayer ? ProjectilePlayerScale : ProjectileEnemyScale;
                    }

                    go = CreateStaticView("Projectile", sprite, scale, SortProjectile, tint);
                    _projectileViews[entity] = go;
                }

                SetPosition(go, transforms[i].Position, SortProjectile);

                // Projectile rotation toward velocity. Guarded against entities
                // destroyed earlier this frame by CombatSystem.
                if (_world != null && _world.IsCreated)
                {
                    var em = _world.EntityManager;
                    if (em.Exists(entity) && em.HasComponent<PhysicsVelocity2D>(entity))
                    {
                        var vel = em.GetComponentData<PhysicsVelocity2D>(entity).Value;
                        if (math.lengthsq(vel) > 0.01f)
                        {
                            float angle = math.degrees(math.atan2(vel.y, vel.x));
                            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        }
                    }
                }
            }

            RemoveStaleProjectiles(_projectileViews, _aliveProjectile);
        }

        // ====================================================================
        // Animation core
        // ====================================================================
        // Picks Walk/Idle clip based on velocity, advances clip time, swaps
        // sprite, updates flipX. Fallback: holds first frame of any clip.
        private void StepAnimation(ref ViewState view, Entity entity, float3 pos, float dt)
        {
            if (view.Go == null) return;

            // Velocity from previous-frame position diff (no PhysicsVelocity2D dep).
            // Normalize by dt so the threshold compares per-second velocity — otherwise
            // higher framerates would shrink the per-frame delta below the deadzone and
            // incorrectly read every moving entity as "not moving".
            float3 velocity = float3.zero;
            if (_lastPositions.TryGetValue(entity, out var last))
            {
                velocity = pos - last;
            }
            _lastPositions[entity] = pos;

            float invDt = dt > 1e-5f ? 1f / dt : 0f;
            float vxPerSec = velocity.x * invDt;
            float vyPerSec = velocity.y * invDt;
            float speedSq = vxPerSec * vxPerSec + vyPerSec * vyPerSec;
            bool moving = speedSq > MoveDeadzoneSq;

            // Flip horizontally based on movement direction (preserves last facing on idle).
            if (view.Renderer != null && math.abs(velocity.x) > 0.001f)
            {
                view.Renderer.flipX = view.SourceFacesLeft ? velocity.x > 0f : velocity.x < 0f;
            }

            // Pick clip; reset clip time when switching.
            string desired = moving ? ClipWalk : ClipIdle;
            if (view.AnimSet != null && view.CurrentClipName != desired)
            {
                var picked = view.AnimSet.GetClipOrFallback(desired, ClipWalk);
                if (picked != null)
                {
                    view.CurrentClip = picked;
                    view.CurrentClipName = picked.name;
                    view.ClipTime = 0f;
                }
            }

            if (view.CurrentClip == null || view.CurrentClip.FrameCount == 0)
            {
                return; // SO missing or empty — leave whatever sprite was set on creation
            }

            view.ClipTime += dt;

            // Fold loop time into one cycle to avoid float precision drift on long sessions.
            if (view.CurrentClip.loop && view.CurrentClip.fps > 0f)
            {
                float duration = view.CurrentClip.FrameCount / view.CurrentClip.fps;
                if (duration > 0f && view.ClipTime >= duration)
                {
                    view.ClipTime %= duration;
                }
            }

            var sprite = view.CurrentClip.GetFrameAt(view.ClipTime);
            if (sprite != null && view.Renderer != null)
            {
                view.Renderer.sprite = sprite;
            }
        }

        // ====================================================================
        // View creation / destruction
        // ====================================================================
        private static ViewState CreateAnimatedView(string label, SpriteAnimationSetSO animSet, float defaultScale, int sortOrder)
        {
            var go = new GameObject($"[View] {label}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortOrder;
            sr.color = Color.white;

            float scale = defaultScale;
            bool sourceFacesLeft = true;
            AnimationClipDef firstClip = null;

            if (animSet != null)
            {
                if (animSet.renderScale > 0f) scale = animSet.renderScale;
                sourceFacesLeft = animSet.sourceFacesLeft;
                firstClip = animSet.GetClipOrFallback(ClipIdle, ClipWalk);
                if (firstClip != null && firstClip.FrameCount > 0)
                {
                    sr.sprite = firstClip.frames[0];
                }
            }

            if (sr.sprite == null)
            {
                sr.sprite = CreatePlaceholderSprite();
            }

            go.transform.localScale = Vector3.one * scale;

            return new ViewState
            {
                Go = go,
                Renderer = sr,
                AnimSet = animSet,
                CurrentClip = firstClip,
                CurrentClipName = firstClip != null ? firstClip.name : null,
                ClipTime = 0f,
                RenderScale = scale,
                SourceFacesLeft = sourceFacesLeft,
                BaseSortOrder = sortOrder,
            };
        }

        private static GameObject CreateStaticView(string label, Sprite sprite, float scale, int sortOrder, Color tint)
        {
            var go = new GameObject($"[View] {label}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortOrder;
            sr.color = tint;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private SpriteAnimationSetSO LookupAnimSet(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var registry = DataRegistry.Instance;
            if (registry == null)
            {
                NightDashLog.Warn("[VisualBridge] DataRegistry not ready; sprite will use placeholder.");
                return null;
            }

            if (!registry.TryGetAnimationSet(id, out var set))
            {
                NightDashLog.Info($"[VisualBridge] No animation set for id '{id}'.");
                return null;
            }

            return set;
        }

        // ====================================================================
        // Helpers
        // ====================================================================
        private Sprite LoadSpriteFromResources(string resourcePath)
        {
            if (_spriteCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                NightDashLog.Info($"[VisualBridge] Sprite not found at '{resourcePath}', generating placeholder.");
                sprite = CreatePlaceholderSprite();
            }

            _spriteCache[resourcePath] = sprite;
            return sprite;
        }

        private static Sprite _placeholderSprite;
        private static Sprite CreatePlaceholderSprite()
        {
            if (_placeholderSprite != null) return _placeholderSprite;
            var tex = new Texture2D(16, 16);
            var pixels = new Color[16 * 16];
            for (int j = 0; j < pixels.Length; j++) pixels[j] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            return _placeholderSprite;
        }

        private static void SetPosition(GameObject go, float3 pos, int baseSortOrder)
        {
            if (go == null) return;
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // Y-sort: bottom-edge offset added to type-base order (preserves
            // Player > Boss > Enemy > Projectile layering across screens).
            // Multiplier kept small + clamped so the offset never pushes the
            // character into Stage01 environment sortingOrder range (1..150).
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float halfHeight = go.transform.localScale.y * 0.5f;
                float bottomY = pos.y - halfHeight;
                int yOffset = Mathf.Clamp((int)(-bottomY * 2f), -200, 200);
                sr.sortingOrder = baseSortOrder + yOffset;
            }
        }

        private void CleanupAnimationState()
        {
            if (_lastPositions.Count == 0) return;

            _staleScratch.Clear();
            foreach (var kvp in _lastPositions)
            {
                if (_playerViews.ContainsKey(kvp.Key)) continue;
                if (_enemyViews.ContainsKey(kvp.Key)) continue;
                if (_bossViews.ContainsKey(kvp.Key)) continue;
                _staleScratch.Add(kvp.Key);
            }

            for (int i = 0; i < _staleScratch.Count; i++) _lastPositions.Remove(_staleScratch[i]);
        }

        private void RemoveStaleViews(Dictionary<Entity, ViewState> views, HashSet<Entity> alive)
        {
            _staleScratch.Clear();
            foreach (var kvp in views)
            {
                if (!alive.Contains(kvp.Key)) _staleScratch.Add(kvp.Key);
            }

            for (int i = 0; i < _staleScratch.Count; i++)
            {
                var key = _staleScratch[i];
                if (views.TryGetValue(key, out var view) && view.Go != null) Destroy(view.Go);
                views.Remove(key);
            }
        }

        private void RemoveStaleProjectiles(Dictionary<Entity, GameObject> views, HashSet<Entity> alive)
        {
            _staleScratch.Clear();
            foreach (var kvp in views)
            {
                if (!alive.Contains(kvp.Key)) _staleScratch.Add(kvp.Key);
            }

            for (int i = 0; i < _staleScratch.Count; i++)
            {
                var key = _staleScratch[i];
                if (views.TryGetValue(key, out var go) && go != null) Destroy(go);
                views.Remove(key);
            }
        }

        private static void DestroyAllViews(Dictionary<Entity, ViewState> views)
        {
            foreach (var kvp in views)
            {
                if (kvp.Value.Go != null) Destroy(kvp.Value.Go);
            }
            views.Clear();
        }

        private static void DestroyAllProjectiles(Dictionary<Entity, GameObject> views)
        {
            foreach (var kvp in views)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            views.Clear();
        }
    }
}
