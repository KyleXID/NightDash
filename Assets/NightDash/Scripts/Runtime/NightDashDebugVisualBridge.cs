// ============================================================================
// NightDashDebugVisualBridge.cs
// Runtime visual bridge that syncs ECS entities to Unity GameObjects with
// SpriteRenderers. Handles player, enemies (per-archetype), bosses, and
// projectiles.
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
    public sealed class NightDashDebugVisualBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------ constants
        private const float PlayerScale   = 1.2f;
        private const float BossScale     = 2.5f;
        private const float ProjectilePlayerScale = 0.8f;
        private const float ProjectileEnemyScale  = 0.6f;

        private const int SortPlayer     = 100;
        private const int SortEnemy      =  50;
        private const int SortBoss       =  80;
        private const int SortProjectile =  70;

        // ------------------------------------------------------------------ sprite paths (Resources)
        // Player classes
        private const string PathPlayerDefault   = "NightDash/Art/Stage01/Characters/spr_char_warrior";
        private const string PathPlayerBlade     = "NightDash/Art/Stage01/Characters/spr_char_warrior";
        private const string PathPlayerShadow    = "NightDash/Art/Stage01/Characters/spr_char_mage";
        private const string PathPlayerDemon     = "NightDash/Art/Stage01/Characters/spr_char_astrologer";

        // Enemies (per archetype)
        private const string PathEnemyGhoulScout     = "NightDash/Art/Stage01/Enemies/spr_enemy_ghoul_scout";
        private const string PathEnemyEmberBat       = "NightDash/Art/Stage01/Enemies/spr_enemy_ember_bat";
        private const string PathEnemyAshCaster      = "NightDash/Art/Stage01/Enemies/spr_enemy_ash_caster";
        private const string PathEnemyWastelandBrute = "NightDash/Art/Stage01/Enemies/spr_enemy_wasteland_brute";
        private const string PathEnemyFallback       = "NightDash/Art/Stage01/Enemies/spr_enemy_ghoul_scout";

        // Boss
        private const string PathBoss = "NightDash/Art/Stage01/Enemies/spr_boss_agron";

        // Projectile (fallback)
        private const string PathProjectile = "NightDash/Art/Stage01/VFX/spr_vfx_demon_orb";

        // ------------------------------------------------------------------ weapon → VFX sprite mapping
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

        // ------------------------------------------------------------------ archetype → (path, scale)
        private static readonly Dictionary<string, (string path, float scale)> EnemyArchetypeMap =
            new Dictionary<string, (string, float)>
            {
                { "ghoul_scout",     (PathEnemyGhoulScout,     1.2f) },
                { "ember_bat",       (PathEnemyEmberBat,       1.0f) },
                { "ash_caster",      (PathEnemyAshCaster,      1.2f) },
                { "wasteland_brute", (PathEnemyWastelandBrute, 1.8f) },
            };

        // ------------------------------------------------------------------ view dictionaries
        private readonly Dictionary<Entity, GameObject> _playerViews     = new();
        private readonly Dictionary<Entity, GameObject> _enemyViews      = new();
        private readonly Dictionary<Entity, GameObject> _bossViews       = new();
        private readonly Dictionary<Entity, GameObject> _projectileViews = new();

        // ------------------------------------------------------------------ cached sprites
        private readonly Dictionary<string, Sprite> _spriteCache = new();

        // ------------------------------------------------------------------ ECS queries
        private EntityQuery _playerQuery;
        private EntityQuery _enemyQuery;
        private EntityQuery _bossQuery;
        private EntityQuery _projectileQuery;

        private World _world;

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] DebugVisualBridge");
            go.AddComponent<NightDashDebugVisualBridge>();
            DontDestroyOnLoad(go);
            NightDashLog.Info("[VisualBridge] Auto-created.");
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================
        private void LateUpdate()
        {
            if (!TryGetWorld()) return;

            SyncPlayers();
            SyncEnemies();
            SyncBosses();
            SyncProjectiles();
        }

        private void OnDestroy()
        {
            DestroyAll(_playerViews);
            DestroyAll(_enemyViews);
            DestroyAll(_bossViews);
            DestroyAll(_projectileViews);
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

            NightDashLog.Info("[VisualBridge] Queries initialised.");
            return true;
        }

        // ====================================================================
        // Sync: Players
        // ====================================================================
        private void SyncPlayers()
        {
            using var entities   = _playerQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var alive = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                alive.Add(entity);

                if (!_playerViews.TryGetValue(entity, out var go))
                {
                    var sprite = ResolvePlayerSprite();
                    go = CreateView("Player", sprite, PlayerScale, SortPlayer, Color.white);
                    _playerViews[entity] = go;
                    NightDashLog.Info($"[VisualBridge] Player view created for {entity}");
                }

                SetPosition(go, transforms[i].Position);
            }

            RemoveStale(_playerViews, alive);
        }

        private Sprite ResolvePlayerSprite()
        {
            // Try to read RunSelection from ECS to pick class sprite
            string classId = null;
            if (_world != null && _world.IsCreated)
            {
                var em = _world.EntityManager;
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly<RunSelection>());
                if (query.CalculateEntityCount() > 0)
                {
                    var sel = query.GetSingleton<RunSelection>();
                    classId = sel.ClassId.ToString();
                }
            }

            string path = classId switch
            {
                "class_warrior"    => PathPlayerBlade,
                "class_mage"       => PathPlayerShadow,
                "class_astrologer" => PathPlayerDemon,
                _                  => PathPlayerDefault,
            };

            return LoadSprite(path);
        }

        // ====================================================================
        // Sync: Enemies (per-archetype sprites)
        // ====================================================================
        private void SyncEnemies()
        {
            using var entities   = _enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var em = _world.EntityManager;

            var alive = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                alive.Add(entity);

                if (!_enemyViews.TryGetValue(entity, out var go))
                {
                    string archetypeId = "ghoul_scout"; // fallback
                    if (em.HasComponent<EnemyArchetypeData>(entity))
                    {
                        archetypeId = em.GetComponentData<EnemyArchetypeData>(entity).Id.ToString();
                    }
                    ResolveEnemyVisual(archetypeId, out var sprite, out var scale);

                    go = CreateView($"Enemy_{archetypeId}", sprite, scale, SortEnemy, Color.white);
                    _enemyViews[entity] = go;
                }

                SetPosition(go, transforms[i].Position);
            }

            RemoveStale(_enemyViews, alive);
        }

        private void ResolveEnemyVisual(string archetypeId, out Sprite sprite, out float scale)
        {
            if (EnemyArchetypeMap.TryGetValue(archetypeId, out var info))
            {
                sprite = LoadSprite(info.path);
                scale  = info.scale;
            }
            else
            {
                sprite = LoadSprite(PathEnemyFallback);
                scale  = 0.38f;
                NightDashLog.Info($"[VisualBridge] Unknown archetype '{archetypeId}', using fallback sprite.");
            }
        }

        // ====================================================================
        // Sync: Bosses
        // ====================================================================
        private void SyncBosses()
        {
            using var entities   = _bossQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _bossQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var alive = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                alive.Add(entity);

                if (!_bossViews.TryGetValue(entity, out var go))
                {
                    var sprite = LoadSprite(PathBoss);
                    go = CreateView("Boss", sprite, BossScale, SortBoss, Color.white);
                    _bossViews[entity] = go;
                    NightDashLog.Info($"[VisualBridge] Boss view created for {entity}");
                }

                SetPosition(go, transforms[i].Position);
            }

            RemoveStale(_bossViews, alive);
        }

        // ====================================================================
        // Sync: Projectiles
        // ====================================================================
        private void SyncProjectiles()
        {
            using var entities     = _projectileQuery.ToEntityArray(Allocator.Temp);
            using var transforms   = _projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var projectiles  = _projectileQuery.ToComponentDataArray<ProjectileData>(Allocator.Temp);

            var alive = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                alive.Add(entity);

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
                        sprite = LoadSprite(vfxInfo.path);
                        scale = vfxInfo.scale;

                        // Melee tint: brighter flash
                        if (isMelee)
                        {
                            tint = new Color(1f, 0.9f, 0.6f, 0.9f);
                        }
                    }
                    else
                    {
                        sprite = LoadSprite(PathProjectile);
                        scale = isPlayer ? ProjectilePlayerScale : ProjectileEnemyScale;
                    }

                    go = CreateView("Projectile", sprite, scale, SortProjectile, tint);
                    _projectileViews[entity] = go;
                }

                SetPosition(go, transforms[i].Position);

                // Rotate projectile sprite to face movement direction
                if (_world != null && _world.IsCreated)
                {
                    var em = _world.EntityManager;
                    if (em.HasComponent<PhysicsVelocity2D>(entity))
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

            RemoveStale(_projectileViews, alive);
        }

        // ====================================================================
        // Helpers
        // ====================================================================
        private Sprite LoadSprite(string resourcePath)
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

        private static Sprite CreatePlaceholderSprite()
        {
            var tex = new Texture2D(16, 16);
            var pixels = new Color[16 * 16];
            for (int j = 0; j < pixels.Length; j++) pixels[j] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        }

        private static GameObject CreateView(string label, Sprite sprite, float scale, int sortOrder, Color tint)
        {
            var go = new GameObject($"[View] {label}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder  = sortOrder;
            sr.color         = tint;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static void SetPosition(GameObject go, float3 pos)
        {
            if (go == null) return;
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // Y-sort based on bottom edge of sprite
            // Lower bottom = higher sorting order = rendered in front
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float halfHeight = go.transform.localScale.y * 0.5f;
                float bottomY = pos.y - halfHeight;
                sr.sortingOrder = 200 + (int)(-bottomY * 10f);
            }
        }

        private static void RemoveStale(Dictionary<Entity, GameObject> views, HashSet<Entity> alive)
        {
            // Collect stale keys first to avoid modifying during iteration
            var stale = new List<Entity>();
            foreach (var kvp in views)
            {
                if (!alive.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }

            foreach (var key in stale)
            {
                if (views.TryGetValue(key, out var go))
                    Destroy(go);
                views.Remove(key);
            }
        }

        private static void DestroyAll(Dictionary<Entity, GameObject> views)
        {
            foreach (var kvp in views)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            views.Clear();
        }
    }
}
