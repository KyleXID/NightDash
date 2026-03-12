using System;
using NightDash.ECS.Components;
using UnityEngine;
using Unity.Entities;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightDash.Runtime
{
    public sealed class NightDashHudResultUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private bool debugForceHudVisible = false;
        [SerializeField] private bool debugForceResultVisible = false;

        [Header("Icons")]
        [SerializeField] private Texture2D hpIcon;
        [SerializeField] private Texture2D shieldIcon;
        [SerializeField] private Texture2D xpIcon;
        [SerializeField] private Texture2D goldIcon;
        [SerializeField] private Texture2D soulIcon;
        [SerializeField] private Texture2D timerIcon;
        [SerializeField] private Texture2D waveIcon;
        [SerializeField] private Texture2D potionIcon;
        [SerializeField] private Texture2D dashIcon;
        [SerializeField] private Texture2D interactIcon;
        [SerializeField] private Texture2D victoryIcon;
        [SerializeField] private Texture2D defeatIcon;
        [SerializeField] private Texture2D warningIcon;
        [SerializeField] private Texture2D buttonDefaultTexture;
        [SerializeField] private Texture2D buttonHoverTexture;
        [SerializeField] private Texture2D buttonPressedTexture;
        [SerializeField] private Texture2D buttonDisabledTexture;

        private RunSelectionLobbyUI _lobbyUi;
        private GameObject _hudRoot;
        private GameObject _resultRoot;
        private GameObject _rewardRoot;
        private GameObject _shieldRow;

        private RectTransform _hpFill;
        private RectTransform _shieldFill;
        private RectTransform _xpFill;
        private Text _hpText;
        private Text _shieldText;
        private Text _xpText;
        private Text _timerText;
        private Text _waveText;
        private Text _goldText;
        private Text _soulText;
        private Text _resultTimeText;
        private Text _resultKillText;
        private Text _resultGoldText;
        private Text _resultSoulText;
        private Text _resultHeaderText;
        private Image _resultOutcomeIcon;
        private Text _rewardHeaderText;
        private Text _rewardBodyText;
        private Button _rewardConfirmButton;
        private Button _retryButton;
        private Button _metaButton;
        private Button _menuButton;

        private bool _hudEnabled;
        private bool _resultEnabled;
        private bool _rewardEnabled;
        private bool _resultVictory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashHudResultUI>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var go = new GameObject("NightDashHudResultUI");
            go.AddComponent<NightDashHudResultUI>();
        }

        private void Awake()
        {
            _lobbyUi = FindFirstObjectByType<RunSelectionLobbyUI>(FindObjectsInactive.Include);
            LoadFallbackIcons();
            EnsureEventSystem();
            BuildCanvas();
        }

        private void Update()
        {
            UpdateFromGameState();
            UpdateVisibility();
        }

        private void LoadFallbackIcons()
        {
            hpIcon = hpIcon != null ? hpIcon : LoadIcon("nd_ui_icon_hp_heart_default");
            shieldIcon = shieldIcon != null ? shieldIcon : LoadIcon("nd_ui_icon_shield_default");
            xpIcon = xpIcon != null ? xpIcon : LoadIcon("nd_ui_icon_xp_star_default");
            goldIcon = goldIcon != null ? goldIcon : LoadIcon("nd_ui_icon_gold_coin_default");
            soulIcon = soulIcon != null ? soulIcon : LoadIcon("nd_ui_icon_soul_orb_default");
            timerIcon = timerIcon != null ? timerIcon : LoadIcon("nd_ui_icon_timer_hourglass_default");
            waveIcon = waveIcon != null ? waveIcon : LoadIcon("nd_ui_icon_wave_skull_default");
            potionIcon = potionIcon != null ? potionIcon : LoadIcon("nd_ui_icon_potion_default");
            dashIcon = dashIcon != null ? dashIcon : LoadIcon("nd_ui_icon_dash_default");
            interactIcon = interactIcon != null ? interactIcon : LoadIcon("nd_ui_icon_interact_key_default");
            victoryIcon = victoryIcon != null ? victoryIcon : LoadIcon("nd_ui_icon_victory_crown_default");
            defeatIcon = defeatIcon != null ? defeatIcon : LoadIcon("nd_ui_icon_defeat_skull_default");
            warningIcon = warningIcon != null ? warningIcon : LoadIcon("nd_ui_icon_warning_default");
            buttonDefaultTexture = buttonDefaultTexture != null ? buttonDefaultTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_default");
            buttonHoverTexture = buttonHoverTexture != null ? buttonHoverTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_hover");
            buttonPressedTexture = buttonPressedTexture != null ? buttonPressedTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_pressed");
            buttonDisabledTexture = buttonDisabledTexture != null ? buttonDisabledTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_disabled");

            buttonDefaultTexture = CropButtonFrameTexture(buttonDefaultTexture);
            buttonHoverTexture = CropButtonFrameTexture(buttonHoverTexture);
            buttonPressedTexture = CropButtonFrameTexture(buttonPressedTexture);
            buttonDisabledTexture = CropButtonFrameTexture(buttonDisabledTexture);
        }

        private static Texture2D LoadIcon(string iconName)
        {
            return Resources.Load<Texture2D>($"NightDash/UI/Icons/{iconName}");
        }

        private void BuildCanvas()
        {
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

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

            _hudRoot = CreateRect("HUDRoot", transform).gameObject;
            _rewardRoot = CreateRect("RewardRoot", transform).gameObject;
            _resultRoot = CreateRect("ResultRoot", transform).gameObject;
            StretchFull((RectTransform)_hudRoot.transform);
            StretchFull((RectTransform)_rewardRoot.transform);
            StretchFull((RectTransform)_resultRoot.transform);

            BuildHud(_hudRoot.transform);
            BuildReward(_rewardRoot.transform);
            BuildResult(_resultRoot.transform);
            UpdateVisibility();
        }

        private void BuildHud(Transform parent)
        {
            RectTransform topLeft = CreatePanel("TopLeftPanel", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(440f, 140f));
            VerticalLayoutGroup topLeftLayout = topLeft.gameObject.AddComponent<VerticalLayoutGroup>();
            topLeftLayout.spacing = 4f;
            topLeftLayout.padding = new RectOffset(10, 10, 10, 10);
            topLeftLayout.childControlHeight = true;
            topLeftLayout.childControlWidth = true;

            _hpText = CreateIconBarRow(topLeft, "HP", hpIcon, new Color(0.76f, 0.14f, 0.22f, 1f), out _hpFill);
            _shieldText = CreateIconBarRow(topLeft, "Shield", shieldIcon, new Color(0.39f, 0.39f, 0.84f, 1f), out _shieldFill);
            _shieldRow = _shieldText != null ? _shieldText.transform.parent.gameObject : null;
            _xpText = CreateIconBarRow(topLeft, "XP", xpIcon, new Color(0.77f, 0.66f, 0.23f, 1f), out _xpFill);

            RectTransform topCenter = CreatePanel("TopCenterPanel", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(360f, 86f));
            HorizontalLayoutGroup centerLayout = topCenter.gameObject.AddComponent<HorizontalLayoutGroup>();
            centerLayout.spacing = 14f;
            centerLayout.padding = new RectOffset(10, 10, 10, 10);
            centerLayout.childAlignment = TextAnchor.MiddleCenter;
            centerLayout.childControlHeight = false;
            centerLayout.childControlWidth = false;

            _waveText = CreateIconCounter(topCenter, waveIcon, "Wave 01", 48f, 48f);
            _timerText = CreateIconCounter(topCenter, timerIcon, "00:00", 48f, 48f);

            RectTransform topRight = CreatePanel("TopRightPanel", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(260f, 102f));
            VerticalLayoutGroup rightLayout = topRight.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.spacing = 3f;
            rightLayout.padding = new RectOffset(10, 10, 8, 8);
            rightLayout.childControlHeight = true;
            rightLayout.childControlWidth = true;

            _goldText = CreateIconCounter(topRight, goldIcon, "Gold 0000", 32f, 32f);
            _soulText = CreateIconCounter(topRight, soulIcon, "Souls 000", 32f, 32f);

            RectTransform bottomLeft = CreatePanel("BottomLeftPanel", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(270f, 84f));
            HorizontalLayoutGroup leftBottomLayout = bottomLeft.gameObject.AddComponent<HorizontalLayoutGroup>();
            leftBottomLayout.spacing = 8f;
            leftBottomLayout.padding = new RectOffset(10, 10, 10, 10);
            leftBottomLayout.childAlignment = TextAnchor.MiddleLeft;
            leftBottomLayout.childControlHeight = false;
            leftBottomLayout.childControlWidth = false;

            CreateSimpleIcon(bottomLeft, potionIcon, 52f, 52f);
            CreateSimpleIcon(bottomLeft, dashIcon, 52f, 52f);
            CreateSimpleIcon(bottomLeft, interactIcon, 52f, 52f);

            RectTransform bottomCenter = CreatePanel("BottomCenterPanel", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(250f, 70f));
            HorizontalLayoutGroup bottomCenterLayout = bottomCenter.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomCenterLayout.spacing = 10f;
            bottomCenterLayout.padding = new RectOffset(10, 10, 10, 10);
            bottomCenterLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomCenterLayout.childControlHeight = false;
            bottomCenterLayout.childControlWidth = false;
            CreateSimpleIcon(bottomCenter, warningIcon, 48f, 48f);
            CreateSimpleIcon(bottomCenter, defeatIcon, 48f, 48f);
            CreateSimpleIcon(bottomCenter, victoryIcon, 48f, 48f);
        }

        private void BuildResult(Transform parent)
        {
            RectTransform dim = CreateRect("Dim", parent);
            StretchFull(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform panel = CreatePanel("ResultPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 760f));
            VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(24, 24, 20, 20);
            panelLayout.spacing = 2f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandHeight = false;

            RectTransform iconRow = CreateRect("OutcomeIconRow", panel);
            HorizontalLayoutGroup iconLayout = iconRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            iconLayout.spacing = 0f;
            iconLayout.childAlignment = TextAnchor.MiddleCenter;
            iconLayout.childControlHeight = false;
            iconLayout.childControlWidth = false;
            SetPreferredHeight(iconRow, 268f);
            _resultOutcomeIcon = CreateSimpleIcon(iconRow, defeatIcon, 260f, 260f);

            RectTransform headerRow = CreateRect("OutcomeHeaderRow", panel);
            SetPreferredHeight(headerRow, 96f);
            LayoutElement headerRowLayout = headerRow.gameObject.AddComponent<LayoutElement>();
            headerRowLayout.preferredHeight = 96f;

            _resultHeaderText = CreateText(headerRow, "YOU DIED", 56, TextAnchor.MiddleCenter, new Color(0.72f, 0.12f, 0.2f, 1f));
            StretchFull(_resultHeaderText.rectTransform);

            RectTransform stats = CreatePanel("Stats", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 208f));
            VerticalLayoutGroup statsLayout = stats.gameObject.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 0f;
            statsLayout.padding = new RectOffset(14, 14, 4, 4);
            statsLayout.childControlHeight = true;
            statsLayout.childControlWidth = true;
            SetPreferredHeight(stats, 208f);

            _resultTimeText = CreateStatRow(stats, timerIcon, "Time Survived", "00:00");
            _resultKillText = CreateStatRow(stats, waveIcon, "Kills", "0");
            _resultGoldText = CreateStatRow(stats, goldIcon, "Gold Earned", "0");
            _resultSoulText = CreateStatRow(stats, soulIcon, "Souls Earned", "0");

            RectTransform spacer = CreateRect("BottomSpacer", panel);
            LayoutElement spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLayout.flexibleHeight = 1f;
            spacerLayout.preferredHeight = 1f;

            RectTransform actions = CreateRect("Actions", panel);
            HorizontalLayoutGroup actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 14f;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = false;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferredHeight(actions, 62f);

            _retryButton = CreateActionButton(actions, "Retry", () => SubmitNavigation(RunNavigationAction.Retry));
            _metaButton = CreateActionButton(actions, "Meta", () => SubmitNavigation(RunNavigationAction.ReturnToLobby));
            _menuButton = CreateActionButton(actions, "Menu", () => SubmitNavigation(RunNavigationAction.ReturnToLobby));
        }

        private void BuildReward(Transform parent)
        {
            RectTransform dim = CreateRect("RewardDim", parent);
            StretchFull(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.7f);

            RectTransform panel = CreatePanel("RewardPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 380f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            _rewardHeaderText = CreateText(panel, "BOSS REWARD", 34, TextAnchor.MiddleCenter, Color.white);
            SetPreferredHeight(_rewardHeaderText.rectTransform, 48f);

            _rewardBodyText = CreateText(panel, "Evolution check pending.", 22, TextAnchor.UpperLeft, new Color(0.92f, 0.88f, 0.97f, 1f));
            SetPreferredHeight(_rewardBodyText.rectTransform, 180f);

            RectTransform actions = CreateRect("RewardActions", panel);
            HorizontalLayoutGroup actionsLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferredHeight(actions, 74f);

            _rewardConfirmButton = CreateActionButton(actions, "Confirm", SubmitBossRewardConfirm);
        }

        private void UpdateFromGameState()
        {
            RunStatus status = RunStatus.Loading;
            bool hasLoop = false;
            bool runActive = false;
            bool snapshotValid = false;
            bool navigationPending = false;
            bool rewardPending = false;
            bool snapshotVictory = false;
            float elapsedTime = 0f;
            int level = 1;
            float experience = 0f;
            float nextLevelExperience = 1f;
            float hpCurrent = 0f;
            float hpMax = 1f;
            int currentWave = 1;
            int kills = 0;
            int gold = 0;
            int souls = 0;
            int rewardGranted = 0;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                EntityManager em = world.EntityManager;

                EntityQuery loopQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GameLoopState>());
                if (!loopQuery.IsEmptyIgnoreFilter)
                {
                    GameLoopState loop = loopQuery.GetSingleton<GameLoopState>();
                    hasLoop = true;
                    status = loop.Status;
                    runActive = loop.IsRunActive == 1 && loop.Status == RunStatus.Playing;
                    elapsedTime = loop.ElapsedTime;
                    level = loop.Level;
                    experience = loop.Experience;
                    nextLevelExperience = Mathf.Max(1f, loop.NextLevelExperience);
                }
                loopQuery.Dispose();

                EntityQuery resultQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RunResultStats>());
                if (!resultQuery.IsEmptyIgnoreFilter)
                {
                    RunResultStats result = resultQuery.GetSingleton<RunResultStats>();
                    currentWave = Mathf.Max(1, result.CurrentWave);
                    kills = result.KillCount;
                    gold = result.GoldEarned;
                    souls = result.SoulsEarned;
                }
                resultQuery.Dispose();

                EntityQuery snapshotQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ResultSnapshot>());
                if (!snapshotQuery.IsEmptyIgnoreFilter)
                {
                    ResultSnapshot snapshot = snapshotQuery.GetSingleton<ResultSnapshot>();
                    snapshotValid = snapshot.HasSnapshot == 1;
                    if (snapshotValid)
                    {
                        snapshotVictory = snapshot.IsVictory == 1;
                        elapsedTime = snapshot.ElapsedTime;
                        level = snapshot.FinalLevel;
                        kills = snapshot.KillCount;
                        gold = snapshot.GoldEarned;
                        souls = snapshot.SoulsEarned;
                        rewardGranted = snapshot.RewardGranted;
                    }
                }
                snapshotQuery.Dispose();

                EntityQuery rewardQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<BossRewardState>(),
                    ComponentType.ReadOnly<EvolutionState>());
                if (!rewardQuery.IsEmptyIgnoreFilter)
                {
                    BossRewardState reward = rewardQuery.GetSingleton<BossRewardState>();
                    EvolutionState evolution = rewardQuery.GetSingleton<EvolutionState>();
                    rewardPending = reward.HasPendingReward == 1;

                    if (_rewardHeaderText != null)
                    {
                        _rewardHeaderText.text = evolution.HasAbyssEvolution == 1
                            ? "ABYSS EVOLUTION"
                            : evolution.HasNormalEvolution == 1 ? "EVOLUTION UNLOCKED" : "BOSS REWARD";
                    }

                    if (_rewardBodyText != null)
                    {
                        _rewardBodyText.text = evolution.HasAbyssEvolution == 1
                            ? "Abyss evolution conditions were met. Confirm to claim the reward and continue."
                            : evolution.HasNormalEvolution == 1
                                ? "A weapon evolution condition was met. Confirm to claim the reward and continue."
                                : "Boss reward acquired. Confirm to continue to the result screen.";
                    }
                }
                rewardQuery.Dispose();

                EntityQuery navigationQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RunNavigationRequest>());
                if (!navigationQuery.IsEmptyIgnoreFilter)
                {
                    navigationPending = navigationQuery.GetSingleton<RunNavigationRequest>().IsPending == 1;
                }
                navigationQuery.Dispose();

                EntityQuery playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadOnly<CombatStats>());
                if (!playerQuery.IsEmptyIgnoreFilter)
                {
                    var players = playerQuery.ToComponentDataArray<CombatStats>(Unity.Collections.Allocator.Temp);
                    for (int i = 0; i < players.Length; i++)
                    {
                        hpCurrent = players[i].CurrentHealth;
                        hpMax = Mathf.Max(1f, players[i].MaxHealth);
                    }
                    players.Dispose();
                }
                playerQuery.Dispose();
            }

            if (debugForceResultVisible)
            {
                _hudEnabled = false;
                _rewardEnabled = false;
                _resultEnabled = true;
            }
            else if (debugForceHudVisible)
            {
                _hudEnabled = true;
                _rewardEnabled = false;
                _resultEnabled = false;
            }
            else if (status == RunStatus.Playing)
            {
                _hudEnabled = true;
                _rewardEnabled = false;
                _resultEnabled = false;
            }
            else if (status == RunStatus.LevelUpSelection)
            {
                _hudEnabled = false;
                _rewardEnabled = false;
                _resultEnabled = false;
            }
            else if (status == RunStatus.Victory && rewardPending)
            {
                _hudEnabled = false;
                _rewardEnabled = true;
                _resultEnabled = false;
            }
            else if (status == RunStatus.Result || snapshotValid)
            {
                _hudEnabled = false;
                _rewardEnabled = false;
                _resultEnabled = true;
            }
            else
            {
                _hudEnabled = false;
                _rewardEnabled = false;
                _resultEnabled = false;
            }

            _resultVictory = snapshotValid
                ? snapshotVictory
                : status == RunStatus.Victory || (status == RunStatus.Result && hpCurrent > 0.01f);

            int sec = Mathf.Max(0, Mathf.FloorToInt(elapsedTime));
            int min = sec / 60;
            int remain = sec % 60;

            float hp01 = hpMax > 0f ? Mathf.Clamp01(hpCurrent / hpMax) : 0f;
            float xp01 = Mathf.Clamp01(experience / nextLevelExperience);

            SetFill(_hpFill, hp01);
            SetFill(_xpFill, xp01);

            _hpText.text = $"HP {(int)hpCurrent}/{(int)hpMax}";
            _xpText.text = $"Lv {level}  XP {(int)(xp01 * 100f)}%";
            _timerText.text = $"{min:00}:{remain:00}";
            _waveText.text = $"Wave {currentWave:00}";
            _goldText.text = $"Gold {gold}";
            _soulText.text = $"Souls {souls}";
            _resultTimeText.text = $"{min:00}:{remain:00}";
            _resultKillText.text = $"{kills}";
            _resultGoldText.text = rewardGranted > 0 ? $"{gold} (+{rewardGranted})" : $"{gold}";
            _resultSoulText.text = $"{souls}";

            if (_resultHeaderText != null)
            {
                _resultHeaderText.text = _resultVictory ? "VICTORY" : "YOU DIED";
                _resultHeaderText.color = _resultVictory
                    ? new Color(0.79f, 0.66f, 0.21f, 1f)
                    : new Color(0.72f, 0.12f, 0.2f, 1f);
            }

            if (_resultOutcomeIcon != null)
            {
                Texture2D resultTexture = _resultVictory ? victoryIcon : defeatIcon;
                if (resultTexture != null)
                {
                    _resultOutcomeIcon.sprite = Sprite.Create(
                        resultTexture,
                        new Rect(0f, 0f, resultTexture.width, resultTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    _resultOutcomeIcon.color = Color.white;
                }
            }

            if (_retryButton != null)
            {
                _retryButton.interactable = !navigationPending;
            }

            if (_metaButton != null)
            {
                _metaButton.interactable = !navigationPending;
            }

            if (_menuButton != null)
            {
                _menuButton.interactable = !navigationPending;
            }

            if (_rewardConfirmButton != null)
            {
                _rewardConfirmButton.interactable = !navigationPending;
            }
        }

        private void UpdateVisibility()
        {
            bool titleOrLobbyVisible = false;
            if (_lobbyUi != null)
            {
                titleOrLobbyVisible = _lobbyUi.IsTitleVisible || _lobbyUi.IsLobbyVisible;
            }

            bool showResult = _resultEnabled && !titleOrLobbyVisible;
            bool showReward = _rewardEnabled && !showResult && !titleOrLobbyVisible;
            bool showHud = _hudEnabled && !showResult && !showReward && !titleOrLobbyVisible;

            if (_hudRoot != null)
            {
                _hudRoot.SetActive(showHud);
            }

            if (_shieldRow != null)
            {
                _shieldRow.SetActive(false);
            }

            if (_rewardRoot != null)
            {
                _rewardRoot.SetActive(showReward);
            }

            if (_resultRoot != null)
            {
                _resultRoot.SetActive(showResult);
            }
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x >= 0.5f ? 1f : 0f, anchorMin.y >= 0.5f ? 1f : 0f);
            if (anchorMin == new Vector2(0.5f, 0.5f))
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0.11f, 0.07f, 0.16f, 0.76f);
            return rect;
        }

        private Text CreateIconBarRow(RectTransform parent, string label, Texture2D icon, Color fillColor, out RectTransform fillRect)
        {
            RectTransform row = CreateRect($"{label}Row", parent);
            SetPreferredHeight(row, 33f);

            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            CreateSimpleIcon(row, icon, 30f, 30f);

            RectTransform barBg = CreateRect($"{label}BarBg", row);
            barBg.sizeDelta = new Vector2(240f, 20f);
            LayoutElement barLayout = barBg.gameObject.AddComponent<LayoutElement>();
            barLayout.preferredWidth = 240f;
            barLayout.preferredHeight = 20f;
            Image bg = barBg.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            fillRect = CreateRect($"{label}BarFill", barBg);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.8f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = fillColor;

            Text value = CreateText(row, $"{label} 0", 15, TextAnchor.MiddleLeft, new Color(0.92f, 0.9f, 0.96f, 1f));
            SetPreferredWidth(value.rectTransform, 124f);
            return value;
        }

        private Text CreateIconCounter(RectTransform parent, Texture2D icon, string value, float iconW, float iconH)
        {
            RectTransform row = CreateRect("CounterRow", parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetPreferredHeight(row, iconH + 2f);

            CreateSimpleIcon(row, icon, iconW, iconH);
            Text text = CreateText(row, value, 20, TextAnchor.MiddleLeft, new Color(0.93f, 0.88f, 0.96f, 1f));
            return text;
        }

        private Text CreateStatRow(RectTransform parent, Texture2D icon, string label, string value)
        {
            RectTransform row = CreateRect($"{label}Row", parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 1f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferredHeight(row, 44f);

            CreateSimpleIcon(row, icon, 46f, 46f);
            Text labelText = CreateText(row, label, 16, TextAnchor.MiddleLeft, new Color(0.68f, 0.62f, 0.73f, 1f));
            SetPreferredWidth(labelText.rectTransform, 220f);
            Text valueText = CreateText(row, value, 16, TextAnchor.MiddleLeft, new Color(0.93f, 0.88f, 0.97f, 1f));
            return valueText;
        }

        private Button CreateActionButton(RectTransform parent, string label, Action onClick)
        {
            RectTransform rect = CreateRect($"{label}Button", parent);
            rect.sizeDelta = new Vector2(240f, 72f);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 240f;
            layout.preferredHeight = 72f;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.sprite = CreateRuntimeSprite(buttonDefaultTexture);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = CreateRuntimeSprite(buttonHoverTexture),
                pressedSprite = CreateRuntimeSprite(buttonPressedTexture),
                disabledSprite = CreateRuntimeSprite(buttonDisabledTexture)
            };
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = CreateText(rect, label.ToUpperInvariant(), 24, TextAnchor.MiddleCenter, new Color(0.95f, 0.91f, 0.98f, 1f));
            StretchFull(text.rectTransform);
            return button;
        }

        private Image CreateSimpleIcon(RectTransform parent, Texture2D texture, float width, float height)
        {
            RectTransform rect = CreateRect("Icon", parent);
            rect.sizeDelta = new Vector2(width, height);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            Image image = rect.gameObject.AddComponent<Image>();
            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                image.color = new Color(0.45f, 0.15f, 0.2f, 1f);
            }
            return image;
        }

        private static Sprite CreateRuntimeSprite(Texture2D texture)
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

        private static Texture2D CropButtonFrameTexture(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            int x = Mathf.RoundToInt(source.width * 0.07f);
            int y = Mathf.RoundToInt(source.height * 0.29f);
            int w = Mathf.RoundToInt(source.width * 0.86f);
            int h = Mathf.RoundToInt(source.height * 0.42f);

            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.ReadPixels(new Rect(x, y, w, h), 0, 0);
            cropped.Apply(false, true);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return cropped;
        }

        private static void SetFill(RectTransform rect, float value01)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            layout.minHeight = height;
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
                if (es.GetComponent<StandaloneInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                }
            }
        }

        private static void SubmitNavigation(RunNavigationAction action)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<RunNavigationRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            entityManager.SetComponentData(singleton, new RunNavigationRequest
            {
                Action = action,
                IsPending = 1
            });
        }

        private static void SubmitBossRewardConfirm()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<BossRewardConfirmRequest>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            entityManager.SetComponentData(singleton, new BossRewardConfirmRequest
            {
                IsPending = 1
            });
        }
    }
}
