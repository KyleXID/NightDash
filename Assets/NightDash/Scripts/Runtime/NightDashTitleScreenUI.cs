using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using NightDash.Runtime.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime
{
    public sealed class NightDashTitleScreenUI : MonoBehaviour
    {
        [SerializeField] private Texture2D titleTexture;
        [SerializeField] private Texture2D logoTexture;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        // Sprint B M1: 4-button menu. Localization deferred to a future sprint;
        // strings inlined for now and routed through M0 ScreenRouter.
        private const string ButtonStartLabel = "Start";
        private const string ButtonContinueLabel = "Continue";
        private const string ButtonSettingsLabel = "Settings";
        private const string ButtonQuitLabel = "Quit";

        private RunSelectionLobbyUI _lobbyUi;
        private NightDashDebugVisualBridge _visualBridge;
        private GameObject _firstButton;
        private readonly GameObject[] _menuButtons = new GameObject[4];
        private readonly Text[] _menuButtonLabels = new Text[4];
        private readonly System.Action[] _menuButtonActions = new System.Action[4];
        private int _selectedIndex;

        // Button frame 4-state sprites loaded once at Awake. Same pattern as
        // PauseMenu — Resources.Load + 9-slice (Image.type = Sliced) so the
        // bronze trim corners stay pixel-perfect at any RectTransform size.
        private Sprite _buttonSpriteDefault;
        private Sprite _buttonSpriteHover;
        private Sprite _buttonSpriteDisabled;

        // Reveal animation: cascading fade + scale-up. Each button delays
        // start by RevealStagger * its slot, then eases over RevealDuration.
        // Driven by unscaledDeltaTime so the cinematic plays even with
        // Time.timeScale=0.
        private const float RevealDuration = 0.18f;
        private const float RevealStagger = 0.08f;
        private const float RevealStartScale = 0.85f;
        private float _revealAnimTime;

        // Two-state title: initial (PRESS START prompt) -> menu (4-button stack).
        private GameObject _pressStartText;
        private Text _pressStartLabel;
        private bool _menuRevealed;
        private float _pressStartPulseTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            var lobby = FindFirstObjectByType<RunSelectionLobbyUI>(FindObjectsInactive.Include);
            if (lobby == null)
            {
                return;
            }

            var go = new GameObject("NightDashTitleScreenUI");
            var ui = go.AddComponent<NightDashTitleScreenUI>();
            ui.SetTitleTexture(lobby.TitleTexture);
            ui.SetLogoTexture(lobby.TitleLogoTexture);
        }

        private void Awake()
        {
            _lobbyUi = FindFirstObjectByType<RunSelectionLobbyUI>();
            if (_lobbyUi != null)
            {
                _lobbyUi.SetLobbyVisible(false);
                // SetLobbyVisible only hides the lobby panel; the OnGUI loop
                // still draws the legacy title texture each frame and covers
                // our Canvas. Disabling the component stops OnGUI entirely
                // until Start is clicked.
                _lobbyUi.enabled = false;
            }

            // Resources are the latest authored assets and ALWAYS win when
            // present. SerializeField values (e.g. baked by
            // NightDashBootstrapSceneSetup) and lobby-UI fallback are used
            // only when Resources are missing.
            var resTitle = Resources.Load<Texture2D>("NightDash/UI/Title/title_screen_background");
            if (resTitle != null) titleTexture = resTitle;

            var resLogo = Resources.Load<Texture2D>("NightDash/UI/Title/nightdash_logo");
            if (resLogo != null) logoTexture = resLogo;

            if (_lobbyUi != null)
            {
                if (titleTexture == null) titleTexture = _lobbyUi.TitleTexture;
                if (logoTexture == null) logoTexture = _lobbyUi.TitleLogoTexture;
            }

            EnsureEventSystem();
            LoadButtonSprites();
            BuildCanvas();
        }

        private void OnEnable()
        {
            // Title becomes the input-routing context whenever the screen is shown.
            NightDashInputContextStack.Push(NightDashInputContext.Title);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);

            // Freeze gameplay while the title is visible. Without this,
            // EnemySpawn / Combat / Movement systems continue running and the
            // player can take damage from off-screen mobs before pressing
            // Start. Restored in OnStartClicked.
            Time.timeScale = 0f;

            // Always start in the PRESS START state. Menu only opens after the
            // player presses any confirm key. Keeps the central hero artwork
            // unobstructed on first impression.
            _menuRevealed = false;
            _pressStartPulseTime = 0f;
            SetMenuVisible(false);
            SetPressStartVisible(true);

            // Hide gameplay sprites entirely so PNG transparency areas don't
            // bleed game content through the title illustration.
            HideGameplayViews();
        }

        private void OnDisable()
        {
            NightDashInputContextStack.Pop(NightDashInputContext.Title);
            // Title intentionally does NOT restore Time.timeScale here.
            // The next screen (Lobby/Pause/etc.) owns the freeze policy:
            // restoring on disable would un-pause the game right after
            // Lobby's OnEnable just set timeScale=0.
        }

        private void HideGameplayViews()
        {
            if (_visualBridge == null)
            {
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            }
            if (_visualBridge != null)
            {
                _visualBridge.DestroyAllViewsImmediate();
                _visualBridge.enabled = false;
            }
        }

        private void RestoreGameplayViews()
        {
            if (_visualBridge == null)
            {
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            }
            if (_visualBridge != null)
            {
                _visualBridge.enabled = true;
            }
        }

        private void Update()
        {
            // Only this screen owns input while it is the topmost context.
            if (NightDashInputContextStack.Top != NightDashInputContext.Title) return;

            // Direct keyboard polling — avoids Update-order dependency on
            // NightDashUIInputRuntime and works whether or not Input System
            // UI Module's default actions are wired up.
            bool up, down, confirm, cancel;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
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

            if (!_menuRevealed)
            {
                // Initial state: pulse PRESS START, wait for any confirm.
                PulsePressStart();
                if (confirm) RevealMenu();
            }
            else
            {
                if (up) SelectIndex(_selectedIndex - 1);
                else if (down) SelectIndex(_selectedIndex + 1);
                else if (confirm) ClickSelected();
                else if (cancel) HideMenu();

                // Cascade reveal animation runs AFTER SelectIndex so the
                // animation's per-frame alpha/scale wins over SelectIndex's
                // alpha=1 reset. Self-clamps once all buttons settle.
                TickRevealAnimation();
            }
        }

        // Drives the per-frame fade + scale cascade after RevealMenu().
        // Each button's tween begins at slotIndex*Stagger seconds and finishes
        // RevealDuration seconds later, eased with cubic ease-out.
        private void TickRevealAnimation()
        {
            float total = RevealDuration + (_menuButtons.Length - 1) * RevealStagger;
            if (_revealAnimTime > total + 0.01f) return; // Already settled.

            _revealAnimTime += Time.unscaledDeltaTime;

            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var go = _menuButtons[i];
                if (go == null) continue;

                float delay = i * RevealStagger;
                float t = Mathf.Clamp01((_revealAnimTime - delay) / RevealDuration);
                // Ease-out cubic — quick start, soft landing.
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);

                float scale = Mathf.Lerp(RevealStartScale, 1f, ease);
                go.transform.localScale = new Vector3(scale, scale, 1f);

                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    var c = img.color;
                    c.a = ease;
                    img.color = c;
                }
                if (_menuButtonLabels[i] != null)
                {
                    var lc = _menuButtonLabels[i].color;
                    lc.a = ease;
                    _menuButtonLabels[i].color = lc;
                }
            }
        }

        private void PulsePressStart()
        {
            if (_pressStartLabel == null) return;
            // unscaledDeltaTime so the pulse keeps running while timeScale=0.
            _pressStartPulseTime += Time.unscaledDeltaTime;
            float a = 0.35f + 0.65f * Mathf.PingPong(_pressStartPulseTime * 1.4f, 1f);
            var c = _pressStartLabel.color;
            c.a = a;
            _pressStartLabel.color = c;
        }

        private void RevealMenu()
        {
            _menuRevealed = true;
            SetPressStartVisible(false);
            SetMenuVisible(true);
            // Apply hover sprite + label color to the first button. We don't
            // rely on EventSystem.SetSelectedGameObject anymore — keyboard
            // navigation goes through Update polling + SelectIndex direct
            // sprite swap (mirrors NightDashPauseMenuUI).
            SelectIndex(0);

            // Start the cascading fade+scale reveal. Pre-zero each button so
            // the first frame doesn't pop at full opacity before TickReveal
            // gets a chance to overwrite it.
            _revealAnimTime = 0f;
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var go = _menuButtons[i];
                if (go == null) continue;
                go.transform.localScale = new Vector3(RevealStartScale, RevealStartScale, 1f);
                var img = go.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = 0f; img.color = c; }
                if (_menuButtonLabels[i] != null) { var lc = _menuButtonLabels[i].color; lc.a = 0f; _menuButtonLabels[i].color = lc; }
            }
        }

        private void HideMenu()
        {
            _menuRevealed = false;
            SetMenuVisible(false);
            SetPressStartVisible(true);
            _pressStartPulseTime = 0f;
            // Park the reveal animation so a re-reveal restarts cleanly.
            _revealAnimTime = 0f;
        }

        private void SetMenuVisible(bool visible)
        {
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                if (_menuButtons[i] != null) _menuButtons[i].SetActive(visible);
            }
        }

        private void SetPressStartVisible(bool visible)
        {
            if (_pressStartText != null) _pressStartText.SetActive(visible);
        }

        private void SelectIndex(int next)
        {
            int len = _menuButtons.Length;
            if (len == 0) return;
            _selectedIndex = ((next % len) + len) % len;
            for (int i = 0; i < len; i++)
            {
                var go = _menuButtons[i];
                if (go == null) continue;
                bool selected = (i == _selectedIndex);

                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = selected ? _buttonSpriteHover : _buttonSpriteDefault;
                    img.color = Color.white;
                }
                if (_menuButtonLabels[i] != null)
                {
                    _menuButtonLabels[i].color = selected
                        ? new Color(1f, 0.95f, 0.78f, 1f)   // warm parchment when selected
                        : new Color(0.85f, 0.82f, 0.74f, 1f);
                }
            }
        }

        private void ClickSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _menuButtonActions.Length) return;
            var action = _menuButtonActions[_selectedIndex];
            action?.Invoke();
        }

        public void SetTitleTexture(Texture2D texture)
        {
            titleTexture = texture;
        }

        public void SetLogoTexture(Texture2D texture)
        {
            logoTexture = texture;
        }

        private void BuildCanvas()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Higher than NightDashDebugVisualBridge sprite renderers
            // (player baseSort 1000 + Y offset up to 200 = 1200) so Title
            // always covers the game world.
            canvas.sortingOrder = 5000;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            foreach (Transform child in gameObject.transform)
            {
                Destroy(child.gameObject);
            }

            // PNG is alpha-trimmed externally so we no longer need a base
            // layer. Cover-fit the trimmed illustration over the full screen.
            var bg = CreateRect("TitleBackground", gameObject.transform);
            var bgImage = bg.gameObject.AddComponent<RawImage>();
            bgImage.texture = titleTexture;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            StretchFull(bg);

            // Cover-fit the texture: keep native aspect ratio and trim the
            // longer axis so the rendered image fills the full screen
            // without horizontal/vertical squashing.
            ApplyCoverFitUv(bgImage);

            if (logoTexture != null)
            {
                // Logo: top-CENTER anchor, native aspect preserved. Hero sky
                // illustration leaves clear sky in the upper area for the logo.
                var logoRect = CreateRect("TitleLogo", gameObject.transform);
                logoRect.anchorMin = new Vector2(0.5f, 1f);
                logoRect.anchorMax = new Vector2(0.5f, 1f);
                logoRect.pivot = new Vector2(0.5f, 1f);
                logoRect.anchoredPosition = new Vector2(0f, -60f);

                const float logoHeight = 360f;
                float aspect = logoTexture.height > 0
                    ? (float)logoTexture.width / logoTexture.height
                    : 2f;
                logoRect.sizeDelta = new Vector2(logoHeight * aspect, logoHeight);

                var logoImage = logoRect.gameObject.AddComponent<RawImage>();
                logoImage.texture = logoTexture;
                logoImage.color = Color.white;
                logoImage.raycastTarget = false;
            }

            // PRESS START prompt — bottom-center, pulsed alpha. Visible only
            // before the player confirms; replaced by the 4-button menu.
            BuildPressStartLabel();

            // 4-button vertical stack. Absolute pixel offsets from screen
            // center keep layout predictable across CanvasScaler match modes
            // and Pixel Perfect Camera. First button at -150 below center,
            // each subsequent button 110px lower.
            _menuButtonActions[0] = OnStartClicked;
            _menuButtonActions[1] = OnContinueClicked;
            _menuButtonActions[2] = OnSettingsClicked;
            _menuButtonActions[3] = OnQuitClicked;

            _menuButtons[0] = CreateMenuButton("StartButton",    ButtonStartLabel,    0);
            _menuButtons[1] = CreateMenuButton("ContinueButton", ButtonContinueLabel, 1);
            _menuButtons[2] = CreateMenuButton("SettingsButton", ButtonSettingsLabel, 2);
            _menuButtons[3] = CreateMenuButton("QuitButton",     ButtonQuitLabel,     3);

            _firstButton = _menuButtons[0];
            _selectedIndex = 0;

            // Hide menu until the player confirms PRESS START.
            SetMenuVisible(false);
        }

        private void BuildPressStartLabel()
        {
            var rect = CreateRect("PressStart", gameObject.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(800f, 90f);
            rect.anchoredPosition = new Vector2(0f, 90f); // 90px above bottom

            var text = rect.gameObject.AddComponent<Text>();
            text.text = "PRESS ANY KEY";
            text.alignment = TextAnchor.MiddleCenter;
            // Silver native 16pt → 64 = 4× native for the PRESS ANY KEY prompt.
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            text.raycastTarget = false;

            _pressStartText = rect.gameObject;
            _pressStartLabel = text;
        }

        // Returns the GameObject so the caller can populate _menuButtons.
        // Mirrors NightDashPauseMenuUI: Image (Simple + preserveAspect) +
        // direct sprite swap from SelectIndex (no Unity Button, no
        // EventSystem highlight). Keyboard navigation goes through the
        // Update polling loop above so it's not dependent on
        // InputSystemUIInputModule.
        private GameObject CreateMenuButton(string name, string label, int slotIndex)
        {
            // Sprite native is alpha-trimmed 101×37. Uniform 3× scale —
            // every source pixel maps to a 3×3 block — keeps pixel art
            // sharp without dominating the title screen.
            //   303 × 111 = 101 × 37 × 3
            const float buttonWidth = 303f;
            const float buttonHeight = 111f;
            const float buttonSpacing = 22f;
            const float topY = 100f; // first button slightly above screen center

            var rect = CreateRect(name, gameObject.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            rect.anchoredPosition = new Vector2(0f, topY - slotIndex * (buttonHeight + buttonSpacing));

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _buttonSpriteDefault;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false; // Keyboard-only menu.

            var textRect = CreateRect("Label", rect);
            StretchFull(textRect);
            var text = textRect.gameObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            // Silver native 16pt → 32 = 2× native, matches PauseMenu button labels.
            text.fontSize = 32;
            text.fontStyle = FontStyle.Normal;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;

            _menuButtonLabels[slotIndex] = text;
            return rect.gameObject;
        }

        private void LoadButtonSprites()
        {
            _buttonSpriteDefault  = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_default");
            _buttonSpriteHover    = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_hover");
            _buttonSpriteDisabled = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_disabled");
        }

        private void OnStartClicked()
        {
            // Hand off to NightDashLobbyScreenUI (M2). The legacy
            // RunSelectionLobbyUI stays disabled — it draws via OnGUI and
            // would otherwise overlay the new Canvas lobby.
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            var lobby = FindFirstObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include);
            if (lobby != null)
            {
                lobby.gameObject.SetActive(true);
            }
            else if (_lobbyUi != null)
            {
                // Fallback: legacy lobby if Canvas one is missing.
                _lobbyUi.enabled = true;
                _lobbyUi.SetLobbyVisible(true);
                Time.timeScale = 1f;
                RestoreGameplayViews();
            }
            gameObject.SetActive(false);
            NightDashLog.Info("[NightDash] Title Start clicked (Canvas UI).");
        }

        private void OnContinueClicked()
        {
            // Continue uses the same path as Start for now — RunSelectionSession
            // already restores the last stage/class selection from PlayerPrefs.
            // Future: branch to Result review or last save slot.
            OnStartClicked();
        }

        private void OnSettingsClicked()
        {
            // Placeholder: settings panel arrives in a later sprint.
            NightDashLog.Info("[NightDash] Settings — not yet implemented.");
        }

        private void OnQuitClicked()
        {
            NightDashLog.Info("[NightDash] Quit clicked.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                if (es.GetComponent(inputSystemModuleType) == null)
                {
                    es.gameObject.AddComponent(inputSystemModuleType);
                }

                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    standalone.enabled = false;
                }
            }
            else
            {
                // Fallback for environments without Input System UI module.
                if (es.GetComponent<StandaloneInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                }
            }
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

        // Sets RawImage.uvRect so the texture renders in "cover" mode —
        // native aspect preserved, longer axis trimmed. Centered.
        private static void ApplyCoverFitUv(RawImage img)
        {
            if (img == null || img.texture == null) return;
            float texAspect = (float)img.texture.width / Mathf.Max(1, img.texture.height);
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);

            if (texAspect > screenAspect)
            {
                // Texture wider than screen — trim left/right.
                float fit = screenAspect / texAspect;
                float pad = (1f - fit) * 0.5f;
                img.uvRect = new Rect(pad, 0f, fit, 1f);
            }
            else
            {
                // Texture taller than screen — trim top/bottom.
                float fit = texAspect / screenAspect;
                float pad = (1f - fit) * 0.5f;
                img.uvRect = new Rect(0f, pad, 1f, fit);
            }
        }
    }
}
