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
        private const float DefaultPlayerScale = 1.6f;
        private const float DefaultBossScale   = 3.6f;
        private const float ProjectilePlayerScale = 0.8f;
        private const float ProjectileEnemyScale  = 0.6f;
        // Playback rate for animated weapon-VFX frame sequences (looping).
        private const float ProjectileVfxFps       = 12f;

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
        // Map value = (base Resources path, render scale). The renderer first
        // tries an animated frame sequence (<path>_01.._NN); if none exists it
        // falls back to a single static sprite at <path>. Evolved/abyss weapon
        // ids ("..._evolved" / "..._abyss") reuse their base entry via
        // TryResolveWeaponVfx() until dedicated evolution VFX are authored.
        private const string PathProjectile = "NightDash/Art/Stage01/VFX/spr_vfx_demon_orb";
        private const string VfxDir = "NightDash/Art/Stage01/VFX/";
        private static readonly Dictionary<string, (string path, float scale)> WeaponVfxMap =
            new Dictionary<string, (string, float)>
            {
                // scale = 화면상 크기 배율. ring/barrier 등 지속·범위 무기는 히트 반경에
                // 맞춰 캐릭터(스케일 1.8)보다 크게. 모든 값 인게임 튜닝 대상.
                // Legacy / static (no frame sequence on disk → static sprite).
                { "weapon_hellflame_slash",       (VfxDir + "spr_vfx_hellflame_slash", 1.2f) },
                { "weapon_abyss_hellflame_slash", (VfxDir + "spr_vfx_hellflame_slash", 1.5f) },
                { "weapon_starfall",              (VfxDir + "spr_vfx_starfall",        1.5f) },
                { "weapon_void_starfall",         (VfxDir + "spr_vfx_evolution_void_starfall", 1.6f) }, // 공허의 별낙하 (별의 낙하 진화 결과)

                // Animated weapon VFX (frame sequences spr_vfx_<id>_NN.png).
                { "weapon_demon_greatsword",      (VfxDir + "spr_vfx_demon_greatsword", 2.0f) },
                { "weapon_chain_scythe",          (VfxDir + "spr_vfx_chain_scythe",     3.2f) },
                { "weapon_demon_orb",             (VfxDir + "spr_vfx_demon_orb",        1.4f) },
                { "weapon_abyss_tentacle",        (VfxDir + "spr_vfx_abyss_tentacle",   3.6f) },
                { "weapon_dark_barrier",          (VfxDir + "spr_vfx_dark_barrier",     5.5f) },  // 캐릭터보다 약간 크게 (반투명)
                { "weapon_dark_lightning",        (VfxDir + "spr_vfx_dark_lightning",   2.2f) },
                { "weapon_hell_hammer",           (VfxDir + "spr_vfx_hell_hammer",      1.9f) },
                { "weapon_holy_wave",             (VfxDir + "spr_vfx_holy_wave",        2.0f) },
                { "weapon_light_ring",            (VfxDir + "spr_vfx_light_ring",       1.4f) },  // 작은 고리 투사체, 나선형 발산
                { "weapon_rapid_shot",            (VfxDir + "spr_vfx_rapid_shot",       1.3f) },
                { "weapon_revolver",              (VfxDir + "spr_vfx_revolver",         1.2f) },
                { "weapon_shadow_arrow",          (VfxDir + "spr_vfx_shadow_arrow",     1.4f) },
                { "weapon_slash_combo",           (VfxDir + "spr_vfx_slash_combo",      1.9f) },
                { "weapon_spear",                 (VfxDir + "spr_vfx_spear",            1.6f) },
                { "weapon_spinning_blade",        (VfxDir + "spr_vfx_spinning_blade",   3.0f) },
                { "weapon_split_bullet",          (VfxDir + "spr_vfx_split_bullet",     1.4f) },
            };

        // Dedicated VFX for FIRST-tier evolution weapons ("weapon_x_evolved").
        // Keyed by BASE id (suffix stripped). The evolution sprites read as
        // bigger / brighter than base (Notion "Evolution 17종"). Abyss variants
        // are intentionally NOT here — they keep falling back to the base VFX.
        // Note: starfall's evolution is the "void starfall" visual.
        private static readonly Dictionary<string, (string path, float scale)> WeaponEvolvedVfxMap =
            new Dictionary<string, (string, float)>
            {
                { "weapon_demon_greatsword", (VfxDir + "spr_vfx_evolution_demon_greatsword", 2.0f) },
                { "weapon_demon_orb",        (VfxDir + "spr_vfx_evolution_demon_orb",        1.4f) },
                { "weapon_starfall",         (VfxDir + "spr_vfx_evolution_void_starfall",    1.6f) }, // 공허의 별낙하 (starfall_evolved)
                { "weapon_holy_wave",        (VfxDir + "spr_vfx_evolution_holy_wave",        2.5f) },
                { "weapon_light_ring",       (VfxDir + "spr_vfx_evolution_light_ring",       1.4f) },
                { "weapon_rapid_shot",       (VfxDir + "spr_vfx_evolution_rapid_shot",       1.3f) },
                { "weapon_revolver",         (VfxDir + "spr_vfx_evolution_revolver",         1.2f) },
                { "weapon_hell_hammer",      (VfxDir + "spr_vfx_evolution_hell_hammer",      1.9f) },
                { "weapon_slash_combo",      (VfxDir + "spr_vfx_evolution_slash_combo",      1.9f) },
                { "weapon_shadow_arrow",     (VfxDir + "spr_vfx_evolution_shadow_arrow",     1.4f) },
                { "weapon_spear",            (VfxDir + "spr_vfx_evolution_spear",            1.6f) },
                { "weapon_split_bullet",     (VfxDir + "spr_vfx_evolution_split_bullet",     1.4f) },
                { "weapon_spinning_blade",   (VfxDir + "spr_vfx_evolution_spinning_blade",   3.0f) },
                { "weapon_dark_barrier",     (VfxDir + "spr_vfx_evolution_dark_barrier",     5.5f) },
                { "weapon_chain_scythe",     (VfxDir + "spr_vfx_evolution_chain_scythe",     3.2f) },
                { "weapon_dark_lightning",   (VfxDir + "spr_vfx_evolution_dark_lightning",   2.2f) },
                { "weapon_abyss_tentacle",   (VfxDir + "spr_vfx_evolution_abyss_tentacle",   3.6f) },
            };

        // Resolves a weapon id to its VFX entry. First-tier evolution variants
        // ("weapon_x_evolved") prefer their dedicated evolution VFX, then fall
        // back to the base weapon entry. Abyss variants ("weapon_x_abyss") strip
        // straight to the base VFX (no dedicated abyss art yet).
        // NOTE: only the trailing "_evolved"/"_abyss" segment is stripped, and
        // only after a direct lookup fails. Base weapons whose id naturally ends
        // with one of those tokens (e.g. weapon_abyss_tentacle) MUST keep a
        // direct map entry so the strip path is never reached for them.
        private static bool TryResolveWeaponVfx(string weaponId, out (string path, float scale) info)
        {
            if (WeaponVfxMap.TryGetValue(weaponId, out info)) return true;

            string baseId = null;
            if (weaponId.EndsWith("_evolved"))
            {
                baseId = weaponId.Substring(0, weaponId.Length - "_evolved".Length);
                if (WeaponEvolvedVfxMap.TryGetValue(baseId, out info)) return true; // dedicated evolution VFX
            }
            else if (weaponId.EndsWith("_abyss")) baseId = weaponId.Substring(0, weaponId.Length - "_abyss".Length);

            if (baseId != null && WeaponVfxMap.TryGetValue(baseId, out info)) return true;

            info = default;
            return false;
        }

        // True when a weapon id is a first-tier evolution ("..._evolved").
        private static bool IsEvolved(string weaponId) => weaponId.EndsWith("_evolved");

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

        // Generic Entity → SpriteRenderer accessor used by the status-effect
        // tint bridge. Searches player → enemy → boss views in order.
        public bool TryGetRenderer(Entity entity, out SpriteRenderer renderer)
        {
            if (_playerViews.TryGetValue(entity, out var pv) && pv.Renderer != null)
            {
                renderer = pv.Renderer;
                return true;
            }
            if (_enemyViews.TryGetValue(entity, out var ev) && ev.Renderer != null)
            {
                renderer = ev.Renderer;
                return true;
            }
            if (_bossViews.TryGetValue(entity, out var bv) && bv.Renderer != null)
            {
                renderer = bv.Renderer;
                return true;
            }
            renderer = null;
            return false;
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
        // Cache of animated frame sequences keyed by base path. A null value is
        // cached for paths with no sequence so we don't re-probe Resources.
        private readonly Dictionary<string, Sprite[]> _frameCache = new();

        // Chain-scythe whip: a stretching chain rendered between the player and
        // the flying scythe head. One chain GameObject per whip projectile.
        private readonly Dictionary<Entity, GameObject> _whipChains = new();
        // Evolved meteor entities — used to fire an icy sparkle burst when they land
        // (i.e. when their projectile view is removed on lifetime end).
        private readonly HashSet<Entity> _meteorEntities = new();
        private const string ChainVariantPrefix = "NightDash/Art/Stage01/VFX/spr_vfx_chain_scythe_chain_";
        private const string ChainVariantEvolvedPrefix = "NightDash/Art/Stage01/VFX/spr_vfx_evolution_chain_scythe_chain_";
        private Sprite[] _chainVariants;        // base chain-link sprites (spr_vfx_chain_scythe_chain_1..N)
        private Sprite[] _chainVariantsEvolved; // evolution chain-link sprites (spr_vfx_evolution_chain_scythe_chain_1..N)
        private const float HeadRingOffset = 0.6f; // world units from the scythe-head center back to its chain ring
        // Two-tone chain-scythe palette: icy-blue scythe head, dark-navy chain links.
        // Multiplied onto the (otherwise white) sprite, so values < 1 darken/tint.
        private static readonly Color ScytheHeadTint  = new Color(0.55f, 0.78f, 1.00f, 1f);
        private static readonly Color ScytheChainTint = new Color(0.16f, 0.20f, 0.46f, 1f);

        // Melee weapon range indicators: a faint dotted ring + icon around the
        // player showing each melee weapon's reach. Keyed by weapon id; built on
        // first sight, then just repositioned. Range is approximated from the
        // weapon's baseRange (RangeMultiplier / level scaling not reflected).
        private EntityQuery _ownedWeaponQuery;
        private readonly Dictionary<string, WeaponRangeIndicator> _rangeIndicators = new();
        private static readonly string[] RangeIndicatorWeapons = { "weapon_slash_combo", "weapon_hell_hammer" };
        private const int SortRangeIndicator = 160; // above Stage01 props (~150), well below characters
        private static Sprite _rangeDotSprite;

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
            SyncWeaponRangeIndicators();
            CleanupAnimationState();
        }

        private void OnDestroy()
        {
            DestroyAllViews(_playerViews);
            DestroyAllViews(_enemyViews);
            DestroyAllViews(_bossViews);
            DestroyAllProjectiles(_projectileViews);
            DestroyAllWhipChains();
            DestroyAllRangeIndicators();
            _meteorEntities.Clear();
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
            DestroyAllWhipChains();
            DestroyAllRangeIndicators();
            _meteorEntities.Clear();
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

            _ownedWeaponQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<OwnedWeaponElement>());

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

                    if (isPlayer && !string.IsNullOrEmpty(weaponId) && TryResolveWeaponVfx(weaponId, out var vfxInfo))
                    {
                        if (isMelee) tint = new Color(1f, 0.9f, 0.6f, 0.9f);
                        // Barrier shield is rendered see-through so it doesn't obscure
                        // the player / enemies underneath (readability). (Light ring is
                        // now a small spiralling projectile, so it stays fully opaque.)
                        if (weaponId.Contains("dark_barrier"))
                            tint = new Color(1f, 1f, 1f, 0.5f);

                        // Prefer an animated frame sequence (spr_vfx_<id>_NN).
                        var frames = LoadFramesFromResources(vfxInfo.path);
                        if (frames != null && frames.Length >= 2)
                        {
                            // PlayOnce weapons (e.g. star-fall) play the whole sequence
                            // exactly once over their Lifetime so the animation finishes
                            // right as they land; everything else loops at the default fps.
                            bool playOnce = projectiles[i].PlayOnce != 0;
                            float life = projectiles[i].Lifetime;
                            // dark_barrier reads better as a slow, calm pulse.
                            float loopFps = weaponId.Contains("dark_barrier") ? 5f : ProjectileVfxFps;
                            float fps = (playOnce && life > 0.01f) ? (frames.Length / life) : loopFps;
                            go = CreateAnimatedProjectileView("Projectile", frames, vfxInfo.scale, SortProjectile, tint, fps, loop: !playOnce);
                        }
                        else
                        {
                            // A lone "_01" frame is used directly; otherwise load
                            // the single static sprite at the base path.
                            var staticSprite = (frames != null && frames.Length == 1)
                                ? frames[0]
                                : LoadSpriteFromResources(vfxInfo.path);
                            go = CreateStaticView("Projectile", staticSprite, vfxInfo.scale, SortProjectile, tint);
                        }
                    }
                    else
                    {
                        var sprite = LoadSpriteFromResources(PathProjectile);
                        float scale = isPlayer ? ProjectilePlayerScale : ProjectileEnemyScale;
                        go = CreateStaticView("Projectile", sprite, scale, SortProjectile, tint);
                    }

                    // Shadow arrow + (evolved) holy wave leave a fading afterimage trail.
                    if (go != null && (weaponId.Contains("shadow_arrow") || weaponId.Contains("holy_wave")))
                        go.AddComponent<VFXAfterimage>();

                    // Abyss tentacle EVOLUTION only: play the eruption once, then loop
                    // just the tail (settled writhe) frames instead of re-erupting from
                    // frame 1. The base tentacle keeps looping its full clip as before.
                    if (go != null && weaponId.Contains("abyss_tentacle") && IsEvolved(weaponId))
                    {
                        var tentAnim = go.GetComponent<SpriteAnimator>();
                        if (tentAnim != null && tentAnim.frames != null && tentAnim.frames.Length > 3)
                            tentAnim.loopStartFrame = tentAnim.frames.Length - 3; // loop last 3 frames
                    }

                    // Evolved star-fall (void starfall): a twinkling glint pinned to the
                    // comet's star head as it descends + an icy sparkle burst on landing.
                    if (go != null && (weaponId.Contains("void_starfall") || weaponId.Contains("starfall_evolved")))
                    {
                        AttachStarTwinkle(go);
                        _meteorEntities.Add(entity);
                    }

                    // Chain scythe: a row of mixed chain-link sprites spanning from the
                    // player to the flying scythe head. Two-tone palette — the head reads
                    // icy-blue, the chain links read dark navy.
                    if (go != null && isPlayer && weaponId.Contains("chain_scythe"))
                    {
                        var headSr = go.GetComponent<SpriteRenderer>();
                        if (headSr != null) headSr.color = ScytheHeadTint;

                        var chainGo = new GameObject("[VFX] WhipChain");
                        var chainRenderer = chainGo.AddComponent<WhipChainRenderer>();
                        bool evoChain = IsEvolved(weaponId);
                        // Evolution links are drawn as ~-47° diagonal ovals — pin them
                        // upright: rotating by -43° lands the long axis at vertical.
                        float linkAngle = evoChain ? -43f : float.NaN;
                        chainRenderer.Init(LoadChainVariants(evoChain), SortProjectile - 1, ScytheChainTint, linkAngle);
                        _whipChains[entity] = chainGo;
                    }

                    _projectileViews[entity] = go;
                }

                SetPosition(go, transforms[i].Position, SortProjectile);

                // Orient the scythe head + stretch the chain to its ring.
                if (_whipChains.TryGetValue(entity, out var whipChain) && whipChain != null)
                {
                    UpdateWhipChain(whipChain, go);
                }

                // Projectile rotation toward velocity. Guarded against entities
                // destroyed earlier this frame by CombatSystem.
                if (_world != null && _world.IsCreated)
                {
                    var em = _world.EntityManager;
                    if (em.Exists(entity) && em.HasComponent<ProjectileData>(entity) && em.HasComponent<PhysicsVelocity2D>(entity))
                    {
                        // Only directional projectiles (bullets/arrows/spears) rotate to
                        // face travel. Sky-fall bolts, melee sweeps and orbit weapons keep
                        // their drawn orientation (AlignToVelocity == 0).
                        if (em.GetComponentData<ProjectileData>(entity).AlignToVelocity != 0)
                        {
                            var vel = em.GetComponentData<PhysicsVelocity2D>(entity).Value;
                            if (math.lengthsq(vel) > 0.01f)
                            {
                                float angle = math.degrees(math.atan2(vel.y, vel.x));
                                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                            }
                        }
                        else if (em.HasComponent<OrbitState>(entity))
                        {
                            // Orbiting blades / melee sweep: face outward along the
                            // orbit angle so the blade visibly swings around the player.
                            var orbit = em.GetComponentData<OrbitState>(entity);
                            if (orbit.Radius > 0.01f)
                            {
                                go.transform.rotation = Quaternion.Euler(0f, 0f, math.degrees(orbit.Angle));
                            }
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

        // Projectile view that plays a looping sprite-frame animation.
        private static GameObject CreateAnimatedProjectileView(string label, Sprite[] frames, float scale, int sortOrder, Color tint, float fps, bool loop = true)
        {
            var go = new GameObject($"[View] {label}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];            // avoid a 1-frame placeholder flash before Start()
            sr.sortingOrder = sortOrder;
            sr.color = tint;
            go.transform.localScale = Vector3.one * scale;

            var anim = go.AddComponent<SpriteAnimator>();
            anim.frames = frames;
            anim.fps = fps;
            anim.loop = loop;
            anim.playOnStart = true;
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

        // Loads an animated frame sequence (<basePath>_01, _02, ...) from
        // Resources, stopping at the first missing frame. Returns null (cached)
        // when no "_01" frame exists, signalling the caller to use a static sprite.
        private Sprite[] LoadFramesFromResources(string basePath)
        {
            if (_frameCache.TryGetValue(basePath, out var cached))
                return cached;

            var frames = new List<Sprite>(8);
            for (int n = 1; ; n++)
            {
                var s = Resources.Load<Sprite>($"{basePath}_{n:D2}");
                if (s == null) break;
                frames.Add(s);
            }

            var arr = frames.Count > 0 ? frames.ToArray() : null;
            _frameCache[basePath] = arr;
            return arr;
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

        // Orients the scythe head so its blade points outward (away from the
        // player, toward where enemies are) and its ring stays on the player
        // side, then spans the chain from the player up to that ring so the
        // chain is always physically attached to the head's ring. The per-link
        // layout — count and randomly-mixed variant per slot — is in WhipChainRenderer.
        private void UpdateWhipChain(GameObject chainGo, GameObject headGo)
        {
            var playerRenderer = GetAnyPlayerRenderer();
            var chainRenderer = chainGo.GetComponent<WhipChainRenderer>();
            if (playerRenderer == null || chainRenderer == null)
            {
                chainGo.SetActive(false);
                return;
            }

            Vector3 playerPos = playerRenderer.transform.position; playerPos.z = 0f;
            Vector3 headPos = headGo.transform.position; headPos.z = 0f;
            Vector3 delta = headPos - playerPos;
            float dist = delta.magnitude;
            if (dist < 0.05f) { chainGo.SetActive(false); return; }

            Vector3 dir = delta / dist; // player → head = outward / enemy direction

            // Blade faces outward. The sprite is drawn blade-up (+Y), so rotate
            // its +Y axis onto `dir` (angle(dir) - 90°). The ring (sprite -Y)
            // then automatically points back along the chain toward the player.
            float headAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            headGo.transform.rotation = Quaternion.Euler(0f, 0f, headAngle);

            // Chain ends at the ring, which sits HeadRingOffset world units back
            // from the head center along the chain — so the ring is always linked.
            Vector3 ringPos = headPos - dir * HeadRingOffset;

            chainGo.SetActive(true);
            chainRenderer.UpdateChain(playerPos, ringPos);
        }

        // Twinkling glint sprite pinned to the void-starfall comet head.
        private const string SparklePath = "NightDash/Art/Stage01/VFX/spr_vfx_sparkle";
        private static Sprite _sparkleSprite;

        // Pins a small twinkling 4-point glint to the comet's star head (lower-left
        // of the sprite, since the head leads the down-left fall after the art's
        // 180° rotation). VFXSparkle pulses its scale/alpha so it sparkles.
        private void AttachStarTwinkle(GameObject meteorGo)
        {
            var headSr = meteorGo.GetComponent<SpriteRenderer>();
            if (headSr == null) return;
            if (_sparkleSprite == null) _sparkleSprite = Resources.Load<Sprite>(SparklePath);
            if (_sparkleSprite == null) return;

            var sparkGo = new GameObject("[VFX] StarTwinkle");
            sparkGo.transform.SetParent(meteorGo.transform, false);
            if (headSr.sprite != null)
            {
                Bounds b = headSr.sprite.bounds;
                sparkGo.transform.localPosition = new Vector3(b.min.x + b.size.x * 0.28f, b.min.y + b.size.y * 0.24f, 0f);
            }
            sparkGo.transform.localScale = Vector3.one * 0.6f;

            var ssr = sparkGo.AddComponent<SpriteRenderer>();
            ssr.sprite = _sparkleSprite;
            ssr.sortingOrder = headSr.sortingOrder + 1;
            ssr.color = new Color(0.85f, 0.95f, 1f, 1f); // icy tint matching the comet head
            sparkGo.AddComponent<VFXSparkle>();
        }

        // Loads the chain-link variant sprites — evolution set
        // (spr_vfx_evolution_chain_scythe_chain_1..N) for evolved scythes,
        // otherwise the base set (spr_vfx_chain_scythe_chain_1..N).
        private Sprite[] LoadChainVariants(bool evolved)
        {
            if (evolved && _chainVariantsEvolved != null) return _chainVariantsEvolved;
            if (!evolved && _chainVariants != null) return _chainVariants;

            string prefix = evolved ? ChainVariantEvolvedPrefix : ChainVariantPrefix;
            var list = new List<Sprite>(8);
            for (int n = 1; ; n++)
            {
                var s = Resources.Load<Sprite>($"{prefix}{n}");
                if (s == null) break;
                list.Add(s);
            }
            var arr = list.Count > 0 ? list.ToArray() : null;
            // Evolution art is optional — fall back to the base links if absent.
            if (arr == null && evolved) arr = LoadChainVariants(false);

            if (evolved) _chainVariantsEvolved = arr;
            else _chainVariants = arr;
            return arr;
        }

        private void DestroyAllWhipChains()
        {
            foreach (var kvp in _whipChains)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _whipChains.Clear();
        }

        // Draws/repositions a translucent dotted range ring (+ weapon icon) around
        // the player for each owned melee weapon in RangeIndicatorWeapons. Built
        // once per weapon, then just repositioned each frame.
        private void SyncWeaponRangeIndicators()
        {
            var playerRenderer = GetAnyPlayerRenderer();
            if (playerRenderer == null || _ownedWeaponQuery.IsEmptyIgnoreFilter)
            {
                foreach (var kvp in _rangeIndicators)
                    if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
                return;
            }

            var em = _world.EntityManager;
            // OwnedWeaponElement lives on the progression singleton (not the player).
            Entity ownerEntity = _ownedWeaponQuery.GetSingletonEntity();
            if (!em.HasBuffer<OwnedWeaponElement>(ownerEntity))
            {
                foreach (var kvp in _rangeIndicators)
                    if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
                return;
            }
            DynamicBuffer<OwnedWeaponElement> owned = em.GetBuffer<OwnedWeaponElement>(ownerEntity, true);

            Vector3 center = playerRenderer.transform.position;
            var registry = DataRegistry.Instance;

            for (int t = 0; t < RangeIndicatorWeapons.Length; t++)
            {
                string id = RangeIndicatorWeapons[t];

                // Match by BASE id so the guide stays after evolution
                // (weapon_slash_combo_evolved still maps to weapon_slash_combo).
                bool ownedNow = false;
                for (int i = 0; i < owned.Length; i++)
                {
                    string ownedId = owned[i].Id.ToString();
                    if (ownedId.EndsWith("_evolved")) ownedId = ownedId.Substring(0, ownedId.Length - "_evolved".Length);
                    else if (ownedId.EndsWith("_abyss")) ownedId = ownedId.Substring(0, ownedId.Length - "_abyss".Length);
                    if (ownedId == id) { ownedNow = true; break; }
                }

                if (!ownedNow)
                {
                    if (_rangeIndicators.TryGetValue(id, out var hidden) && hidden != null)
                        hidden.gameObject.SetActive(false);
                    continue;
                }

                if (!_rangeIndicators.TryGetValue(id, out var indicator) || indicator == null)
                {
                    // Approximated from baseRange — matches WeaponSystem's max(2.5, range)
                    // shape but does not reflect RangeMultiplier / level scaling.
                    float radius = 2.5f;
                    if (registry != null && registry.TryGetWeapon(id, out var wd) && wd != null)
                        radius = math.max(2.5f, wd.baseRange);

                    var go = new GameObject($"[VFX] RangeIndicator {id}");
                    indicator = go.AddComponent<WeaponRangeIndicator>();
                    Sprite icon = LoadSpriteFromResources(
                        $"NightDash/UI/Icons/Weapons/nd_ui_icon_weapon_{StripWeaponPrefix(id)}_default");
                    indicator.Init(GetRangeDotSprite(), icon, radius, RangeRingColor(id),
                        new Color(1f, 1f, 1f, 0.55f), SortRangeIndicator);
                    _rangeIndicators[id] = indicator;
                }

                indicator.gameObject.SetActive(true);
                indicator.SetCenter(center);
            }
        }

        private static string StripWeaponPrefix(string id)
            => id.StartsWith("weapon_") ? id.Substring("weapon_".Length) : id;

        private static Color RangeRingColor(string id)
            => id.Contains("hell_hammer")
                ? new Color(1.0f, 0.45f, 0.35f, 0.28f)  // ember red
                : new Color(1.0f, 0.88f, 0.45f, 0.28f); // slash gold

        // 1×1 white sprite reused for every ring dot.
        private static Sprite GetRangeDotSprite()
        {
            if (_rangeDotSprite != null) return _rangeDotSprite;
            // Dedicated 1×1 white texture (Texture2D.whiteTexture is read-only and can
            // throw "not readable" inside Sprite.Create on some platforms/builds).
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // PPU 1 so the 1×1 texture maps to a 1-world-unit sprite; DotScale then
            // controls the dot size directly in world units.
            _rangeDotSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _rangeDotSprite;
        }

        private void DestroyAllRangeIndicators()
        {
            foreach (var kvp in _rangeIndicators)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _rangeIndicators.Clear();
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
                bool wasMeteor = _meteorEntities.Remove(key);
                if (views.TryGetValue(key, out var go) && go != null)
                {
                    // Meteor reached lifetime end (landed) → icy sparkle burst at impact.
                    if (wasMeteor) SpawnMeteorLandingBurst(go.transform.position);
                    Destroy(go);
                }
                views.Remove(key);

                // Tear down the matching whip chain, if any.
                if (_whipChains.TryGetValue(key, out var chain))
                {
                    if (chain != null) Destroy(chain);
                    _whipChains.Remove(key);
                }
            }
        }

        // Bursts a small ring of icy sparkles at a landed meteor's impact point.
        private void SpawnMeteorLandingBurst(Vector3 pos)
        {
            if (_sparkleSprite == null) _sparkleSprite = Resources.Load<Sprite>(SparklePath);
            if (_sparkleSprite == null) return;
            pos.z = 0f;

            // Central expanding flash.
            var core = new GameObject("[VFX] MeteorBurst");
            core.transform.position = pos;
            var csr = core.AddComponent<SpriteRenderer>();
            csr.sprite = _sparkleSprite;
            csr.sortingOrder = SortProjectile + 2;
            csr.color = new Color(0.85f, 0.95f, 1f, 1f);
            core.AddComponent<VFXAutoDestroy>().Init(0.3f, fadeOut: true, fadeOutRatio: 1f, maxAlpha: 1f, scaleFrom: 0.35f, scaleTo: 1.2f);

            // Ring of imploding glints around it.
            const int n = 6;
            var icy = new Color(0.7f, 0.9f, 1f, 1f);
            for (int i = 0; i < n; i++)
            {
                float ang = (2f * Mathf.PI / n) * i;
                var g = new GameObject("[VFX] MeteorBurst");
                g.transform.position = pos + new Vector3(Mathf.Cos(ang) * 0.45f, Mathf.Sin(ang) * 0.45f, 0f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = _sparkleSprite;
                sr.sortingOrder = SortProjectile + 2;
                sr.color = icy;
                g.AddComponent<VFXAutoDestroy>().Init(0.35f, fadeOut: true, fadeOutRatio: 1f, maxAlpha: 1f, scaleFrom: 0.55f, scaleTo: 0.12f);
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

    // Leaves a fading sprite afterimage behind a moving VFX (e.g. the shadow
    // arrow's trail). Every `interval` it clones the current sprite at the
    // current pose with a low alpha that fades out via VFXAutoDestroy.
    public sealed class VFXAfterimage : MonoBehaviour
    {
        public float interval = 0.04f;
        public float fadeDuration = 0.22f;
        public float startAlpha = 0.5f;

        private SpriteRenderer _sr;
        private float _timer;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (_sr == null || _sr.sprite == null) return;
            _timer += Time.deltaTime;
            if (_timer < interval) return;
            _timer = 0f;

            var ghost = new GameObject("[VFX] Afterimage");
            var t = transform;
            ghost.transform.SetPositionAndRotation(t.position, t.rotation);
            ghost.transform.localScale = t.lossyScale;

            var gsr = ghost.AddComponent<SpriteRenderer>();
            gsr.sprite = _sr.sprite;
            gsr.flipX = _sr.flipX;
            gsr.flipY = _sr.flipY;
            gsr.sortingOrder = _sr.sortingOrder - 1;
            var col = _sr.color;
            col.a = startAlpha;
            gsr.color = col;

            ghost.AddComponent<VFXAutoDestroy>().Init(fadeDuration, fadeOut: true, fadeOutRatio: 1f, maxAlpha: startAlpha);
        }
    }

    // Twinkles a glint sprite by pulsing its scale + alpha (and slowly spinning),
    // so the star head reads as sparkling. Phase is offset per-instance so multiple
    // meteors don't twinkle in lockstep.
    public sealed class VFXSparkle : MonoBehaviour
    {
        public float speed = 9f;       // pulse rate
        public float minScale = 0.35f; // local-scale floor (relative to set localScale)
        public float maxScale = 1.0f;
        public float spinDegPerSec = 80f;

        private SpriteRenderer _sr;
        private float _t;
        private float _phase;
        private float _baseScale;
        private float _baseAlpha;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _phase = (GetInstanceID() & 15) * 0.41f; // de-sync instances
            _baseScale = transform.localScale.x;
            _baseAlpha = _sr != null ? _sr.color.a : 1f;
        }

        private void LateUpdate()
        {
            if (_sr == null) return;
            _t += Time.deltaTime * speed;
            // Sharp-ish peaks so it glints rather than slowly throbs.
            float s01 = Mathf.Pow(Mathf.Sin(_t + _phase) * 0.5f + 0.5f, 2f);
            float scale = _baseScale * Mathf.Lerp(minScale, maxScale, s01);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.Rotate(0f, 0f, spinDegPerSec * Time.deltaTime);
            var c = _sr.color;
            c.a = _baseAlpha * Mathf.Lerp(0.3f, 1f, s01);
            _sr.color = c;
        }
    }

    // Renders the chain-scythe chain as a row of individual link sprites laid
    // from the player to the flying scythe head. Each link slot is assigned a
    // RANDOM variant (stable per slot, so it doesn't flicker) and the link
    // count grows/shrinks with the distance — the chain "extends" with mixed
    // links instead of a single stretched/tiled sprite.
    public sealed class WhipChainRenderer : MonoBehaviour
    {
        private const float LinkSpacing = 0.18f; // world units between link centers
        private const float LinkScale   = 1.8f;  // smaller than the scythe head
        private const int   MaxLinks    = 96;

        private Sprite[] _variants;
        private int _sortingOrder;
        private Color _tint = Color.white;
        private uint _seed = 1u;
        // NaN = links rotate to follow the chain direction (base). A value pins every
        // link to that fixed world angle instead (evolution links stand vertical).
        private float _linkAngleDeg = float.NaN;
        private readonly System.Collections.Generic.List<SpriteRenderer> _links = new();

        public void Init(Sprite[] variants, int sortingOrder, Color tint, float linkAngleDeg = float.NaN)
        {
            _variants = variants;
            _sortingOrder = sortingOrder;
            _tint = tint;
            _linkAngleDeg = linkAngleDeg;
            _seed = (uint)(GetInstanceID() & 0x7fffffff) | 1u;
        }

        public void UpdateChain(Vector3 playerPos, Vector3 endPos)
        {
            if (_variants == null || _variants.Length == 0) { SetActiveCount(0); return; }

            playerPos.z = 0f;
            endPos.z = 0f;
            Vector3 delta = endPos - playerPos;
            float dist = delta.magnitude;
            if (dist < 0.05f) { SetActiveCount(0); return; }

            Vector3 dir = delta / dist;
            // Links follow the chain by default; a fixed angle pins them upright.
            var rot = float.IsNaN(_linkAngleDeg)
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
                : Quaternion.Euler(0f, 0f, _linkAngleDeg);
            int n = Mathf.Clamp(Mathf.RoundToInt(dist / LinkSpacing), 1, MaxLinks);

            while (_links.Count < n)
            {
                var go = new GameObject("link");
                go.transform.SetParent(transform, false);
                _links.Add(go.AddComponent<SpriteRenderer>());
            }

            for (int i = 0; i < n; i++)
            {
                SpriteRenderer lr = _links[i];
                Transform t = lr.transform;
                t.position = playerPos + dir * ((i + 0.5f) * LinkSpacing);
                t.rotation = rot;
                t.localScale = Vector3.one * LinkScale;
                uint h = _seed + (uint)i * 2654435761u; // stable pseudo-random variant per slot
                lr.sprite = _variants[(int)(h % (uint)_variants.Length)];
                lr.color = _tint;
                lr.sortingOrder = _sortingOrder;
            }
            SetActiveCount(n);
        }

        private void SetActiveCount(int n)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                bool on = i < n;
                if (_links[i].gameObject.activeSelf != on) _links[i].gameObject.SetActive(on);
            }
        }
    }

    // Draws a faint dotted ring at a melee weapon's max range around the player,
    // with the weapon's icon pinned at the top of the ring. Pure readability aid —
    // kept translucent and sorted below characters so it never obscures gameplay.
    // The ring geometry is built once in Init(); SetCenter() just follows the player.
    public sealed class WeaponRangeIndicator : MonoBehaviour
    {
        private const int   DotCount  = 48;
        private const float DotScale  = 0.12f; // dot diameter in world units (PPU-1 dot sprite)
        private const float IconScale = 0.8f;

        public void Init(Sprite dot, Sprite icon, float radius, Color ringColor, Color iconColor, int sortingOrder)
        {
            if (transform.childCount > 0) return; // already built; Init is one-shot
            for (int i = 0; i < DotCount; i++)
            {
                float ang = (2f * Mathf.PI * i) / DotCount;
                var go = new GameObject("dot");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
                go.transform.localScale = Vector3.one * DotScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dot;
                sr.color = ringColor;
                sr.sortingOrder = sortingOrder;
            }

            if (icon != null)
            {
                var iconGo = new GameObject("icon");
                iconGo.transform.SetParent(transform, false);
                iconGo.transform.localPosition = new Vector3(0f, radius, 0f);
                iconGo.transform.localScale = Vector3.one * IconScale;
                var isr = iconGo.AddComponent<SpriteRenderer>();
                isr.sprite = icon;
                isr.color = iconColor;
                isr.sortingOrder = sortingOrder + 1;
            }
        }

        public void SetCenter(Vector3 center)
        {
            center.z = 0f;
            transform.position = center;
        }
    }
}
