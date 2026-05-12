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
        private readonly Text[] _optionKindTexts = new Text[3];
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

            // Panel sized to fit header(96) + cards(520) + footer(96) +
            // 2 spacings(48) + 2 padding(128) = 888, with a sliver of
            // headroom for the ornate frame's corner ornament.
            // Cards row height is intentionally larger than card height so
            // the selected card's 1.08× scale (~+18px on each axis) has
            // room before bleeding into the footer above/below.
            RectTransform panel = CreateRect("Panel", _levelRoot.transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1320f, 940f);

            // Backdrop fills the inside of the ornate frame with a dark wash
            // so card text stays readable on top of the gameplay scene.
            // ignoreLayout so the VerticalLayoutGroup doesn't squish it.
            RectTransform backdrop = CreateRect("Backdrop", panel);
            StretchFull(backdrop);
            backdrop.offsetMin = new Vector2(28f, 28f);
            backdrop.offsetMax = new Vector2(-28f, -28f);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0.1f, 0.07f, 0.16f, 0.94f);
            var backdropLayout = backdrop.gameObject.AddComponent<LayoutElement>();
            backdropLayout.ignoreLayout = true;

            // Ornate frame overlay — sprite Sliced so the corner ornament
            // stays pixel-perfect at 1320×760 (5× the 256×192 source).
            RectTransform frame = CreateRect("Frame", panel);
            StretchFull(frame);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            var panelSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_panel_default");
            if (panelSprite != null)
            {
                frameImage.sprite = panelSprite;
                frameImage.type = Image.Type.Sliced;
                frameImage.color = Color.white;
            }
            else
            {
                frameImage.color = new Color(0f, 0f, 0f, 0f);
            }
            frameImage.raycastTarget = false;
            var frameLayout = frame.gameObject.AddComponent<LayoutElement>();
            frameLayout.ignoreLayout = true;

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            // Tight top padding (8) so "LEVEL UP" sits near the frame's
            // upper trim. Bottom padding 56 keeps the footer comfortably
            // inside the ornate frame.
            layout.padding = new RectOffset(56, 56, 8, 56);
            layout.spacing = 16f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            _headerText = CreateText(panel, "LEVEL UP", 64, TextAnchor.MiddleCenter, Color.white);
            SetPreferredHeight(_headerText.rectTransform, 96f);

            RectTransform cards = CreateRect("Cards", panel);
            HorizontalLayoutGroup cardsLayout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.spacing = 24f;
            cardsLayout.childAlignment = TextAnchor.MiddleCenter;
            cardsLayout.childControlHeight = false;
            cardsLayout.childControlWidth = false;
            // 520 = card (464) + ~28px margin above/below so the selected
            // card's 1.08× scale doesn't bleed into header/footer.
            SetPreferredHeight(cards, 520f);

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

                // Kind label sits in the card's upper panel area (~62~92%)
                // so "WEAPON" / "PASSIVE" reads clearly and never collides
                // with the description below. Anchored above the divider
                // line so the gold/silver trim doesn't slice the glyphs.
                _optionKindTexts[i] = CreateText(card, "-", 40, TextAnchor.MiddleCenter,
                    new Color(1f, 0.92f, 0.74f, 1f));
                _optionKindTexts[i].fontStyle = FontStyle.Bold;
                var kindRect = _optionKindTexts[i].rectTransform;
                kindRect.anchorMin = new Vector2(0f, 0.62f);
                kindRect.anchorMax = new Vector2(1f, 0.92f);
                kindRect.offsetMin = new Vector2(20f, 0f);
                kindRect.offsetMax = new Vector2(-20f, 0f);

                // Description text sits inside the card's lower description
                // panel (~4~50%). Kind label moved out, so this region now
                // only holds the level line + optional flavor detail.
                _optionTexts[i] = CreateText(card, "-", 40, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.98f, 1f));
                var optText = _optionTexts[i];
                optText.horizontalOverflow = HorizontalWrapMode.Wrap;
                optText.verticalOverflow = VerticalWrapMode.Truncate;
                optText.resizeTextForBestFit = true;
                optText.resizeTextMinSize = 22;
                optText.resizeTextMaxSize = 40;
                var textRect = optText.rectTransform;
                textRect.anchorMin = new Vector2(0f, 0.04f);
                textRect.anchorMax = new Vector2(1f, 0.50f);
                textRect.offsetMin = new Vector2(24f, 0f);
                textRect.offsetMax = new Vector2(-24f, 0f);
            }

            // Footer = REROLL button centered + counter parked to its right.
            // No HorizontalLayoutGroup; the two pieces are positioned via
            // explicit anchors so the button stays exactly on the panel's
            // vertical centerline.
            RectTransform footer = CreateRect("Footer", panel);
            SetPreferredHeight(footer, 120f);

            // REROLL action button — centered on the footer.
            _rerollButton = CreateButton(footer, "REROLL", SubmitReroll);
            RectTransform rerollRect = _rerollButton.GetComponent<RectTransform>();
            rerollRect.anchorMin = new Vector2(0.5f, 0.5f);
            rerollRect.anchorMax = new Vector2(0.5f, 0.5f);
            rerollRect.pivot = new Vector2(0.5f, 0.5f);
            rerollRect.sizeDelta = new Vector2(303f, 111f);
            rerollRect.anchoredPosition = Vector2.zero;
            var rerollLE = _rerollButton.GetComponent<LayoutElement>();
            if (rerollLE != null)
            {
                rerollLE.preferredWidth = 303f;
                rerollLE.preferredHeight = 111f;
                rerollLE.ignoreLayout = true;
            }
            var rerollLabel = _rerollButton.GetComponentInChildren<Text>();
            if (rerollLabel != null) rerollLabel.fontSize = 44;

            // Counter (reroll icon + "Rerolls Left: N") sits to the RIGHT
            // of the centered button — its left edge anchors to the button's
            // right edge, separated by a small gap.
            RectTransform counter = CreateRect("RerollCounter", footer);
            counter.anchorMin = new Vector2(0.5f, 0.5f);
            counter.anchorMax = new Vector2(0.5f, 0.5f);
            counter.pivot = new Vector2(0f, 0.5f);
            counter.sizeDelta = new Vector2(420f, 80f);
            // Button half-width 151.5 + gap 24 ≈ 176 from center.
            counter.anchoredPosition = new Vector2(176f, 0f);
            HorizontalLayoutGroup counterLayout = counter.gameObject.AddComponent<HorizontalLayoutGroup>();
            counterLayout.spacing = 14f;
            counterLayout.childAlignment = TextAnchor.MiddleLeft;
            counterLayout.childControlHeight = false;
            counterLayout.childControlWidth = false;

            CreateIconImage(counter, "NightDash/UI/Icons/nd_ui_icon_reroll_default", 64f, 64f);
            _rerollText = CreateText(counter, "Rerolls Left: 1", 32, TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.94f, 1f));
            SetPreferredWidth(_rerollText.rectTransform, 340f);
        }

        // Loads a sprite from Resources and stamps it inside a layout-friendly
        // RectTransform. Used by the reroll counter to surface the reroll icon
        // beside its remaining-count text.
        private static void CreateIconImage(Transform parent, string spritePath, float width, float height)
        {
            RectTransform rect = CreateRect("Icon", parent);
            rect.sizeDelta = new Vector2(width, height);
            LayoutElement le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            Image img = rect.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null) img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
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
                if (_optionKindTexts[i] != null)
                {
                    _optionKindTexts[i].text = BuildOptionKindLabel(option);
                }
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

        // Used by the card's TOP slot — just the upgrade category in upper
        // case. Lives separately so long body text never pushes the label
        // out of frame.
        private static string BuildOptionKindLabel(UpgradeOptionElement option)
        {
            return option.Kind.ToString().ToUpperInvariant();
        }

        // Used by the card's BOTTOM description slot — title, level line,
        // and optional flavor detail. Kind is intentionally NOT included
        // anymore (rendered separately at the top of the card).
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
                return $"{levelLine}\n{detail}";
            }

            return levelLine;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            // Sized at uniform 2× of the alpha-trimmed 101×37 button sprite
            // so footer buttons stay compact next to the reroll counter while
            // still matching the bronze-trim style used everywhere else.
            RectTransform rect = CreateRect($"{label}Button", parent);
            rect.sizeDelta = new Vector2(202f, 74f);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 202f;
            layout.preferredHeight = 74f;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            var defSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_default");
            var hovSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_hover");
            var pressSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_pressed");
            var disSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_disabled");
            if (defSprite != null)
            {
                image.sprite = defSprite;
            }
            else
            {
                image.color = new Color(0.26f, 0.18f, 0.34f, 1f);
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (defSprite != null && hovSprite != null && pressSprite != null && disSprite != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = hovSprite,
                    pressedSprite = pressSprite,
                    disabledSprite = disSprite
                };
            }
            button.onClick.AddListener(onClick);

            Text text = CreateText(rect, label, 28, TextAnchor.MiddleCenter, new Color(0.96f, 0.94f, 0.78f, 1f));
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
