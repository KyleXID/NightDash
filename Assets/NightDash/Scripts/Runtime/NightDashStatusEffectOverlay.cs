// Floats a small icon stack above every entity with an active StatusEffectState.
// Mirrors NightDashHealthBarOverlay's pool pattern — single Canvas, per-entity
// container reused, world→screen each frame. Burn / Poison / Freeze / Stun
// icons render side-by-side so the player can read "this guy is burning AND
// frozen" at a glance.

using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashStatusEffectOverlay : MonoBehaviour
    {
        // Sits with the floating HUD overlays — above gameplay but below
        // modal stacks (Settings 6500, Pause 6000, etc).
        private const int SortOrder = 810;

        // Icon stack metrics — tuned so a fully-loaded enemy (4 effects) is
        // ~24px wide without overlapping the HP bar above the head.
        private const float IconSize = 18f;
        private const float IconSpacing = 2f;
        private const float WorldOffsetY = 1.55f;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashStatusEffectOverlay>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashStatusEffectOverlay");
            go.AddComponent<NightDashStatusEffectOverlay>();
        }

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _cameraCache;
        private World _queryWorld;
        private EntityQuery _statusQuery;

        // Cached sprite references — Resources load only once.
        private Sprite _burnSprite;
        private Sprite _freezeSprite;
        private Sprite _poisonSprite;
        private Sprite _stunSprite;

        private readonly Dictionary<Entity, EntityStack> _entityToStack = new();
        private readonly HashSet<Entity> _aliveThisFrame = new();
        private readonly List<Entity> _toRemove = new();
        private readonly Stack<EntityStack> _stackPool = new();

        private class EntityStack
        {
            public GameObject Container;
            public RectTransform Rect;
            public Image[] Icons = new Image[4];
        }

        private void Awake()
        {
            BuildCanvas();
            LoadSprites();
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)transform;
        }

        private void LoadSprites()
        {
            _burnSprite   = Resources.Load<Sprite>("NightDash/UI/Icons/Status/nd_ui_icon_status_burn_default");
            _freezeSprite = Resources.Load<Sprite>("NightDash/UI/Icons/Status/nd_ui_icon_status_freeze_default");
            _poisonSprite = Resources.Load<Sprite>("NightDash/UI/Icons/Status/nd_ui_icon_status_poison_default");
            _stunSprite   = Resources.Load<Sprite>("NightDash/UI/Icons/Status/nd_ui_icon_status_stun_default");
        }

        private void LateUpdate()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { HideAll(); return; }
            EnsureQueriesFor(world, world.EntityManager);
            Camera cam = ResolveCamera();
            if (cam == null) { HideAll(); return; }

            EntityManager em = world.EntityManager;
            _aliveThisFrame.Clear();

            using var entities = _statusQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var states = _statusQuery.ToComponentDataArray<StatusEffectState>(Unity.Collections.Allocator.Temp);
            using var xforms = _statusQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                // Defensive: a structural change earlier this frame can
                // leave a stale query reference for one tick.
                if (!em.Exists(entity)) continue;
                StatusEffectState s = states[i];
                if (s.ActiveMask == 0) continue;

                _aliveThisFrame.Add(entity);
                if (!_entityToStack.TryGetValue(entity, out EntityStack stack))
                {
                    stack = AcquireStack();
                    _entityToStack[entity] = stack;
                }

                // Position above entity.
                Vector3 worldPos = xforms[i].Position + new float3(0f, WorldOffsetY, 0f);
                Vector3 screen = cam.WorldToScreenPoint(worldPos);
                if (screen.z <= 0f)
                {
                    stack.Container.SetActive(false);
                    continue;
                }
                stack.Container.SetActive(true);
                stack.Rect.anchoredPosition = ScreenToCanvas(screen);

                // Light up the 4 icons individually.
                bool burn   = (s.ActiveMask & StatusEffectBits.Burn)   != 0;
                bool freeze = (s.ActiveMask & StatusEffectBits.Freeze) != 0;
                bool poison = (s.ActiveMask & StatusEffectBits.Poison) != 0;
                bool stun   = (s.ActiveMask & StatusEffectBits.Stun)   != 0;

                int activeCount = (burn?1:0) + (freeze?1:0) + (poison?1:0) + (stun?1:0);
                float totalWidth = activeCount * IconSize + math.max(0, activeCount - 1) * IconSpacing;
                float x = -totalWidth * 0.5f + IconSize * 0.5f;

                SetIcon(stack.Icons[0], burn,   _burnSprite,   ref x);
                SetIcon(stack.Icons[1], poison, _poisonSprite, ref x);
                SetIcon(stack.Icons[2], freeze, _freezeSprite, ref x);
                SetIcon(stack.Icons[3], stun,   _stunSprite,   ref x);
            }

            // Sweep entities that lost their effect / despawned.
            _toRemove.Clear();
            foreach (var kv in _entityToStack)
            {
                if (!_aliveThisFrame.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                if (_entityToStack.TryGetValue(_toRemove[i], out EntityStack s))
                {
                    ReleaseStack(s);
                    _entityToStack.Remove(_toRemove[i]);
                }
            }
        }

        private void SetIcon(Image img, bool active, Sprite sprite, ref float x)
        {
            if (img == null) return;
            img.enabled = active && sprite != null;
            if (active)
            {
                img.sprite = sprite;
                img.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += IconSize + IconSpacing;
            }
        }

        private Vector2 ScreenToCanvas(Vector3 screen)
        {
            float scaleX = _canvasRect.rect.width > 0f
                ? _canvasRect.rect.width / Screen.width : 1f;
            float scaleY = _canvasRect.rect.height > 0f
                ? _canvasRect.rect.height / Screen.height : 1f;
            return new Vector2(
                (screen.x - Screen.width * 0.5f) * scaleX,
                (screen.y - Screen.height * 0.5f) * scaleY);
        }

        private EntityStack AcquireStack()
        {
            if (_stackPool.Count > 0)
            {
                var s = _stackPool.Pop();
                s.Container.SetActive(true);
                return s;
            }
            return CreateStack();
        }

        private void ReleaseStack(EntityStack s)
        {
            if (s == null || s.Container == null) return;
            s.Container.SetActive(false);
            _stackPool.Push(s);
        }

        private EntityStack CreateStack()
        {
            var root = new GameObject("StatusStack", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize * 4 + IconSpacing * 3, IconSize);

            var s = new EntityStack { Container = root, Rect = rect };
            for (int i = 0; i < 4; i++)
            {
                var iconGo = new GameObject($"Icon{i}", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(rect, false);
                var ir = (RectTransform)iconGo.transform;
                ir.anchorMin = new Vector2(0.5f, 0.5f);
                ir.anchorMax = new Vector2(0.5f, 0.5f);
                ir.pivot = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = new Vector2(IconSize, IconSize);
                var img = iconGo.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = false;
                s.Icons[i] = img;
            }
            return s;
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _statusQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<StatusEffectState>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        private void OnDestroy()
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _statusQuery.Dispose();
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
            foreach (var kv in _entityToStack)
            {
                ReleaseStack(kv.Value);
            }
            _entityToStack.Clear();
        }
    }
}
