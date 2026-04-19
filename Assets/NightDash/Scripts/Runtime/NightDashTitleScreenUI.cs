using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

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
        [SerializeField] private string startButtonText = "Start";
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private RunSelectionLobbyUI _lobbyUi;

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

            EnsureEventSystem();
            LoadButtonFrameTextures();
            BuildCanvas();
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

            var buttonRect = CreateRect("StartButton", gameObject.transform);
            buttonRect.anchorMin = new Vector2(0.5f, 0.14f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.14f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(460f, 100f);
            buttonRect.anchoredPosition = Vector2.zero;

            var buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = Color.white;
            buttonImage.type = Image.Type.Simple;
            buttonImage.preserveAspect = false;
            buttonImage.sprite = CreateSprite(buttonDefaultTexture);

            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = CreateSprite(buttonHoverTexture),
                pressedSprite = CreateSprite(buttonPressedTexture),
                disabledSprite = CreateSprite(buttonDisabledTexture)
            };
            button.onClick.AddListener(OnStartClicked);

            var textRect = CreateRect("Label", buttonRect);
            StretchFull(textRect);
            var text = textRect.gameObject.AddComponent<Text>();
            text.text = startButtonText;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 42;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnStartClicked()
        {
            if (_lobbyUi != null)
            {
                _lobbyUi.SetLobbyVisible(true);
            }

            gameObject.SetActive(false);
            NightDashLog.Info("[NightDash] Title Start clicked (Canvas UI).");
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
