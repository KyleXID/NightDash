using NightDash.ECS.Components;
using NightDash.Data;
using NightDash.Runtime;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightDash.Runtime
{
    public sealed class LevelUpSelectionUI : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);

        private GameObject _levelRoot;
        private Text _headerText;
        private Text _rerollText;
        private Button _rerollButton;
        private readonly Text[] _optionTexts = new Text[3];
        private readonly Button[] _optionButtons = new Button[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<LevelUpSelectionUI>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var go = new GameObject("LevelUpSelectionUI");
            go.AddComponent<LevelUpSelectionUI>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
        }

        private void Update()
        {
            RefreshState();
        }

        private void BuildCanvas()
        {
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
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

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            _levelRoot = CreateRect("LevelRoot", transform).gameObject;
            StretchFull((RectTransform)_levelRoot.transform);

            RectTransform dim = CreateRect("Dim", _levelRoot.transform);
            StretchFull(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.75f);

            RectTransform panel = CreateRect("Panel", _levelRoot.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1180f, 460f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.07f, 0.16f, 0.94f);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 18f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;

            _headerText = CreateText(panel, "LEVEL UP", 34, TextAnchor.MiddleCenter, Color.white);
            SetPreferredHeight(_headerText.rectTransform, 44f);

            RectTransform cards = CreateRect("Cards", panel);
            HorizontalLayoutGroup cardsLayout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 16f;
            cardsLayout.childAlignment = TextAnchor.MiddleCenter;
            cardsLayout.childControlHeight = false;
            cardsLayout.childControlWidth = false;
            SetPreferredHeight(cards, 250f);

            for (int i = 0; i < 3; i++)
            {
                int optionIndex = i;
                RectTransform card = CreateRect($"Option{optionIndex}", cards);
                card.sizeDelta = new Vector2(340f, 250f);
                LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
                cardLayout.preferredWidth = 340f;
                cardLayout.preferredHeight = 250f;

                Image cardImage = card.gameObject.AddComponent<Image>();
                cardImage.color = new Color(0.17f, 0.11f, 0.24f, 1f);

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardImage;
                button.onClick.AddListener(() => SubmitSelection(optionIndex));
                _optionButtons[i] = button;

                _optionTexts[i] = CreateText(card, "-", 22, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.98f, 1f));
                StretchFull(_optionTexts[i].rectTransform);
            }

            RectTransform footer = CreateRect("Footer", panel);
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 18f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlHeight = false;
            footerLayout.childControlWidth = false;
            SetPreferredHeight(footer, 72f);

            _rerollButton = CreateButton(footer, "REROLL", SubmitReroll);
            _rerollText = CreateText(footer, "Rerolls Left: 1", 20, TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.94f, 1f));
            SetPreferredWidth(_rerollText.rectTransform, 220f);
        }

        private void RefreshState()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                SetVisible(false);
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameLoopState>(),
                ComponentType.ReadOnly<PlayerProgressionState>(),
                ComponentType.ReadWrite<UpgradeSelectionRequest>(),
                ComponentType.ReadOnly<UpgradeOptionElement>());
            if (query.IsEmptyIgnoreFilter)
            {
                SetVisible(false);
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            GameLoopState loop = entityManager.GetComponentData<GameLoopState>(singleton);
            if (loop.Status != RunStatus.LevelUpSelection)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            PlayerProgressionState progression = entityManager.GetComponentData<PlayerProgressionState>(singleton);
            DynamicBuffer<UpgradeOptionElement> options = entityManager.GetBuffer<UpgradeOptionElement>(singleton);

            if (_headerText != null)
            {
                _headerText.text = $"LEVEL UP  Lv {loop.Level}";
            }

            if (_rerollText != null)
            {
                _rerollText.text = $"Rerolls Left: {progression.RerollsRemaining}";
            }

            if (_rerollButton != null)
            {
                _rerollButton.interactable = progression.RerollsRemaining > 0;
            }

            for (int i = 0; i < _optionButtons.Length; i++)
            {
                bool hasOption = i < options.Length;
                _optionButtons[i].gameObject.SetActive(hasOption);
                if (!hasOption)
                {
                    continue;
                }

                UpgradeOptionElement option = options[i];
                _optionButtons[i].interactable = true;
                _optionTexts[i].text = BuildOptionText(option);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_levelRoot != null)
            {
                _levelRoot.SetActive(visible);
            }
        }

        private static void SubmitSelection(int optionIndex)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<UpgradeSelectionRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            entityManager.SetComponentData(singleton, new UpgradeSelectionRequest
            {
                SelectedOptionIndex = optionIndex,
                HasSelection = 1,
                RerollRequested = 0
            });
        }

        private static void SubmitReroll()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerProgressionState>(),
                ComponentType.ReadWrite<UpgradeSelectionRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            PlayerProgressionState progression = entityManager.GetComponentData<PlayerProgressionState>(singleton);
            if (progression.RerollsRemaining <= 0)
            {
                return;
            }

            UpgradeSelectionRequest request = entityManager.GetComponentData<UpgradeSelectionRequest>(singleton);
            request.SelectedOptionIndex = -1;
            request.HasSelection = 0;
            request.RerollRequested = 1;
            entityManager.SetComponentData(singleton, request);
        }

        private static string BuildOptionText(UpgradeOptionElement option)
        {
            string title = option.Id.ToString();
            string detail = string.Empty;
            DataRegistry registry = DataRegistry.Instance;

            if (registry != null)
            {
                if (option.Kind == UpgradeKind.Weapon &&
                    registry.TryGetWeapon(option.Id.ToString(), out WeaponData weapon) &&
                    weapon != null)
                {
                    title = string.IsNullOrWhiteSpace(weapon.displayName) ? title : weapon.displayName.Trim();
                }
                else if (option.Kind == UpgradeKind.Passive &&
                    registry.TryGetPassive(option.Id.ToString(), out PassiveData passive) &&
                    passive != null)
                {
                    title = string.IsNullOrWhiteSpace(passive.displayName) ? title : passive.displayName.Trim();
                    detail = passive.description ?? string.Empty;
                }
            }

            string levelLine;
            if (option.Kind == UpgradeKind.Weapon)
            {
                levelLine = option.CurrentLevel == 0
                    ? $"Unlock {title}"
                    : $"{title} Lv {option.CurrentLevel} -> Lv {option.NextLevel}";
            }
            else
            {
                levelLine = option.CurrentLevel == 0
                    ? $"Gain {title}"
                    : $"{title} Lv {option.CurrentLevel} -> Lv {option.NextLevel}";
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return $"{option.Kind}\n{levelLine}\n{detail}";
            }

            return $"{option.Kind}\n{levelLine}";
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = CreateRect($"{label}Button", parent);
            rect.sizeDelta = new Vector2(240f, 64f);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 240f;
            layout.preferredHeight = 64f;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.18f, 0.34f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = CreateText(rect, label, 24, TextAnchor.MiddleCenter, Color.white);
            StretchFull(text.rectTransform);
            return button;
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            RectTransform rect = CreateRect("Text", parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
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

        private static void SetPreferredHeight(RectTransform rect, float height)
        {
            LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = rect.gameObject.AddComponent<LayoutElement>();
            }
            layout.preferredHeight = height;
        }

        private static void SetPreferredWidth(RectTransform rect, float width)
        {
            LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = rect.gameObject.AddComponent<LayoutElement>();
            }
            layout.preferredWidth = width;
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
