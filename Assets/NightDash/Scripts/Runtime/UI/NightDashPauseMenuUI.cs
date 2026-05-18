// Sprint B / M3 — Pause Menu screen.
// Auto-created at startup as inactive. Activated by NightDashGameplayPauseController
// when ESC is pressed during the Playing context. Owns the pause invariant
// (Time.timeScale=0 + GameplayPauseTag in the ECS world) and four menu actions:
// Resume, Settings (placeholder), Return to Lobby (RunTeardownBridge — Phase 4),
// Quit. Mirrors NightDashTitleScreenUI / NightDashLobbyScreenUI patterns:
//   - Direct Keyboard.current polling (avoids InputSystemUIInputModule defaults)
//   - unscaledDeltaTime animation (works while timeScale=0)
//   - OnDisable does NOT restore Time.timeScale unconditionally — Resume vs
//     Return-to-Lobby differ on whether the next screen owns the freeze policy.

using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightDash.ECS.Components;
using NightDash.Runtime;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashPauseMenuUI : MonoBehaviour
    {
        // Higher than Lobby (4500) and Title (5000) so the pause overlay
        // always covers any game UI underneath.
        private const int PauseCanvasSortingOrder = 6000;
        private const float BackdropAlpha = 0.62f;

        private const string ButtonResumeLabel = "Resume";
        private const string ButtonSettingsLabel = "Settings";
        private const string ButtonReturnLobbyLabel = "Return to Lobby";
        private const string ButtonReturnTitleLabel = "Return to Title";
        private const string ButtonQuitLabel = "Quit";

        // All five buttons are now interactable — Settings opens the volume
        // modal (NightDashSettingsModal) instead of being grayed out.
        private const int ButtonCount = 5;
        private const int IdxResume = 0;
        private const int IdxSettings = 1;
        private const int IdxReturnLobby = 2;
        private const int IdxReturnTitle = 3;
        private const int IdxQuit = 4;
        private static readonly bool[] ButtonEnabled = { true, true, true, true, true };

        private Canvas _canvas;
        private GameObject _backdrop;
        private readonly GameObject[] _buttonObjects = new GameObject[ButtonCount];
        private readonly Text[] _buttonLabels = new Text[ButtonCount];
        private int _selectedIndex;

        // Set true when a menu action explicitly hands off ownership of
        // Time.timeScale to the next screen (Return-to-Lobby case). When
        // true, OnDisable will NOT restore timeScale — letting the next
        // screen apply its own policy.
        private bool _handsOffTimeScale;

        // Confirmation dialog state. Destructive actions (Return to Lobby,
        // Return to Title, Quit) prompt the user with Yes/No before
        // committing. Enter = Yes, Esc = No.
        private GameObject _confirmOverlay;
        private Text _confirmMessageLabel;
        private bool _confirmActive;
        private System.Action _confirmAction;

        // Button frame 4-state sprites loaded once at Awake. Selection state
        // swaps Image.sprite (default → hover for selected, → disabled for
        // ButtonEnabled=false). pressed sprite is unused right now since
        // keyboard menus don't have a held-down state.
        private Sprite _buttonSpriteDefault;
        private Sprite _buttonSpriteHover;
        private Sprite _buttonSpriteDisabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashPauseMenuUI>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("NightDashPauseMenuUI");
            go.AddComponent<NightDashPauseMenuUI>();
            go.SetActive(false); // Hidden until ESC during Playing.
        }

        private void Awake()
        {
            EnsureEventSystem();
            LoadButtonSprites();
            BuildCanvas();
        }

        private void LoadButtonSprites()
        {
            _buttonSpriteDefault  = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_default");
            _buttonSpriteHover    = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_hover");
            _buttonSpriteDisabled = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_disabled");
        }

        private void OnEnable()
        {
            // Take ownership of input + screen + simulation pause.
            NightDashInputContextStack.Push(NightDashInputContext.Pause);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Paused);
            Time.timeScale = 0f;
            EnsurePauseTag();

            _handsOffTimeScale = false;
            HideConfirm(); // Reset any leftover dialog state from a prior session.
            SelectIndex(FirstEnabledIndex());
        }

        private void OnDisable()
        {
            // Always release the pause invariants we set on enable. Even on
            // hand-off paths, the next screen will (re)apply its own policy.
            NightDashInputContextStack.Pop(NightDashInputContext.Pause);
            RemovePauseTag();

            // Resume path restores 1x; hand-off path leaves timeScale=0 and
            // lets the next screen (Lobby) decide. Title/Lobby OnEnable both
            // set their own policy explicitly.
            if (!_handsOffTimeScale)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (NightDashInputContextStack.Top != NightDashInputContext.Pause) return;

            // When the Settings modal is open it handles ESC itself. Bail
            // out here so the same keypress doesn't double-close the modal
            // *and* resume the game in the same frame.
            if (NightDash.Runtime.UI.NightDashSettingsModal.IsOpen) return;

            ReadKeyboard(out bool up, out bool down, out bool confirm, out bool cancel);

            if (_confirmActive)
            {
                // Yes/No prompt — only confirm/cancel are meaningful.
                if (confirm)
                {
                    var action = _confirmAction;
                    HideConfirm();
                    action?.Invoke();
                }
                else if (cancel)
                {
                    HideConfirm();
                }
                return;
            }

            if (up) SelectIndex(StepIndex(_selectedIndex, -1));
            else if (down) SelectIndex(StepIndex(_selectedIndex, +1));
            else if (confirm) ClickSelected();
            else if (cancel) Resume();
        }

        // ====================================================================
        // Menu actions
        // ====================================================================

        private void ClickSelected()
        {
            switch (_selectedIndex)
            {
                case IdxResume: Resume(); break;
                case IdxSettings: NightDash.Runtime.UI.NightDashSettingsModal.Show(); break;
                case IdxReturnLobby:
                    ShowConfirm("Return to Lobby?\nCurrent run will be discarded.", ReturnToLobby);
                    break;
                case IdxReturnTitle:
                    ShowConfirm("Return to Title?\nCurrent run will be discarded.", ReturnToTitle);
                    break;
                case IdxQuit:
                    ShowConfirm("Quit the game?", Quit);
                    break;
            }
        }

        private void Resume()
        {
            // SetActive(false) cascades into OnDisable which restores everything.
            gameObject.SetActive(false);
        }

        private void ReturnToLobby()
        {
            // Hand off Time.timeScale ownership to Lobby. OnDisable will
            // skip the 1x restore so Lobby.OnEnable's timeScale=0 sticks.
            _handsOffTimeScale = true;

            // Sweep run-spawned entities, reset GameLoopState, clear pending
            // navigation, drop pooled views. Persistent singletons survive.
            RunTeardownBridge.DestroyCurrentRun();

            // Explicit context cleanup. Pop Pause AND Playing — we're
            // abandoning the gameplay session entirely. Without the Playing
            // pop, the stack would leak [Playing, Lobby] and the next ESC
            // during gameplay would not even fire (already-in-Pause guard
            // would think we're nested).
            NightDashInputContextStack.Pop(NightDashInputContext.Pause);
            NightDashInputContextStack.Pop(NightDashInputContext.Playing);

            var lobby = FindFirstObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include);
            if (lobby != null) lobby.gameObject.SetActive(true);
            // Lobby.OnEnable now pushes Lobby and applies timeScale=0.

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            gameObject.SetActive(false);
            // OnDisable's Pop(Pause) silently no-ops because Top is Lobby.
        }

        private void ReturnToTitle()
        {
            // Same hand-off contract as ReturnToLobby — full teardown +
            // explicit context unwind — but the next screen is Title.
            _handsOffTimeScale = true;

            RunTeardownBridge.DestroyCurrentRun();

            NightDashInputContextStack.Pop(NightDashInputContext.Pause);
            NightDashInputContextStack.Pop(NightDashInputContext.Playing);

            var title = FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (title != null) title.gameObject.SetActive(true);
            // Title.OnEnable pushes Title and applies its own timeScale=0
            // policy + HideGameplayViews.

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);
            gameObject.SetActive(false);
        }

        // ====================================================================
        // Confirmation dialog
        // ====================================================================

        private void ShowConfirm(string message, System.Action onYes)
        {
            _confirmActive = true;
            _confirmAction = onYes;
            if (_confirmMessageLabel != null) _confirmMessageLabel.text = message;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(true);
        }

        private void HideConfirm()
        {
            _confirmActive = false;
            _confirmAction = null;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ====================================================================
        // ECS pause-tag helpers
        // ====================================================================

        private static void EnsurePauseTag()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayPauseTag>());
            if (!query.IsEmpty) return; // Idempotent.
            var e = em.CreateEntity();
            em.AddComponent<GameplayPauseTag>(e);
        }

        private static void RemovePauseTag()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayPauseTag>());
            if (query.IsEmpty) return;
            em.DestroyEntity(query);
        }

        // ====================================================================
        // Input
        // ====================================================================

        private static void ReadKeyboard(out bool up, out bool down, out bool confirm, out bool cancel)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
            {
                up = down = confirm = cancel = false;
                return;
            }
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
            cancel = kb.escapeKey.wasPressedThisFrame;
#else
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
            cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private int StepIndex(int from, int direction)
        {
            int n = _buttonObjects.Length;
            int idx = from;
            for (int i = 0; i < n; i++)
            {
                idx = (idx + direction + n) % n;
                if (ButtonEnabled[idx]) return idx;
            }
            return from; // No enabled button found (shouldn't happen).
        }

        private static int FirstEnabledIndex()
        {
            for (int i = 0; i < ButtonEnabled.Length; i++)
            {
                if (ButtonEnabled[i]) return i;
            }
            return 0;
        }

        // ====================================================================
        // Canvas construction
        // ====================================================================

        private void BuildCanvas()
        {
            // Root canvas — overlay so it floats above all gameplay rendering.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = PauseCanvasSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            BuildBackdrop();
            BuildTitleLabel();
            BuildButtons();
            BuildConfirmOverlay();
        }

        private void BuildBackdrop()
        {
            _backdrop = new GameObject("Backdrop");
            var rect = _backdrop.AddComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = _backdrop.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, BackdropAlpha);
            img.raycastTarget = true; // Block clicks to gameplay underneath.
        }

        private void BuildTitleLabel()
        {
            var go = new GameObject("PausedTitle");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(transform, false);
            // Anchored at 0.85 so PAUSED breathes in the upper third while
            // the 3×-scaled 5-button stack centers comfortably below.
            rect.anchorMin = new Vector2(0.5f, 0.85f);
            rect.anchorMax = new Vector2(0.5f, 0.85f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 120f);
            rect.anchoredPosition = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = "PAUSED";
            text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            // Silver native is 16pt. Multiples of 16 stay pixel-perfect
            // under Hinted Raster — 128 = 8× native for the title.
            text.fontSize = 128;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.92f, 0.78f, 1f); // Warm parchment tone.
            text.raycastTarget = false;
        }

        private void BuildButtons()
        {
            string[] labels =
            {
                ButtonResumeLabel,
                ButtonSettingsLabel,
                ButtonReturnLobbyLabel,
                ButtonReturnTitleLabel,
                ButtonQuitLabel,
            };

            // Uniform 3× scale of the alpha-trimmed 101×37 sprite — keeps
            // pixels sharp while leaving plenty of vertical room for the
            // PAUSED title above and a comfortable 5-button stack overall:
            //   303 × 111  =  101 × 37  ×  3
            //   5 * 111 + 4 * 16 = 619 < 1080
            const float buttonWidth = 303f;
            const float buttonHeight = 111f;
            const float buttonSpacing = 16f;
            const float stackY = -50f; // shift down a touch to balance below PAUSED

            int n = labels.Length;
            float totalHeight = n * buttonHeight + (n - 1) * buttonSpacing;
            float startY = stackY + totalHeight * 0.5f - buttonHeight * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Button_{labels[i].Replace(" ", "")}");
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                rect.anchoredPosition = new Vector2(0f, startY - i * (buttonHeight + buttonSpacing));

                var bg = go.AddComponent<Image>();
                bg.sprite = _buttonSpriteDefault;
                // Simple + preserveAspect = uniform integer-scale of the
                // sprite. RectTransform is set to an exact multiple of the
                // sprite's native 101×37 so every source pixel maps to N
                // RectTransform pixels — corners and center scale together,
                // no warping, no blur.
                bg.type = Image.Type.Simple;
                bg.preserveAspect = true;
                bg.color = Color.white;
                bg.raycastTarget = false; // Keyboard-only menu.

                var labelGo = new GameObject("Label");
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                // Silver ascent > descent → MiddleCenter pulls glyphs upward.
                // Trim the rect's top edge so labels sit on the frame's
                // optical center, matching the Title menu treatment.
                labelRect.offsetMax = new Vector2(0f, -10f);

                var text = labelGo.AddComponent<Text>();
                text.text = labels[i];
                text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
                // Silver native 16pt → 48 = 3× native, comfortable read on
                // the 303×111 button frame.
                text.fontSize = 48;
                text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;

                _buttonObjects[i] = go;
                _buttonLabels[i] = text;
            }
        }

        private void BuildConfirmOverlay()
        {
            // Full-screen dim that swallows the menu visually + a centered
            // message panel with a Yes/No hint. Starts inactive; ShowConfirm
            // populates the message and toggles it on.
            var root = new GameObject("ConfirmOverlay");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;

            var msgGo = new GameObject("Message");
            var msgRect = msgGo.AddComponent<RectTransform>();
            msgRect.SetParent(rootRect, false);
            msgRect.anchorMin = new Vector2(0.5f, 0.5f);
            msgRect.anchorMax = new Vector2(0.5f, 0.5f);
            msgRect.pivot = new Vector2(0.5f, 0.5f);
            msgRect.sizeDelta = new Vector2(900f, 220f);
            msgRect.anchoredPosition = new Vector2(0f, 30f);

            var msgText = msgGo.AddComponent<Text>();
            msgText.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            msgText.fontSize = 64;
            msgText.fontStyle = FontStyle.Bold;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.color = new Color(1f, 0.92f, 0.78f, 1f);
            msgText.raycastTarget = false;
            msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
            msgText.verticalOverflow = VerticalWrapMode.Overflow;

            // Hint row layout (left → right):
            //   [confirm 40x40]  YES (ENTER)            NO (ESC)  [cancel 40x40]
            // Each glyph cluster takes its own anchored slot inside the 1000px
            // hint rect; the gap between Yes and No is wide enough that the
            // text from one side cannot crawl over the icon from the other.
            var hintGo = new GameObject("Hint");
            var hintRect = hintGo.AddComponent<RectTransform>();
            hintRect.SetParent(rootRect, false);
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(800f, 60f);
            hintRect.anchoredPosition = new Vector2(0f, -120f);

            // Yes group sits centered in the left half of the hint rect.
            //   icon center at x = -270, label rect spans -260..-30 → group
            //   center ≈ -150 .. which lands the (icon + label) cluster at
            //   the optical middle of the left half (-200). Mirror values
            //   below build the No side symmetrically.
            NightDash.Runtime.UI.NightDashUIIcons.Attach(
                hintRect,
                NightDash.Runtime.UI.NightDashUIIcons.Confirm,
                new Vector2(40f, 40f),
                new Vector2(-270f, -3f));

            var yesGo = new GameObject("YesLabel");
            var yesRect = yesGo.AddComponent<RectTransform>();
            yesRect.SetParent(hintRect, false);
            yesRect.anchorMin = new Vector2(0f, 0f);
            yesRect.anchorMax = new Vector2(0.5f, 1f);
            // offsetMin pushes the label's left edge further in so it never
            // crowds the confirm glyph at -270 (icon right ≈ -250).
            yesRect.offsetMin = new Vector2(170f, 0f);
            // Drop the top edge another 2px — the Silver pixel font's glyph
            // cluster needs a little extra downward bias to line up with the
            // icon's optical center.
            yesRect.offsetMax = new Vector2(-30f, -6f);
            var yesText = yesGo.AddComponent<Text>();
            yesText.text = "YES (ENTER)";
            yesText.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            yesText.fontSize = 36;
            yesText.alignment = TextAnchor.MiddleLeft;
            yesText.color = new Color(0.85f, 0.92f, 0.76f, 1f);
            yesText.raycastTarget = false;

            // No group: mirror of Yes (cancel icon left, label right) so the
            // whole cluster lands at the optical middle of the right half.
            NightDash.Runtime.UI.NightDashUIIcons.Attach(
                hintRect,
                NightDash.Runtime.UI.NightDashUIIcons.Cancel,
                new Vector2(40f, 40f),
                new Vector2(110f, -3f));

            var noGo = new GameObject("NoLabel");
            var noRect = noGo.AddComponent<RectTransform>();
            noRect.SetParent(hintRect, false);
            noRect.anchorMin = new Vector2(0.5f, 0f);
            noRect.anchorMax = new Vector2(1f, 1f);
            noRect.offsetMin = new Vector2(170f, 0f);
            noRect.offsetMax = new Vector2(-30f, -6f);
            var noText = noGo.AddComponent<Text>();
            noText.text = "NO (ESC)";
            noText.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            noText.fontSize = 36;
            noText.alignment = TextAnchor.MiddleLeft;
            noText.color = new Color(0.92f, 0.78f, 0.74f, 1f);
            noText.raycastTarget = false;

            _confirmOverlay = root;
            _confirmMessageLabel = msgText;
            root.SetActive(false);
        }

        private void SelectIndex(int idx)
        {
            _selectedIndex = idx;
            for (int i = 0; i < _buttonObjects.Length; i++)
            {
                if (_buttonObjects[i] == null || _buttonLabels[i] == null) continue;
                bool enabled = ButtonEnabled[i];
                bool selected = (i == _selectedIndex);

                var bg = _buttonObjects[i].GetComponent<Image>();
                if (bg != null)
                {
                    Sprite next;
                    if (!enabled) next = _buttonSpriteDisabled;
                    else if (selected) next = _buttonSpriteHover;
                    else next = _buttonSpriteDefault;
                    if (next != null) bg.sprite = next;
                    bg.color = Color.white;
                }

                // Label color stays subtle — the frame sprite already
                // communicates state. Selected gets warm parchment for legibility.
                if (!enabled)
                {
                    _buttonLabels[i].color = new Color(0.45f, 0.45f, 0.45f, 1f);
                }
                else if (selected)
                {
                    _buttonLabels[i].color = new Color(1f, 0.95f, 0.78f, 1f);
                }
                else
                {
                    _buttonLabels[i].color = new Color(0.85f, 0.82f, 0.74f, 1f);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
