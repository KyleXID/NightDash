// HUD bridge for boss-related visual cues:
//   - "BOSS APPROACHING" warning toast on the 0→1 spawn transition (5s fade)
//   - Off-screen objective marker pointing at the boss while it lives
//
// Polls `BossSpawnState` and `BossTag` from the default ECS world each frame.
// The bridge is keyboard-of-input-free — it just listens.

using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashBossWarningToast : MonoBehaviour
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashBossWarningToast>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashBossWarningToast");
            go.AddComponent<NightDashBossWarningToast>();
        }

        private const float ToastDuration = 5f;
        private const float ToastFadeInSec = 0.4f;
        private const float ToastFadeOutSec = 0.8f;
        // Distance from the screen edge where the marker glues itself when
        // the boss is off-camera. Keeps the arrow from clipping the HUD.
        private const float EdgeInset = 64f;

        private Canvas _canvas;
        private GameObject _toastGo;
        private CanvasGroup _toastGroup;
        private RectTransform _markerRect;
        private Image _markerIcon;
        private CanvasGroup _markerGroup;

        private byte _lastSpawnedFlag;
        private float _toastTimer = -1f;
        private Camera _cameraCache;
        // Cached queries so Update doesn't allocate a new EntityQuery + tear
        // it down every frame. Tied to the lifetime of `_queryWorld` — if the
        // ECS world reloads we rebuild the cache lazily.
        private World _queryWorld;
        private EntityQuery _bossStateQuery;
        private EntityQuery _bossTransformQuery;

        private void Awake()
        {
            BuildCanvas();
            BuildToast();
            BuildMarker();
        }

        private void OnEnable()
        {
            // Reset the transition tracker so a fresh run sees the next
            // 0 → 1 spawn flip — even if the previous run ended with the
            // flag still latched to 1 on the singleton.
            _lastSpawnedFlag = 0;
            _toastTimer = -1f;
            HideAll();
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD (4500-ish) but below pause overlay (5500) so
            // a paused game doesn't see the toast pulse through the dim.
            _canvas.sortingOrder = 4700;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildToast()
        {
            _toastGo = new GameObject("BossWarningToast", typeof(RectTransform), typeof(CanvasGroup));
            _toastGo.transform.SetParent(transform, false);
            var r = (RectTransform)_toastGo.transform;
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -140f);
            r.sizeDelta = new Vector2(720f, 96f);

            _toastGroup = _toastGo.GetComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;

            // Backdrop bar.
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(r, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.38f, 0.08f, 0.10f, 0.92f);
            bgImg.raycastTarget = false;

            // Warning icon on the left.
            NightDash.Runtime.UI.NightDashUIIcons.Attach(
                r,
                NightDash.Runtime.UI.NightDashUIIcons.Warning,
                new Vector2(56f, 56f),
                new Vector2(-300f, 0f));

            // Label centered.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(r, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(110f, 0f);
            lr.offsetMax = new Vector2(-30f, 0f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = "BOSS APPROACHING";
            lt.alignment = TextAnchor.MiddleCenter;
            lt.fontSize = 44;
            lt.fontStyle = FontStyle.Bold;
            lt.color = new Color(0.98f, 0.92f, 0.86f, 1f);
            lt.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.95f);
            lo.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildMarker()
        {
            var go = new GameObject("BossObjectiveMarker",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            _markerRect = (RectTransform)go.transform;
            _markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _markerRect.pivot = new Vector2(0.5f, 0.5f);
            _markerRect.sizeDelta = new Vector2(48f, 48f);
            _markerRect.anchoredPosition = Vector2.zero;
            _markerGroup = go.GetComponent<CanvasGroup>();
            _markerGroup.alpha = 0f;

            _markerIcon = go.GetComponent<Image>();
            _markerIcon.sprite = NightDash.Runtime.UI.NightDashUIIcons.Get(
                NightDash.Runtime.UI.NightDashUIIcons.ObjectiveMarker);
            _markerIcon.preserveAspect = true;
            _markerIcon.raycastTarget = false;
            _markerIcon.color = new Color(0.95f, 0.36f, 0.36f, 1f);
        }

        private void Update()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { HideAll(); return; }
            EntityManager em = world.EntityManager;
            EnsureQueriesFor(world, em);

            byte spawnedFlag = 0;
            byte killedFlag = 0;
            if (!_bossStateQuery.IsEmptyIgnoreFilter)
            {
                var s = _bossStateQuery.GetSingleton<BossSpawnState>();
                spawnedFlag = s.HasSpawnedBoss;
                killedFlag = s.BossKilled;
            }

            // 0 → 1 transition fires the toast.
            if (spawnedFlag == 1 && _lastSpawnedFlag == 0)
            {
                _toastTimer = 0f;
            }
            _lastSpawnedFlag = spawnedFlag;

            TickToast();
            TickMarker(em, spawnedFlag, killedFlag);
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _bossStateQuery = em.CreateEntityQuery(ComponentType.ReadOnly<BossSpawnState>());
            _bossTransformQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<BossTag>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        private void OnDestroy()
        {
            // ECS queries hang off the world's job dependency manager; they
            // need explicit disposal to release the cached archetype handles.
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _bossStateQuery.Dispose();
                _bossTransformQuery.Dispose();
            }
        }

        private void TickToast()
        {
            if (_toastTimer < 0f)
            {
                _toastGroup.alpha = 0f;
                return;
            }
            _toastTimer += Time.unscaledDeltaTime;
            if (_toastTimer >= ToastDuration)
            {
                _toastTimer = -1f;
                _toastGroup.alpha = 0f;
                return;
            }
            float alpha;
            if (_toastTimer < ToastFadeInSec)
            {
                alpha = _toastTimer / ToastFadeInSec;
            }
            else if (_toastTimer > ToastDuration - ToastFadeOutSec)
            {
                alpha = (ToastDuration - _toastTimer) / ToastFadeOutSec;
            }
            else
            {
                // Soft pulse during the middle hold.
                float t = (_toastTimer - ToastFadeInSec) * 2f;
                alpha = 0.85f + 0.15f * Mathf.Sin(t * 6.28f);
            }
            _toastGroup.alpha = Mathf.Clamp01(alpha);
        }

        private void TickMarker(EntityManager em, byte spawned, byte killed)
        {
            bool active = spawned == 1 && killed == 0;
            if (!active)
            {
                _markerGroup.alpha = 0f;
                return;
            }
            if (!TryGetBossPosition(em, out float3 worldPos))
            {
                _markerGroup.alpha = 0f;
                return;
            }
            Camera cam = ResolveCamera();
            if (cam == null)
            {
                _markerGroup.alpha = 0f;
                return;
            }

            Vector3 viewport = cam.WorldToViewportPoint(worldPos);
            bool onScreen = viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;

            // On-screen → marker hidden (boss is visible, no need for a hint).
            if (onScreen)
            {
                _markerGroup.alpha = 0f;
                return;
            }

            // Off-screen → clamp to the edge of the canvas and rotate toward
            // the boss. WorldToViewportPoint returns negative z when behind
            // the camera; flip the vector so it points outward correctly.
            Vector2 v = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f) v = -v;
            if (v.sqrMagnitude < 1e-6f) v = Vector2.up;
            v.Normalize();

            // Canvas size — derive from the screen rect since CanvasScaler
            // matches it for us.
            var canvasRect = (RectTransform)_canvas.transform;
            Vector2 canvasSize = canvasRect.rect.size;
            float halfW = canvasSize.x * 0.5f - EdgeInset;
            float halfH = canvasSize.y * 0.5f - EdgeInset;
            float scale = Mathf.Min(halfW / Mathf.Abs(v.x + 1e-5f), halfH / Mathf.Abs(v.y + 1e-5f));
            Vector2 anchored = v * scale;

            _markerRect.anchoredPosition = anchored;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f; // arrow points up by default
            _markerRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            _markerGroup.alpha = 1f;
        }

        private bool TryGetBossPosition(EntityManager em, out float3 pos)
        {
            pos = default;
            if (_bossTransformQuery.IsEmptyIgnoreFilter) return false;
            var arr = _bossTransformQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            if (arr.Length == 0) { arr.Dispose(); return false; }
            pos = arr[0].Position;
            arr.Dispose();
            return true;
        }

        private void HideAll()
        {
            if (_toastGroup != null) _toastGroup.alpha = 0f;
            if (_markerGroup != null) _markerGroup.alpha = 0f;
        }

        private static EntityManager ResolveEntityManager()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return default;
            return world.EntityManager;
        }

        private Camera ResolveCamera()
        {
            if (_cameraCache != null && _cameraCache.gameObject.activeInHierarchy) return _cameraCache;
            _cameraCache = Camera.main;
            return _cameraCache;
        }
    }
}
