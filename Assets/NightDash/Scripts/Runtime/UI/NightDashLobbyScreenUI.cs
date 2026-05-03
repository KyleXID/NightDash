// Sprint B / M2 Phase 1 — Canvas-based campfire lobby.
// Replaces RunSelectionLobbyUI's OnGUI flow with a uGUI screen that shows
// 7 character cards in a row, lit by a placeholder campfire (real campfire
// sprite + glow halo arrives in Phase 2). Arrow keys pick character, up/down
// pick stage, Enter starts the run, ESC returns to Title.
//
// ECS hand-off is preserved: stage/class selection still goes through
// RunSelectionLobbyWorldBridge.TryApplySelectionToCurrentWorld so the
// existing bootstrap/data systems keep working.

using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashLobbyScreenUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning
        private const int    CharacterCardCount = 7;
        private const float  CardHeight         = 360f;
        private const float  CardSpacing        = 40f;
        private const float  CardSelectedScale  = 1.12f;
        private const float  CardUnselectedScale = 0.92f;
        private const float  IdleFps            = 6f;

        private static readonly Color CardSelectedTint   = Color.white;
        private static readonly Color CardUnselectedTint = new Color(0.32f, 0.30f, 0.42f, 1f);

        // ------------------------------------------------------------------ state
        private RunSelectionLobbyUI _legacyLobbyUi; // adapter target
        private NightDashDebugVisualBridge _visualBridge;
        private Canvas _canvas;
        private GameObject _backgroundLayer;
        private GameObject _campfirePlaceholder;

        private readonly List<CharacterCard> _cards = new();
        private readonly List<string> _stageIds = new();
        private int _classIndex;
        private int _stageIndex;
        private bool _initialized;
        private float _animTime;

        private struct CharacterCard
        {
            public string ClassId;
            public RectTransform Rect;
            public Image Image;
            public Text Label;
            public AnimationClipDef IdleClip;
        }

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashLobbyScreenUI");
            go.AddComponent<NightDashLobbyScreenUI>();
            // Hidden by default; Title.OnStartClicked enables this screen.
            go.SetActive(false);
        }

        private void Awake()
        {
            _legacyLobbyUi = FindFirstObjectByType<RunSelectionLobbyUI>(FindObjectsInactive.Include);
            BuildCanvas();
            BuildCards();
            CollectStageIds();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;

            NightDashInputContextStack.Push(NightDashInputContext.Lobby);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);

            // Keep gameplay frozen and views hidden — selection screen, not play.
            Time.timeScale = 0f;
            HideGameplayViews();

            // Hard-disable the legacy OnGUI lobby so it cannot draw on top of us.
            if (_legacyLobbyUi != null)
            {
                _legacyLobbyUi.SetLobbyVisible(false);
                _legacyLobbyUi.enabled = false;
            }

            // Restore previous selection (PlayerPrefs-backed).
            RunSelectionSession.GetCurrent(out string savedStage, out string savedClass);
            _classIndex = FindClassIndex(savedClass);
            _stageIndex = FindStageIndex(savedStage);
            ApplySelectionVisuals();
            _animTime = 0f;
        }

        private void OnDisable()
        {
            NightDashInputContextStack.Pop(NightDashInputContext.Lobby);
        }

        // ====================================================================
        // Canvas + visuals
        // ====================================================================
        private void BuildCanvas()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 4500; // below Title (5000)

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Wipe any previously-built children so re-Awake stays clean.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildBackground();
            BuildCampfirePlaceholder();
            BuildHelpText();
        }

        private void BuildBackground()
        {
            var bgRect = CreateRect("Background", transform);
            StretchFull(bgRect);
            var bgImage = bgRect.gameObject.AddComponent<RawImage>();
            var tex = Resources.Load<Texture2D>("NightDash/UI/Lobby/lobby_campfire_background");
            if (tex != null)
            {
                bgImage.texture = tex;
                bgImage.color = Color.white;
            }
            else
            {
                // Fallback dim violet until the asset lands.
                bgImage.color = new Color(0.06f, 0.04f, 0.12f, 1f);
            }
            bgImage.raycastTarget = false;
            _backgroundLayer = bgRect.gameObject;
        }

        private void BuildCampfirePlaceholder()
        {
            // Phase 1 placeholder: a small warm dot in screen center where the
            // animated campfire sprite will land in Phase 2.
            var rect = CreateRect("CampfirePlaceholder", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 160f);
            rect.anchoredPosition = new Vector2(0f, -120f);

            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.55f, 0.18f, 0.85f);
            img.raycastTarget = false;
            _campfirePlaceholder = rect.gameObject;
        }

        private void BuildCards()
        {
            _cards.Clear();

            var registry = DataRegistry.Instance;
            if (registry == null || registry.Catalog == null) return;
            var classes = registry.Catalog.classes;
            if (classes == null || classes.Count == 0) return;

            int cardCount = Mathf.Min(CharacterCardCount, classes.Count);
            float totalWidth = cardCount * CardHeight * 0.55f + (cardCount - 1) * CardSpacing;
            float startX = -totalWidth * 0.5f;
            float cardWidth = CardHeight * 0.55f;

            for (int i = 0; i < cardCount; i++)
            {
                var classData = classes[i];
                if (classData == null || string.IsNullOrEmpty(classData.id)) continue;

                var rect = CreateRect($"Card_{classData.id}", transform);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cardWidth, CardHeight);
                rect.anchoredPosition = new Vector2(startX + i * (cardWidth + CardSpacing) + cardWidth * 0.5f, 80f);

                var image = rect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;

                AnimationClipDef idleClip = null;
                if (registry.TryGetAnimationSet(classData.id, out var animSet) && animSet != null)
                {
                    idleClip = animSet.GetClipOrFallback("Idle", "Walk");
                    if (idleClip != null && idleClip.FrameCount > 0)
                    {
                        var sprite = idleClip.frames[0];
                        if (sprite != null) image.sprite = sprite;
                    }
                }

                // Class name label below the card.
                var labelRect = CreateRect("Label", rect);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -8f);
                labelRect.sizeDelta = new Vector2(0f, 32f);

                var label = labelRect.gameObject.AddComponent<Text>();
                label.text = string.IsNullOrEmpty(classData.displayName) ? classData.id : classData.displayName;
                label.alignment = TextAnchor.UpperCenter;
                label.fontSize = 22;
                label.color = Color.white;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.raycastTarget = false;

                _cards.Add(new CharacterCard
                {
                    ClassId = classData.id,
                    Rect = rect,
                    Image = image,
                    Label = label,
                    IdleClip = idleClip,
                });
            }
        }

        private void BuildHelpText()
        {
            var rect = CreateRect("HelpText", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(900f, 36f);

            var t = rect.gameObject.AddComponent<Text>();
            t.text = "← →  CHARACTER     ↑ ↓  STAGE     ENTER  START     ESC  BACK";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 22;
            t.color = new Color(1f, 1f, 1f, 0.78f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
        }

        // ====================================================================
        // Per-frame animation + input
        // ====================================================================
        private void Update()
        {
            if (NightDashInputContextStack.Top != NightDashInputContext.Lobby) return;

            _animTime += Time.unscaledDeltaTime;
            TickCardIdle();

            ReadInput(out bool left, out bool right, out bool up, out bool down, out bool confirm, out bool cancel);

            if (left)       { MoveClass(-1); }
            else if (right) { MoveClass(+1); }
            else if (up)    { MoveStage(-1); }
            else if (down)  { MoveStage(+1); }
            else if (confirm) { StartRun(); }
            else if (cancel)  { BackToTitle(); }
        }

        private void TickCardIdle()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IdleClip == null || card.IdleClip.FrameCount == 0 || card.Image == null) continue;
                var sprite = card.IdleClip.GetFrameAt(_animTime);
                if (sprite != null) card.Image.sprite = sprite;
            }
        }

        private static void ReadInput(out bool left, out bool right, out bool up, out bool down, out bool confirm, out bool cancel)
        {
            left = right = up = down = confirm = cancel = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
            cancel = kb.escapeKey.wasPressedThisFrame;
#else
            left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
            cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ====================================================================
        // Selection
        // ====================================================================
        private void MoveClass(int delta)
        {
            if (_cards.Count == 0) return;
            int n = _cards.Count;
            _classIndex = ((_classIndex + delta) % n + n) % n;
            ApplySelectionVisuals();
        }

        private void MoveStage(int delta)
        {
            if (_stageIds.Count == 0) return;
            int n = _stageIds.Count;
            _stageIndex = ((_stageIndex + delta) % n + n) % n;
        }

        private void ApplySelectionVisuals()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.Image == null || card.Rect == null) continue;
                bool selected = i == _classIndex;
                card.Image.color = selected ? CardSelectedTint : CardUnselectedTint;
                if (card.Label != null)
                {
                    var c = card.Label.color;
                    c.a = selected ? 1f : 0.5f;
                    card.Label.color = c;
                }
                float scale = selected ? CardSelectedScale : CardUnselectedScale;
                card.Rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // ====================================================================
        // Stage list
        // ====================================================================
        private void CollectStageIds()
        {
            _stageIds.Clear();
            var registry = DataRegistry.Instance;
            if (registry == null || registry.Catalog == null) return;
            var stages = registry.Catalog.stages;
            if (stages == null) return;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && !string.IsNullOrEmpty(stages[i].id))
                {
                    _stageIds.Add(stages[i].id);
                }
            }
            if (_stageIds.Count == 0) _stageIds.Add("stage_01");
        }

        private int FindClassIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].ClassId == id) return i;
            return 0;
        }

        private int FindStageIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < _stageIds.Count; i++)
                if (_stageIds[i] == id) return i;
            return 0;
        }

        // ====================================================================
        // Actions
        // ====================================================================
        private void StartRun()
        {
            if (_cards.Count == 0 || _stageIds.Count == 0) return;
            string classId = _cards[Mathf.Clamp(_classIndex, 0, _cards.Count - 1)].ClassId;
            string stageId = _stageIds[Mathf.Clamp(_stageIndex, 0, _stageIds.Count - 1)];

            RunSelectionSession.SetCurrent(stageId, classId);
            bool applied = RunSelectionLobbyWorldBridge.TryApplySelectionToCurrentWorld(stageId, classId);
            NightDashLog.Info($"[NightDash] Lobby Start: stage='{stageId}', class='{classId}', applied={applied}.");

            // Resume sim, restore gameplay views, hide self.
            Time.timeScale = 1f;
            RestoreGameplayViews();
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Playing);
            gameObject.SetActive(false);
        }

        private void BackToTitle()
        {
            // Title screen handles its own pause + view-hide on enable.
            var title = FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (title != null) title.gameObject.SetActive(true);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);
            gameObject.SetActive(false);
        }

        // ====================================================================
        // Helpers
        // ====================================================================
        private void HideGameplayViews()
        {
            if (_visualBridge == null)
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (_visualBridge != null)
            {
                _visualBridge.DestroyAllViewsImmediate();
                _visualBridge.enabled = false;
            }
        }

        private void RestoreGameplayViews()
        {
            if (_visualBridge == null)
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (_visualBridge != null) _visualBridge.enabled = true;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
