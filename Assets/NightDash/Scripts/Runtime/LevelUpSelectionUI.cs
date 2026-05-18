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
        // Per-card item / passive icon, sandwiched between the kind banner
        // (top ~72-96%) and the description body (~4-40%). Populated each
        // refresh from UpgradeOptionElement.Id via DataRegistry, with a
        // category-specific placeholder when no sprite is shipped.
        private readonly Image[] _optionIconImages = new Image[3];
        // ScrollRect + content RectTransform per card, used for auto-scroll
        // when the description overflows the visible band.
        private readonly ScrollRect[] _optionScrollRects = new ScrollRect[3];
        private readonly RectTransform[] _optionContentRects = new RectTransform[3];
        // Tracks how long the currently-selected card has been focused. Used
        // by the ping-pong scroll animation so each focus change restarts
        // the loop at the top.
        private float _focusedScrollTime;
        private int _lastScrollFocusIndex = -1;

        // Keyboard navigation state — visible while the level-up panel is
        // active. Mouse clicks still work via the Button.onClick handlers.
        private int _selectedIndex;
        private int _visibleOptionCount;
        // When true, the keyboard focus is on the REROLL action button
        // instead of one of the cards. Down arrow on a card moves focus
        // here; up arrow returns focus to the last-selected card.
        private bool _rerollFocused;

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
                TickAutoScroll();
            }
        }

        // Ping-pong scroll for the focused card. Non-focused cards snap back
        // to the top so their description always starts from line 1 when the
        // player tabs over to them.
        private void TickAutoScroll()
        {
            // Hold at the top for a beat, then crawl to the bottom, hold, and
            // crawl back. Tuned so a 4-line description has time to read.
            const float HoldTopSec    = 1.2f;
            const float ScrollDownSec = 3.0f;
            const float HoldBotSec    = 1.0f;
            const float ScrollUpSec   = 2.0f;
            float period = HoldTopSec + ScrollDownSec + HoldBotSec + ScrollUpSec;

            if (_selectedIndex != _lastScrollFocusIndex)
            {
                _focusedScrollTime = 0f;
                _lastScrollFocusIndex = _selectedIndex;
            }
            _focusedScrollTime += Time.unscaledDeltaTime;

            for (int i = 0; i < _optionContentRects.Length; i++)
            {
                RectTransform host = _optionContentRects[i];
                Text text = _optionTexts[i];
                if (host == null || text == null || !host.gameObject.activeInHierarchy) continue;
                RectTransform tr = text.rectTransform;

                float overshoot = tr.rect.height - host.rect.height;
                // Text fits inside the mask — pin to top, no animation.
                if (overshoot <= 1f)
                {
                    tr.anchoredPosition = Vector2.zero;
                    continue;
                }
                if (i != _selectedIndex)
                {
                    tr.anchoredPosition = Vector2.zero;
                    continue;
                }

                float t = _focusedScrollTime % period;
                float normalized;
                if (t < HoldTopSec)
                    normalized = 0f;
                else if (t < HoldTopSec + ScrollDownSec)
                    normalized = (t - HoldTopSec) / ScrollDownSec;
                else if (t < HoldTopSec + ScrollDownSec + HoldBotSec)
                    normalized = 1f;
                else
                    normalized = 1f - (t - HoldTopSec - ScrollDownSec - HoldBotSec) / ScrollUpSec;
                normalized = Mathf.Clamp01(normalized);
                // Slide the Text rect down (positive y, since pivot is top
                // and we want the bottom of the text visible).
                tr.anchoredPosition = new Vector2(0f, overshoot * normalized);
            }
        }

        private void HandleKeyboardNav()
        {
            if (_visibleOptionCount <= 0) return;

            bool left, right, up, down, confirm;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
#else
            left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif

            if (_rerollFocused)
            {
                // Up arrow returns focus to the cards (last selected slot).
                if (up)
                {
                    _rerollFocused = false;
                    ApplySelectionVisuals();
                }
                else if (confirm)
                {
                    if (_rerollButton != null && _rerollButton.interactable)
                    {
                        SubmitReroll();
                    }
                }
                return;
            }

            // Card focus: left/right cycles cards, down hops to REROLL,
            // confirm picks the card.
            if (left) SelectIndex(_selectedIndex - 1);
            else if (right) SelectIndex(_selectedIndex + 1);
            else if (down && _rerollButton != null && _rerollButton.interactable)
            {
                _rerollFocused = true;
                ApplySelectionVisuals();
            }
            else if (confirm) SubmitSelection(_selectedIndex);
        }

        private void SelectIndex(int idx)
        {
            int n = _visibleOptionCount;
            if (n <= 0) return;
            _selectedIndex = ((idx % n) + n) % n;
            _rerollFocused = false;
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
                bool selected = !_rerollFocused && i == _selectedIndex && i < _visibleOptionCount;
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

            // Mirror the card focus treatment on the REROLL button — pop
            // scale + bright tint when focused, otherwise default.
            if (_rerollButton != null)
            {
                _rerollButton.transform.localScale = _rerollFocused
                    ? new Vector3(1.08f, 1.08f, 1f)
                    : Vector3.one;
                var rerollImg = _rerollButton.GetComponent<Image>();
                if (rerollImg != null && _rerollButton.interactable)
                {
                    // Match the button's existing sprite-swap states: hover
                    // when focused, default otherwise.
                    var hover = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_hover");
                    var defaultSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_frame_button_default");
                    if (_rerollFocused && hover != null) rerollImg.sprite = hover;
                    else if (defaultSprite != null) rerollImg.sprite = defaultSprite;
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
            panel.sizeDelta = new Vector2(1500f, 940f);

            // Backdrop fills the inside of the ornate frame with a dark wash
            // so card text stays readable on top of the gameplay scene.
            // ignoreLayout so the VerticalLayoutGroup doesn't squish it.
            RectTransform backdrop = CreateRect("Backdrop", panel);
            StretchFull(backdrop);
            backdrop.offsetMin = new Vector2(40f, 40f);
            backdrop.offsetMax = new Vector2(-40f, -40f);
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
            cardsLayout.spacing = 36f;
            cardsLayout.childAlignment = TextAnchor.MiddleCenter;
            cardsLayout.childControlHeight = false;
            cardsLayout.childControlWidth = false;
            // HorizontalLayoutGroup defaults childForceExpand* to true, which
            // pads gaps between siblings with leftover row width and visually
            // ignores the spacing field. Disable so spacing maps 1:1 to pixels.
            cardsLayout.childForceExpandWidth = false;
            cardsLayout.childForceExpandHeight = false;
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

                // Item / passive icon — pinned to the top section of the
                // card. It's the focal visual, allowed to overlap the
                // WEAPON/PASSIVE kind banner above it (text stays readable
                // thanks to its outline).
                // Card visual zones (bottom-up percent of card rect):
                //   0.02-0.34  → description body
                //   0.50-0.74  → icon zone (big art space ABOVE description)
                //   0.72-0.96  → WEAPON/PASSIVE banner label
                // The icon sits inside the art zone — nudged slightly toward
                // the banner so it doesn't crowd the description below.
                RectTransform iconRect = CreateRect("OptionIcon", card);
                iconRect.anchorMin = new Vector2(0.5f, 0.56f);
                iconRect.anchorMax = new Vector2(0.5f, 0.78f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(90f, 90f);
                Image iconImg = iconRect.gameObject.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                _optionIconImages[i] = iconImg;

                // Kind label sits in the card's TOP banner (~72~96%) so it
                // never collides with the description below. Outline keeps
                // it readable on the legendary card's gold center glow.
                _optionKindTexts[i] = CreateText(card, "-", 40, TextAnchor.MiddleCenter,
                    new Color(1f, 0.92f, 0.74f, 1f));
                _optionKindTexts[i].fontStyle = FontStyle.Bold;
                // Outline already added by CreateText.
                var kindRect = _optionKindTexts[i].rectTransform;
                kindRect.anchorMin = new Vector2(0f, 0.72f);
                kindRect.anchorMax = new Vector2(1f, 0.96f);
                kindRect.offsetMin = new Vector2(20f, 0f);
                kindRect.offsetMax = new Vector2(-20f, 0f);

                // Description host — a clipped window pinned to the BOTTOM
                // of the card with a FIXED pixel height that fits exactly
                // 3 lines of 28pt (28 × 3 = 84 + 12 padding ≈ 96px). Using
                // a percentage anchor here (the old 0.02..0.34 setup) made
                // the host ~148px which silently absorbed 4-5 lines and
                // suppressed the auto-scroll trigger. Pixel-fixed height
                // makes "3 lines visible, line 4 scrolls" reliable across
                // every description regardless of length.
                RectTransform descHost = CreateRect("DescHost", card);
                descHost.anchorMin = new Vector2(0f, 0f);
                descHost.anchorMax = new Vector2(1f, 0f);
                descHost.pivot = new Vector2(0.5f, 0f);
                descHost.offsetMin = new Vector2(24f, 12f);
                descHost.offsetMax = new Vector2(-24f, 108f);
                descHost.gameObject.AddComponent<RectMask2D>();
                _optionContentRects[i] = descHost;

                _optionTexts[i] = CreateText(descHost, "-", OptionBodyFontSize, TextAnchor.UpperCenter,
                    new Color(0.95f, 0.92f, 0.98f, 1f));
                var optText = _optionTexts[i];
                optText.horizontalOverflow = HorizontalWrapMode.Wrap;
                optText.verticalOverflow = VerticalWrapMode.Overflow;
                // Pin lineSpacing so the host-height math (28 × 3 = 84) is
                // independent of the font's intrinsic leading. Disable rich
                // text so a stray <size> tag in any data file can't bypass
                // the unified 28pt baseline.
                optText.lineSpacing = 1f;
                optText.supportRichText = false;
                var textRect = optText.rectTransform;
                // Anchor TOP-stretch so the text rect grows downward off the
                // top of the host. We'll set its height to preferredHeight
                // each refresh so wrap is honored, and TickAutoScroll slides
                // its anchoredPosition.y to scroll vertically.
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.pivot = new Vector2(0.5f, 1f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(-8f, 160f);

                // ScrollRect is gone, but the array slot kept for now so we
                // don't have to reshape the rest of the code.
                _optionScrollRects[i] = null;
            }

            // Footer hosts three explicitly-anchored siblings so each piece
            // lands exactly where it should, no LayoutGroup interference:
            //   - REROLL button: horizontally centered, slightly below mid.
            //   - reroll icon:   to the right of the button, vertically aligned.
            //   - "Rerolls Left" text: to the right of the icon.
            RectTransform footer = CreateRect("Footer", panel);
            SetPreferredHeight(footer, 140f);

            // REROLL action button — centered + nudged down 16 so it sits
            // a touch lower than the counter (user request).
            _rerollButton = CreateButton(footer, "REROLL", SubmitReroll);
            RectTransform rerollRect = _rerollButton.GetComponent<RectTransform>();
            rerollRect.anchorMin = new Vector2(0.5f, 0.5f);
            rerollRect.anchorMax = new Vector2(0.5f, 0.5f);
            rerollRect.pivot = new Vector2(0.5f, 0.5f);
            rerollRect.sizeDelta = new Vector2(303f, 111f);
            rerollRect.anchoredPosition = new Vector2(0f, -16f);
            var rerollLE = _rerollButton.GetComponent<LayoutElement>();
            if (rerollLE != null)
            {
                rerollLE.preferredWidth = 303f;
                rerollLE.preferredHeight = 111f;
                rerollLE.ignoreLayout = true;
            }
            var rerollLabel = _rerollButton.GetComponentInChildren<Text>();
            if (rerollLabel != null) rerollLabel.fontSize = 44;

            // Reroll icon — anchored just right of the button. Button is
            // centered with half-width 151.5; gap 24 puts the icon at +176.
            // Sprite goes through the shared icon registry so all menus pull
            // from the same cache.
            NightDash.Runtime.UI.NightDashUIIcons.Attach(
                footer,
                NightDash.Runtime.UI.NightDashUIIcons.Reroll,
                new Vector2(64f, 64f),
                new Vector2(176f + 32f, -16f), // +32 to keep visual center; Attach uses center pivot
                "RerollIcon");

            // "Rerolls Left: N" text — sits to the right of the icon (icon
            // width 64 + gap 14 = 78 → text starts at +254 from center).
            _rerollText = CreateText(footer, "Rerolls Left: 1", 32, TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.94f, 1f));
            RectTransform rerollTextRect = _rerollText.rectTransform;
            rerollTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            rerollTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            rerollTextRect.pivot = new Vector2(0f, 0.5f);
            rerollTextRect.sizeDelta = new Vector2(340f, 60f);
            rerollTextRect.anchoredPosition = new Vector2(254f, -16f);
            _rerollText.horizontalOverflow = HorizontalWrapMode.Overflow;
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
                string body = BuildOptionText(option);
                _optionTexts[i].text = body;
                _optionTexts[i].fontSize = FontSizeForBody(body);
                if (_optionKindTexts[i] != null)
                {
                    _optionKindTexts[i].text = BuildOptionKindLabel(option);
                }
                if (_optionIconImages[i] != null)
                {
                    ApplyOptionIcon(_optionIconImages[i], option);
                }
                // Adjust the ScrollRect's Content height to the Text's
                // actual rendered height so short descriptions fit and long
                // ones become scrollable.
                ResizeOptionContent(i);
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

        // Description bodies must render at a single uniform size — the
        // earlier length-based shrink (22..40pt) made adjacent cards look
        // mismatched and, worse, hid overflow because longer text was
        // silently downscaled until it fit the host. With a fixed 28pt the
        // descHost (sized for exactly 3 lines) will overflow on line 4+
        // and the auto-scroll tick will engage.
        private const int OptionBodyFontSize = 28;
        private static int FontSizeForBody(string body) => OptionBodyFontSize;

        // Used by the card's TOP slot — just the upgrade category in upper
        // case. Lives separately so long body text never pushes the label
        // out of frame.
        private static string BuildOptionKindLabel(UpgradeOptionElement option)
        {
            return option.Kind.ToString().ToUpperInvariant();
        }

        // Sizes the Text rect inside its DescHost mask so the wrapped text
        // is fully drawn. The host clips with RectMask2D and TickAutoScroll
        // slides the Text rect vertically when the text exceeds the host.
        private void ResizeOptionContent(int index)
        {
            RectTransform host = _optionContentRects[index];
            Text text = _optionTexts[index];
            if (host == null || text == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(host);

            float preferred = Mathf.Max(text.preferredHeight, host.rect.height);
            var tr = text.rectTransform;
            tr.sizeDelta = new Vector2(-8f, preferred);
            tr.anchoredPosition = Vector2.zero; // reset to top
        }

        // Resolves an icon sprite for an upgrade option and applies it to
        // the per-card image. Falls back to a category-specific placeholder
        // when the registry doesn't have a hand-authored icon yet.
        private static void ApplyOptionIcon(Image img, UpgradeOptionElement option)
        {
            string id = option.Id.ToString();
            Sprite sprite = null;

            DataRegistry registry = DataRegistry.Instance;
            if (registry != null)
            {
                switch (option.Kind)
                {
                    case UpgradeKind.Weapon:
                        if (registry.TryGetWeapon(id, out NightDash.Data.WeaponData wd) && wd != null && wd.icon != null)
                            sprite = wd.icon;
                        break;
                    case UpgradeKind.Passive:
                        if (registry.TryGetPassive(id, out NightDash.Data.PassiveData pd) && pd != null && pd.icon != null)
                            sprite = pd.icon;
                        if (sprite == null)
                            sprite = NightDash.Runtime.UI.NightDashUIIcons.GetPassive(id);
                        break;
                }
            }

            if (sprite == null)
            {
                // Placeholder per category — keeps the slot from looking
                // empty until the real PixelLab art lands.
                string fallbackKey = option.Kind == UpgradeKind.Weapon
                    ? NightDash.Runtime.UI.NightDashUIIcons.Reroll       // sword-ish swap icon
                    : NightDash.Runtime.UI.NightDashUIIcons.UnlockRelic; // generic relic for passive
                sprite = NightDash.Runtime.UI.NightDashUIIcons.Get(fallbackKey);
            }

            img.sprite = sprite;
            img.enabled = sprite != null;
            if (sprite == null)
            {
                // Last-resort visual: solid dark block tinted with category
                // color so the card still reads as "icon here".
                img.enabled = true;
                img.color = option.Kind == UpgradeKind.Weapon
                    ? new Color(0.42f, 0.20f, 0.20f, 0.9f)
                    : new Color(0.30f, 0.22f, 0.34f, 0.9f);
            }
            else
            {
                // Restore neutral tint when we have a sprite — placeholder
                // dummies (Reroll/UnlockRelic) get a slight desat so the
                // player can tell them apart from real art.
                bool isPlaceholder = option.Kind == UpgradeKind.Passive
                    ? (NightDash.Runtime.UI.NightDashUIIcons.GetPassive(id) == null)
                    : !(registry != null
                        && registry.TryGetWeapon(id, out var w)
                        && w != null && w.icon != null);
                img.color = isPlaceholder
                    ? new Color(0.82f, 0.78f, 0.70f, 0.9f)
                    : Color.white;
            }
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
            // Outline on every level-up text so the glyphs stay readable on
            // top of the card frame's interior + the gameplay backdrop.
            AddTextOutline(text);
            return text;
        }

        // Drops a dark Outline on a Text so the glyphs stay readable on top
        // of bright card centers (the legendary card's gold glow in
        // particular washes out white text without it).
        private static void AddTextOutline(Text text)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
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
