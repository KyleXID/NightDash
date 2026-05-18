// Mock interactable prompt.
//
// Until real chests / altars / NPCs are authored as ECS entities this bridge
// fakes a single interactable at a fixed offset from the player so the
// `interact` / `interact_key` icons have somewhere to live. When the player
// walks within range a world-space icon hovers over the dummy spot and a
// "[E] INTERACT" hint surfaces near the bottom of the HUD. Pressing E hides
// the prompt for ~3 seconds (mock cooldown) — there is no gameplay effect.

using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashInteractPromptMock : MonoBehaviour
    {
        // World-space offset from the player at spawn time — picked to be
        // close enough that a casual stroll touches it but far enough that
        // it doesn't sit on top of the player out of the gate.
        private static readonly Vector2 SpawnOffsetFromPlayer = new(6f, 4f);
        private const float InteractRange = 2.2f;
        private const float CooldownAfterPress = 3f;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashInteractPromptMock>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashInteractPromptMock");
            go.AddComponent<NightDashInteractPromptMock>();
        }

        private Canvas _canvas;
        private CanvasGroup _worldIconGroup;
        private RectTransform _worldIconRect;
        private CanvasGroup _hintGroup;
        private Camera _cameraCache;
        private Vector3 _dummyPos;
        private bool _dummyAnchored;
        private float _cooldownTimer;
        // Cached query — saves a per-frame archetype rebuild.
        private World _queryWorld;
        private EntityQuery _playerQuery;

        private void Awake()
        {
            BuildCanvas();
            BuildWorldIcon();
            BuildHint();
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the HUD's LevelUp canvas (950) so every foreground modal
            // (Pause / Settings / Collection / Inventory / etc.) naturally
            // renders on top of the floating interact icon and prompt.
            _canvas.sortingOrder = 820;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildWorldIcon()
        {
            var go = new GameObject("InteractMarker",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            _worldIconRect = (RectTransform)go.transform;
            _worldIconRect.anchorMin = new Vector2(0f, 0f);
            _worldIconRect.anchorMax = new Vector2(0f, 0f);
            _worldIconRect.pivot = new Vector2(0.5f, 0.5f);
            _worldIconRect.sizeDelta = new Vector2(56f, 56f);
            _worldIconGroup = go.GetComponent<CanvasGroup>();
            _worldIconGroup.alpha = 0f;
            var img = go.GetComponent<Image>();
            img.sprite = NightDash.Runtime.UI.NightDashUIIcons.Get(
                NightDash.Runtime.UI.NightDashUIIcons.Interact);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private void BuildHint()
        {
            var go = new GameObject("InteractHint",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 180f);
            r.sizeDelta = new Vector2(420f, 76f);
            _hintGroup = go.GetComponent<CanvasGroup>();
            _hintGroup.alpha = 0f;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.10f, 0.92f);
            bg.raycastTarget = false;

            // Interact-key glyph on the left.
            NightDash.Runtime.UI.NightDashUIIcons.Attach(
                r,
                NightDash.Runtime.UI.NightDashUIIcons.InteractKey,
                new Vector2(48f, 48f),
                new Vector2(-150f, 0f));

            // Label.
            var labelGo = new GameObject("Label",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(r, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(110f, 0f);
            lr.offsetMax = new Vector2(-20f, 0f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = "[E] INTERACT";
            lt.alignment = TextAnchor.MiddleLeft;
            lt.fontSize = 32;
            lt.color = new Color(0.96f, 0.92f, 0.80f, 1f);
            lt.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.95f);
            lo.effectDistance = new Vector2(2f, -2f);
        }

        private void Update()
        {
            // No modal/context hides here — sortingOrder = 820 keeps the
            // floating finger icon below every popup canvas (LevelUp 950,
            // Pause 6000, Settings 6500, etc.) so the prompt visually goes
            // behind them without being torn down.
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { Hide(); return; }
            EntityManager em = world.EntityManager;
            EnsureQueriesFor(world, em);

            if (!TryGetPlayerPosition(em, out float3 playerPos)) { Hide(); return; }

            // Anchor the dummy interactable on the first frame we see the
            // player so the spawn position is stable from then on.
            if (!_dummyAnchored)
            {
                _dummyPos = new Vector3(
                    playerPos.x + SpawnOffsetFromPlayer.x,
                    playerPos.y + SpawnOffsetFromPlayer.y,
                    0f);
                _dummyAnchored = true;
            }

            float distance = Vector2.Distance(
                new Vector2(playerPos.x, playerPos.y),
                new Vector2(_dummyPos.x, _dummyPos.y));

            Camera cam = ResolveCamera();
            if (cam == null) { Hide(); return; }

            // World icon: always visible while on screen (so the player can
            // see where the interactable is even from a distance).
            Vector3 screen = cam.WorldToScreenPoint(_dummyPos + new Vector3(0f, 0.6f, 0f));
            bool onScreen = screen.z > 0f
                && screen.x >= 0f && screen.x <= Screen.width
                && screen.y >= 0f && screen.y <= Screen.height;
            if (onScreen)
            {
                _worldIconRect.position = screen;
                _worldIconGroup.alpha = 1f;
            }
            else
            {
                _worldIconGroup.alpha = 0f;
            }

            // Hint: visible only when the player is in range AND not on
            // cooldown from a recent press.
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.unscaledDeltaTime;
                _hintGroup.alpha = 0f;
                return;
            }

            bool inRange = distance <= InteractRange;
            _hintGroup.alpha = inRange ? 1f : 0f;
            if (inRange && InteractKeyPressed())
            {
                _cooldownTimer = CooldownAfterPress;
                _hintGroup.alpha = 0f;
            }
        }

        private static bool InteractKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private bool TryGetPlayerPosition(EntityManager em, out float3 pos)
        {
            pos = default;
            if (_playerQuery.IsEmptyIgnoreFilter) return false;
            var arr = _playerQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            if (arr.Length == 0) { arr.Dispose(); return false; }
            pos = arr[0].Position;
            arr.Dispose();
            return true;
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        private void OnDestroy()
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _playerQuery.Dispose();
            }
        }

        private void Hide()
        {
            if (_worldIconGroup != null) _worldIconGroup.alpha = 0f;
            if (_hintGroup != null) _hintGroup.alpha = 0f;
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
