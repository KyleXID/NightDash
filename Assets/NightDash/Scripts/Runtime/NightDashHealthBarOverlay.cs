// World-anchored mini health bars for the player + every enemy that has
// taken at least one hit. Bars sit above the entity in screen space, follow
// their owner each frame, and fade out a couple of seconds after the entity
// stops taking damage (or hides entirely once the entity is back at full).
//
// Two independent toggles in the Settings modal:
//   - Health Bars  → enemy bars (everything that isn't tagged Player)
//   - Player Bar   → the player's own bar; renders TWO rows when the player
//                    has a shield (top = shield, bottom = HP), single HP row
//                    once shield drops to zero.
//
// Hidden entirely while any foreground modal is up (Settings / Collection /
// SaveSlot / Pause / Inventory / LevelUp via RunStatus check) so the bars
// don't draw on top of UI surfaces that should own the screen.

using System.Collections.Generic;
using NightDash.ECS.Components;
using NightDash.Runtime.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashHealthBarOverlay : MonoBehaviour
    {
        // PlayerPrefs keys (also referenced by NightDashSettingsModal).
        public const string PrefEnemyKey  = "nightdash.ui.healthbars";
        public const string PrefPlayerKey = "nightdash.ui.playerbar";

        private const int DefaultEnabled = 1; // both default ON

        // Bar dimensions — kept compact so a 40-enemy crowd doesn't drown the
        // screen. Player bar is slightly wider so the shield overlay reads.
        private const float EnemyBarWidth = 64f;
        private const float EnemyBarHeight = 8f;
        private const float PlayerBarWidth = 80f;
        private const float PlayerBarHeight = 10f;
        private const float WorldOffsetY = 1.1f;
        private const float HideAfterFullSec = 1.5f;

        // Below the HUD's LevelUp canvas (950) so any pause / level-up /
        // modal surface naturally renders over the bars without an extra
        // hide step. The Update guards below handle the cases where Unity's
        // sort order alone isn't enough (e.g. world-space cameras).
        private const int SortOrder = 800;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _canvasRect;
        private Camera _cameraCache;
        private World _queryWorld;
        private EntityQuery _combatQuery;

        private readonly List<Bar> _pool = new();
        private readonly Dictionary<Entity, int> _entityToBar = new();
        private readonly HashSet<Entity> _aliveThisFrame = new();
        private readonly List<Entity> _toRemove = new();

        private struct Bar
        {
            public GameObject Go;
            public RectTransform Rect;
            public Image Backdrop;
            public Image Fill;        // HP fill (single bar) or HP row in two-row
            public Image ShieldFill;   // null for enemy bars; overlay on HP
            public bool InUse;
            public bool IsPlayer;
            public float LastHp;
            public float LastShield;
            public float LastHitTime;
        }

        public static bool EnemyBarsEnabled
        {
            get => PlayerPrefs.GetInt(PrefEnemyKey, DefaultEnabled) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefEnemyKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool PlayerBarEnabled
        {
            get => PlayerPrefs.GetInt(PrefPlayerKey, DefaultEnabled) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefPlayerKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // Backwards-compat for older settings code that referenced a single
        // 'Enabled' flag — now maps to the enemy toggle.
        public static bool Enabled
        {
            get => EnemyBarsEnabled;
            set => EnemyBarsEnabled = value;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashHealthBarOverlay>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashHealthBarOverlay");
            go.AddComponent<NightDashHealthBarOverlay>();
        }

        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortOrder;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            // Bars stay alive across modals — their canvas sits at
            // SortOrder = 800, which is below the LevelUp / Pause /
            // Settings / Collection / SaveSlot / Inventory canvases
            // (all 900+), so those overlays naturally render on top
            // without us tearing the bar bindings down.
            bool enemyOn = EnemyBarsEnabled;
            bool playerOn = PlayerBarEnabled;
            if (!enemyOn && !playerOn) { HideAll(); return; }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { HideAll(); return; }
            EntityManager em = world.EntityManager;
            EnsureQueriesFor(world, em);

            Camera cam = ResolveCamera();
            if (cam == null) { HideAll(); return; }

            _aliveThisFrame.Clear();

            using var entities = _combatQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var stats = _combatQuery.ToComponentDataArray<CombatStats>(Unity.Collections.Allocator.Temp);
            using var xforms = _combatQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            float now = Time.unscaledTime;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                CombatStats cs = stats[i];
                if (cs.MaxHealth <= 0f) continue;

                bool isPlayer = em.HasComponent<PlayerTag>(entity);

                // Per-tag toggle — skip wholesale when the relevant flag is off.
                if (isPlayer && !playerOn) continue;
                if (!isPlayer && !enemyOn) continue;

                float hpRatio = math.clamp(cs.CurrentHealth / cs.MaxHealth, 0f, 1f);
                bool hasShield = cs.MaxShield > 0f;
                float shieldRatio = hasShield
                    ? math.clamp(cs.CurrentShield / cs.MaxShield, 0f, 1f)
                    : 0f;

                _aliveThisFrame.Add(entity);

                int barIdx;
                bool fresh;
                if (!_entityToBar.TryGetValue(entity, out barIdx))
                {
                    // Skip allocating a bar for full-HP enemies that have
                    // never been hit. Player gets a bar as soon as they
                    // have anything below max (or any shield).
                    bool atFull = hpRatio >= 0.9999f && (!hasShield || shieldRatio >= 0.9999f);
                    if (atFull) continue;
                    barIdx = AcquireBar(isPlayer);
                    if (barIdx < 0) continue;
                    _entityToBar[entity] = barIdx;
                    fresh = true;
                }
                else
                {
                    fresh = false;
                    // If a player bar was created for an enemy slot earlier
                    // (or vice versa) the row layout would be wrong. Recycle
                    // and re-acquire so the visual matches the entity tag.
                    if (_pool[barIdx].IsPlayer != isPlayer)
                    {
                        ReleaseBar(entity, barIdx);
                        barIdx = AcquireBar(isPlayer);
                        if (barIdx < 0) continue;
                        _entityToBar[entity] = barIdx;
                        fresh = true;
                    }
                }

                Bar b = _pool[barIdx];
                // Hit detection — current resource lower than last frame.
                bool tookDamage =
                    !fresh &&
                    (hpRatio < b.LastHp - 0.0001f
                     || (hasShield && shieldRatio < b.LastShield - 0.0001f));
                if (tookDamage) b.LastHitTime = now;
                b.LastHp = hpRatio;
                b.LastShield = shieldRatio;
                _pool[barIdx] = b;

                // Auto-hide once back to full + cooldown elapsed.
                bool atFullNow = hpRatio >= 0.9999f && (!hasShield || shieldRatio >= 0.9999f);
                if (atFullNow && (now - b.LastHitTime) > HideAfterFullSec)
                {
                    ReleaseBar(entity, barIdx);
                    continue;
                }

                // World → screen position.
                Vector3 worldPos = xforms[i].Position + new float3(0f, WorldOffsetY, 0f);
                Vector3 screen = cam.WorldToScreenPoint(worldPos);
                if (screen.z <= 0f)
                {
                    b.Go.SetActive(false);
                    continue;
                }
                b.Go.SetActive(true);
                b.Rect.anchoredPosition = ScreenToCanvas(screen);

                // HP fill — solid color, no flash. Hit feedback was washing
                // the bar white and made the actual HP level unreadable
                // during sustained damage.
                b.Fill.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
                b.Fill.color = new Color(0.82f, 0.22f, 0.24f, 1f);

                // Shield fill — single overlay drawn ON TOP of the HP fill so
                // it visually "covers" the HP bar as long as the player has
                // shield left. When shield drops to zero the overlay hides
                // and the player sees just the red HP bar.
                if (b.IsPlayer && b.ShieldFill != null)
                {
                    bool showShield = hasShield && shieldRatio > 0.0001f;
                    b.ShieldFill.enabled = showShield;
                    if (showShield)
                    {
                        b.ShieldFill.rectTransform.anchorMax = new Vector2(shieldRatio, 1f);
                        b.ShieldFill.color = new Color(0.45f, 0.65f, 0.95f, 1f);
                    }
                }
            }

            // Sweep entities that disappeared since the previous frame.
            _toRemove.Clear();
            foreach (var kv in _entityToBar)
            {
                if (!_aliveThisFrame.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                if (_entityToBar.TryGetValue(_toRemove[i], out int idx))
                {
                    ReleaseBar(_toRemove[i], idx);
                }
            }
        }

        private Vector2 ScreenToCanvas(Vector3 screen)
        {
            float scaleX = _canvasRect.rect.width > 0f
                ? _canvasRect.rect.width / Screen.width
                : 1f;
            float scaleY = _canvasRect.rect.height > 0f
                ? _canvasRect.rect.height / Screen.height
                : 1f;
            return new Vector2(
                (screen.x - Screen.width * 0.5f) * scaleX,
                (screen.y - Screen.height * 0.5f) * scaleY);
        }

        // -------- pool helpers ---------------------------------------------
        private int AcquireBar(bool isPlayer)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].InUse && _pool[i].IsPlayer == isPlayer)
                {
                    var b = _pool[i];
                    b.InUse = true;
                    b.LastHitTime = Time.unscaledTime;
                    b.LastHp = 1f;
                    b.LastShield = 1f;
                    _pool[i] = b;
                    b.Go.SetActive(true);
                    return i;
                }
            }
            var newBar = CreateBar(isPlayer);
            newBar.InUse = true;
            newBar.LastHitTime = Time.unscaledTime;
            newBar.LastHp = 1f;
            newBar.LastShield = 1f;
            _pool.Add(newBar);
            return _pool.Count - 1;
        }

        private void ReleaseBar(Entity entity, int idx)
        {
            _entityToBar.Remove(entity);
            if (idx < 0 || idx >= _pool.Count) return;
            var b = _pool[idx];
            b.InUse = false;
            if (b.Go != null) b.Go.SetActive(false);
            _pool[idx] = b;
        }

        private Bar CreateBar(bool isPlayer)
        {
            float w = isPlayer ? PlayerBarWidth : EnemyBarWidth;
            float h = isPlayer ? PlayerBarHeight : EnemyBarHeight;

            // Single-row container — even the player bar is one strip now.
            // Shield, when present, draws ON TOP of the HP fill as a blue
            // overlay so the bar reads at a glance: full blue = full shield,
            // shield drains → red HP starts showing through.
            var root = new GameObject(isPlayer ? "PlayerBar" : "HealthBar",
                typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            var r = (RectTransform)root.transform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            var hpBg = root.GetComponent<Image>();
            hpBg.color = new Color(0f, 0f, 0f, 0.82f);
            hpBg.raycastTarget = false;

            // HP fill (red) — bottom layer.
            var hpFillGo = new GameObject("HpFill",
                typeof(RectTransform), typeof(Image));
            hpFillGo.transform.SetParent(r, false);
            var hpFr = (RectTransform)hpFillGo.transform;
            hpFr.anchorMin = new Vector2(0f, 0f);
            hpFr.anchorMax = new Vector2(1f, 1f);
            hpFr.offsetMin = new Vector2(1f, 1f);
            hpFr.offsetMax = new Vector2(-1f, -1f);
            var hpFill = hpFillGo.GetComponent<Image>();
            hpFill.color = new Color(0.82f, 0.22f, 0.24f, 1f);
            hpFill.raycastTarget = false;

            // Shield fill (blue) — overlay on top of the HP fill, only built
            // for the player bar. Same rect so the two ratios are directly
            // comparable; the shield image's anchorMax.x is set per-frame.
            Image shieldFill = null;
            if (isPlayer)
            {
                var sFillGo = new GameObject("ShieldFill",
                    typeof(RectTransform), typeof(Image));
                sFillGo.transform.SetParent(r, false);
                var sFr = (RectTransform)sFillGo.transform;
                sFr.anchorMin = new Vector2(0f, 0f);
                sFr.anchorMax = new Vector2(1f, 1f);
                sFr.offsetMin = new Vector2(1f, 1f);
                sFr.offsetMax = new Vector2(-1f, -1f);
                shieldFill = sFillGo.GetComponent<Image>();
                shieldFill.color = new Color(0.45f, 0.65f, 0.95f, 1f);
                shieldFill.raycastTarget = false;
                // Make sure ShieldFill renders after HpFill so it visually
                // overlays it (later sibling = on top in UGUI).
                sFillGo.transform.SetAsLastSibling();
            }

            root.SetActive(false);
            return new Bar
            {
                Go = root,
                Rect = r,
                Backdrop = hpBg,
                Fill = hpFill,
                ShieldFill = shieldFill,
                IsPlayer = isPlayer,
                InUse = false,
                LastHp = 1f,
                LastShield = 1f,
                LastHitTime = 0f,
            };
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _combatQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatStats>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        private void OnDestroy()
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _combatQuery.Dispose();
            }
        }

        private Camera ResolveCamera()
        {
            if (_cameraCache != null && _cameraCache.gameObject.activeInHierarchy) return _cameraCache;
            _cameraCache = Camera.main;
            return _cameraCache;
        }

        private void HideAll()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].Go != null) _pool[i].Go.SetActive(false);
                var b = _pool[i];
                b.InUse = false;
                _pool[i] = b;
            }
            _entityToBar.Clear();
        }
    }
}
