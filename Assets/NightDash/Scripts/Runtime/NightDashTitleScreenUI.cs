using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using NightDash.Runtime.UI;

namespace NightDash.Runtime
{
    public sealed class NightDashTitleScreenUI : MonoBehaviour
    {
        [SerializeField] private Texture2D titleTexture;
        [SerializeField] private Texture2D logoTexture;
        [SerializeField] private Texture2D buttonDefaultTexture;
        [SerializeField] private Texture2D buttonHoverTexture;
        [SerializeField] private Texture2D buttonPressedTexture;
        [SerializeField] private Texture2D buttonDisabledTexture;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        // Sprint B M1: 4-button menu. Localization deferred to a future sprint;
        // strings inlined for now and routed through M0 ScreenRouter.
        private const string ButtonStartLabel = "Start";
        private const string ButtonContinueLabel = "Continue";
        private const string ButtonSettingsLabel = "Settings";
        private const string ButtonQuitLabel = "Quit";

        private RunSelectionLobbyUI _lobbyUi;
        private GameObject _firstButton;

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
                if (titleTexture == null)
                {
                    titleTexture = _lobbyUi.TitleTexture;
                }

                if (logoTexture == null)
                {
                    logoTexture = _lobbyUi.TitleLogoTexture;
                }
            }

            // Resources fallback so the title works without inspector setup
            // when assets are placed under Resources/NightDash/UI/Title/.
            if (titleTexture == null)
            {
                titleTexture = Resources.Load<Texture2D>("NightDash/UI/Title/title_screen_background");
            }
            if (logoTexture == null)
            {
                logoTexture = Resources.Load<Texture2D>("NightDash/UI/Title/nightdash_logo");
            }

            EnsureEventSystem();
            LoadButtonFrameTextures();
            BuildCanvas();
        }

        private void OnEnable()
        {
            // Title becomes the input-routing context whenever the screen is shown.
            NightDashInputContextStack.Push(NightDashInputContext.Title);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);

            // Restore default selection so keyboard/gamepad navigation has a focus.
            if (_firstButton != null)
            {
                EventSystem.current?.SetSelectedGameObject(_firstButton);
            }
        }

        private void OnDisable()
        {
            NightDashInputContextStack.Pop(NightDashInputContext.Title);
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
            canvas.sortingOrder = 1000;

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

            var bg = CreateRect("TitleBackground", gameObject.transform);
            var bgImage = bg.gameObject.AddComponent<RawImage>();
            bgImage.texture = titleTexture;
            bgImage.color = Color.white;
            StretchFull(bg);

            if (logoTexture != null)
            {
                var logoRect = CreateRect("TitleLogo", gameObject.transform);
                logoRect.anchorMin = new Vector2(1f, 1f);
                logoRect.anchorMax = new Vector2(1f, 1f);
                logoRect.pivot = new Vector2(1f, 1f);
                logoRect.anchoredPosition = new Vector2(-40f, -30f);
                logoRect.sizeDelta = new Vector2(460f, 180f);

                var logoImage = logoRect.gameObject.AddComponent<RawImage>();
                logoImage.texture = logoTexture;
                logoImage.color = Color.white;
            }

            // 4-button vertical stack centered horizontally, anchored to lower
            // third of the screen. EventSystem handles arrow-key navigation
            // automatically once first selection is set in OnEnable.
            const float buttonWidth = 460f;
            const float buttonHeight = 90f;
            const float buttonSpacing = 14f;
            const float bottomY = 0.32f; // anchor of the bottom button

            var startBtn = CreateMenuButton("StartButton", ButtonStartLabel,
                new Vector2(0.5f, bottomY + 3f * (buttonHeight + buttonSpacing) / referenceResolution.y),
                new Vector2(buttonWidth, buttonHeight), OnStartClicked);
            CreateMenuButton("ContinueButton", ButtonContinueLabel,
                new Vector2(0.5f, bottomY + 2f * (buttonHeight + buttonSpacing) / referenceResolution.y),
                new Vector2(buttonWidth, buttonHeight), OnContinueClicked);
            CreateMenuButton("SettingsButton", ButtonSettingsLabel,
                new Vector2(0.5f, bottomY + 1f * (buttonHeight + buttonSpacing) / referenceResolution.y),
                new Vector2(buttonWidth, buttonHeight), OnSettingsClicked);
            CreateMenuButton("QuitButton", ButtonQuitLabel,
                new Vector2(0.5f, bottomY),
                new Vector2(buttonWidth, buttonHeight), OnQuitClicked);

            _firstButton = startBtn;
        }

        // Returns the GameObject so the caller can set first-selection target.
        private GameObject CreateMenuButton(string name, string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(name, gameObject.transform);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.sprite = CreateSprite(buttonDefaultTexture);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState
            {
                highlightedSprite = CreateSprite(buttonHoverTexture),
                pressedSprite = CreateSprite(buttonPressedTexture),
                disabledSprite = CreateSprite(buttonDisabledTexture)
            };
            btn.onClick.AddListener(onClick);

            var textRect = CreateRect("Label", rect);
            StretchFull(textRect);
            var text = textRect.gameObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 38;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return rect.gameObject;
        }

        private void OnStartClicked()
        {
            // Route through M0 ScreenRouter; lobby UI's existing toggle is
            // preserved for backwards compatibility until M2 swaps it for
            // a router-driven panel.
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            if (_lobbyUi != null) _lobbyUi.SetLobbyVisible(true);
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

        private void LoadButtonFrameTextures()
        {
            NightDashButtonFrameStyle.LoadAndCropFrameTextures(
                ref buttonDefaultTexture,
                ref buttonHoverTexture,
                ref buttonPressedTexture,
                ref buttonDisabledTexture);
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
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
