using NightDash.ECS.Components;
using NightDash.Data;
using NightDash.Runtime;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        private readonly RectTransform[] _optionCards = new RectTransform[3];
        private readonly Image[] _optionCardImages = new Image[3];

        // Keyboard navigation state — visible while the level-up panel is
        // active. Mouse clicks still work via the Button.onClick handlers.
        private int _selectedIndex;
        private int _visibleOptionCount;

        // Card frame sprites loaded once at Awake. UpgradeOptionElement
        // doesn't carry a rarity field yet — until it does, the three
        // options visually map to common / rare / legendary in slot order
        // so the rare tiers actually show up in playtests.
        private Sprite _cardSpriteCommon;
        private Sprite _cardSpriteRare;
        private Sprite _cardSpriteLegendary;

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
            LoadCardSprites();
            BuildCanvas();
        }

        private void LoadCardSprites()
        {
            _cardSpriteCommon    = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_card_common");
            _cardSpriteRare      = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_card_rare");
            _cardSpriteLegendary = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_card_legendary");
        }

        private Sprite ResolveCardSprite(int slotIndex)
        {
            // Temporary mapping until UpgradeOptionElement gains a Rarity field.
            switch (slotIndex)
            {
                case 0: return _cardSpriteCommon;
                case 1: return _cardSpriteRare;
                case 2: return _cardSpriteLegendary;
                default: return _cardSpriteCommon;
            }
        }

        private void Update()
        {
            RefreshState();
            if (_levelRoot != null && _levelRoot.activeSelf)
            {
                HandleKeyboardNav();
            }
        }

        private void HandleKeyboardNav()
        {
            if (_visibleOptionCount <= 0) return;

            bool left, right, confirm;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
#else
            left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif

            if (left) SelectIndex(_selectedIndex - 1);
            else if (right) SelectIndex(_selectedIndex + 1);
            else if (confirm) SubmitSelection(_selectedIndex);
        }

        private void SelectIndex(int idx)
        {
            int n = _visibleOptionCount;
            if (n <= 0) return;
            _selectedIndex = ((idx % n) + n) % n;
            ApplySelectionVisuals();
        }

        // Selected card pops to 1.08× and stays at full brightness; the
        // others sit at 1.0× with a slight desaturated tint so the pointer
        // location is obvious without an extra overlay sprite.
        private void ApplySelectionVisuals()
        {
            for (int i = 0; i < _optionCards.Length; i++)
            {
                var rt = _optionCards[i];
                if (rt == null) continue;
                bool selected = i == _selectedIndex && i < _visibleOptionCount;
                rt.localScale = selected
                    ? new Vector3(1.08f, 1.08f, 1f)
                    : Vector3.one;

                var img = _optionCardImages[i];
                if (img != null)
                {
                    img.color = selected
                        ? Color.white
                        : new Color(0.70f, 0.70f, 0.74f, 1f);
                }

                if (_optionTexts[i] != null)
                {
                    _optionTexts[i].color = selected
                        ? new Color(1f, 0.95f, 0.78f, 1f)
                        : new Color(0.78f, 0.74f, 0.68f, 1f);
                }
            }
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

            // Panel sized to the actual content: header (96) + card row
            // (464) + footer (96) + 3 spacings (24) + top/bottom padding
            // (24+24) = 752 height. 1320 width lets the card row breathe
            // without crowding the panel edges.
            RectTransform panel = CreateRect("Panel", _levelRoot.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1320f, 760f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.07f, 0.16f, 0.94f);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 24f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;

            _headerText = CreateText(panel, "LEVEL UP", 64, TextAnchor.MiddleCenter, Color.white);
            SetPreferredHeight(_headerText.rectTransform, 96f);

            RectTransform cards = CreateRect("Cards", panel);
            HorizontalLayoutGroup cardsLayout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 24f;
            cardsLayout.childAlignment = TextAnchor.MiddleCenter;
            cardsLayout.childControlHeight = false;
            cardsLayout.childControlWidth = false;
            SetPreferredHeight(cards, 464f);

            // Source sprite is alpha-trimmed 84×116. Uniform 4× scale keeps
            // every source pixel mapped to a 4×4 block — pixel art stays
            // sharp at a comfortable read size:
            //   3 * 336 + 2 * 24 = 1056 < 1320 panel width.
            const float cardWidth = 336f;
            const float cardHeight = 464f;

            for (int i = 0; i < 3; i++)
            {
                int optionIndex = i;
                RectTransform card = CreateRect($"Option{optionIndex}", cards);
                card.sizeDelta = new Vector2(cardWidth, cardHeight);
                LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
                cardLayout.preferredWidth = cardWidth;
                cardLayout.preferredHeight = cardHeight;

                Image cardImage = card.gameObject.AddComponent<Image>();
                cardImage.sprite = ResolveCardSprite(i);
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
                cardImage.color = Color.white;

                Button button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardImage;
                button.onClick.AddListener(() => SubmitSelection(optionIndex));
                _optionButtons[i] = button;
                _optionCards[i] = card;
                _optionCardImages[i] = cardImage;

                // Description text sits inside the card's lower description
                // panel. The card sprite reserves the bottom ~42% of its
                // height for text (the upper portion is the icon slot + the
                // gold/silver divider). Anchor + insets land the text inside
                // that region; Wrap+Overflow ensures long descriptions never
                // get clipped.
                _optionTexts[i] = CreateText(card, "-", 40, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.98f, 1f));
                var optText = _optionTexts[i];
                optText.horizontalOverflow = HorizontalWrapMode.Wrap;
                optText.verticalOverflow = VerticalWrapMode.Overflow;
                var textRect = optText.rectTransform;
                textRect.anchorMin = new Vector2(0f, 0.04f);
                textRect.anchorMax = new Vector2(1f, 0.42f);
                textRect.offsetMin = new Vector2(24f, 0f);
                textRect.offsetMax = new Vector2(-24f, 0f);
            }

            RectTransform footer = CreateRect("Footer", panel);
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 32f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlHeight = false;
            footerLayout.childControlWidth = false;
            SetPreferredHeight(footer, 96f);

            _rerollButton = CreateButton(footer, "REROLL", SubmitReroll);
            _rerollText = CreateText(footer, "Rerolls Left: 1", 32, TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.94f, 1f));
            SetPreferredWidth(_rerollText.rectTransform, 360f);
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

            int newVisible = 0;
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
                newVisible++;
            }

            // When the panel first appears (or option count changes), pin
            // the selection to the leftmost visible card and refresh visuals.
            if (newVisible != _visibleOptionCount)
            {
                _visibleOptionCount = newVisible;
                _selectedIndex = 0;
            }
            ApplySelectionVisuals();
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
            rect.sizeDelta = new Vector2(280f, 80f);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 280f;
            layout.preferredHeight = 80f;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.18f, 0.34f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = CreateText(rect, label, 32, TextAnchor.MiddleCenter, Color.white);
            StretchFull(text.rectTransform);
            return button;
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor anchor, Color color)
        {
            RectTransform rect = CreateRect("Text", parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
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
