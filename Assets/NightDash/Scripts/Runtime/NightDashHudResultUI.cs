using System;
using NightDash.ECS.Components;
using NightDash.Runtime.UI;
using UnityEngine;
using Unity.Entities;
using UnityEngine.EventSystems;
// using UnityEngine.SceneManagement; — removed once Retry stopped reloading the scene.
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
        [SerializeField] private Texture2D hitIcon;
        [SerializeField] private Texture2D critIcon;
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
        private bool _shieldVisible;

        private RectTransform _hpFill;
        private RectTransform _shieldFill;
        private RectTransform _xpFill;
        private Text _hpText;
        private Text _shieldText;
        private Text _xpText;
        private Text _hitText;
        private Text _critText;
        private Text _dashText;
        private Text _potionText;
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
        private Button _returnToLobbyButton;
        private Button _returnToTitleButton;

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
            hitIcon = hitIcon != null ? hitIcon : LoadIcon("nd_ui_icon_hit_default");
            critIcon = critIcon != null ? critIcon : LoadIcon("nd_ui_icon_crit_default");
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

            _waveText = CreateIconCounter(topCenter, waveIcon, "Wave 01", 64f, 64f);
            _timerText = CreateIconCounter(topCenter, timerIcon, "00:00", 64f, 64f);

            RectTransform topRight = CreatePanel("TopRightPanel", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(260f, 102f));
            VerticalLayoutGroup rightLayout = topRight.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.spacing = 3f;
            rightLayout.padding = new RectOffset(10, 10, 8, 8);
            rightLayout.childControlHeight = true;
            rightLayout.childControlWidth = true;

            _goldText = CreateIconCounter(topRight, goldIcon, "Gold 0000", 48f, 48f);
            _soulText = CreateIconCounter(topRight, soulIcon, "Souls 000", 48f, 48f);

            RectTransform bottomLeft = CreatePanel("BottomLeftPanel", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(820f, 124f));
            // No backdrop here — text outlines carry legibility against the
            // gameplay scene below.

            HorizontalLayoutGroup leftBottomLayout = bottomLeft.gameObject.AddComponent<HorizontalLayoutGroup>();
            // 28px between each stat group reads as "separate" pairs instead
            // of one long crowded strip.
            leftBottomLayout.spacing = 28f;
            leftBottomLayout.padding = new RectOffset(20, 20, 14, 14);
            leftBottomLayout.childAlignment = TextAnchor.MiddleLeft;
            leftBottomLayout.childControlHeight = false;
            leftBottomLayout.childControlWidth = false;

            // Combat stat readouts: each is an icon + value pair laid out
            // by CreateIconCounter (HorizontalLayoutGroup inside). The
            // outer spacing keeps the pairs visually grouped.
            _hitText = CreateIconCounter(bottomLeft, hitIcon, "DMG 0", 56f, 56f);
            SetPreferredWidth(_hitText.rectTransform, 140f);
            _critText = CreateIconCounter(bottomLeft, critIcon, "CRIT 0%", 56f, 56f);
            SetPreferredWidth(_critText.rectTransform, 160f);
            _dashText = CreateIconCounter(bottomLeft, dashIcon, "READY", 56f, 56f);
            SetPreferredWidth(_dashText.rectTransform, 150f);
            _potionText = CreateIconCounter(bottomLeft, potionIcon, "x3", 52f, 52f);
            SetPreferredWidth(_potionText.rectTransform, 90f);
            // interactIcon was rendered here as a key-hint placeholder, but
            // no interactable entity system existed to drive it. Removed
            // pending the Stage interactables feature (chests / altars / NPCs)
            // — bring it back as a contextual "E to interact" prompt that
            // only appears when the player is near an interactable.

            // BottomCenterPanel was a 3-icon placeholder (warning/defeat/
            // victory) from an earlier prototype with no gameplay binding.
            // Removed pending a real bottom-center widget design. Restore
            // here once we have purposeful content (e.g. boss alert,
            // objective marker, mini-event prompt).
        }

        private void BuildResult(Transform parent)
        {
            RectTransform dim = CreateRect("Dim", parent);
            StretchFull(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform panel = CreatePanel("ResultPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 760f));
            AttachOrnatePanelFrame(panel);
            VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(56, 56, 56, 56);
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

            _resultHeaderText = CreateText(headerRow, "YOU DIED", 96, TextAnchor.MiddleCenter, new Color(0.72f, 0.12f, 0.2f, 1f));
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

            // Three-action footer: Retry runs the same stage again, Lobby
            // returns to character / stage select, Title goes all the way
            // back to the main menu. (Quit lives on the Title screen.)
            _retryButton = CreateActionButton(actions, "Retry", RequestRetry);
            _returnToLobbyButton = CreateActionButton(actions, "Lobby", RequestReturnToLobby);
            _returnToTitleButton = CreateActionButton(actions, "Title", RequestReturnToTitle);
        }

        private void BuildReward(Transform parent)
        {
            RectTransform dim = CreateRect("RewardDim", parent);
            StretchFull(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.7f);

            RectTransform panel = CreatePanel("RewardPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 380f));
            AttachOrnatePanelFrame(panel);
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 40, 40);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            _rewardHeaderText = CreateText(panel, "BOSS REWARD", 48, TextAnchor.MiddleCenter, Color.white);
            SetPreferredHeight(_rewardHeaderText.rectTransform, 48f);

            _rewardBodyText = CreateText(panel, "Evolution check pending.", 32, TextAnchor.UpperLeft, new Color(0.92f, 0.88f, 0.97f, 1f));
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
            float shieldCurrent = 0f;
            float shieldMax = 0f;
            float playerDamage = 0f;
            float critChance = 0f;
            float dashCooldown = 0f;
            int potionCount = 0;
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
                        shieldCurrent = players[i].CurrentShield;
                        shieldMax = players[i].MaxShield;
                        playerDamage = players[i].Damage;
                        critChance = players[i].CritChance;
                        dashCooldown = players[i].DashCooldownRemaining;
                        potionCount = players[i].PotionCount;
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

            // Shield ratio + text. Hidden via _shieldVisible when MaxShield
            // is 0 (no shield buffer configured for this class yet).
            _shieldVisible = shieldMax > 0f;
            if (_shieldVisible)
            {
                float shield01 = Mathf.Clamp01(shieldCurrent / shieldMax);
                SetFill(_shieldFill, shield01);
                if (_shieldText != null)
                {
                    _shieldText.text = $"SHIELD {(int)shieldCurrent}/{(int)shieldMax}";
                }
            }

            _hpText.text = $"HP {(int)hpCurrent}/{(int)hpMax}";
            _xpText.text = $"Lv {level}  XP {(int)(xp01 * 100f)}%";
            if (_hitText != null) _hitText.text = $"DMG {(int)playerDamage}";
            if (_critText != null) _critText.text = $"CRIT {(int)(critChance * 100)}%";
            if (_dashText != null)
            {
                _dashText.text = dashCooldown <= 0.01f ? "READY" : $"{dashCooldown:0.0}s";
            }
            if (_potionText != null) _potionText.text = $"x{potionCount}";
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
                    // The two outcome sprites have very different aspect
                    // ratios (defeat_skull 36×55 vs victory_crown 55×52).
                    // preserveAspect prevents the 260×260 result frame from
                    // squashing them into the wrong shape.
                    _resultOutcomeIcon.preserveAspect = true;
                    _resultOutcomeIcon.color = Color.white;
                }
            }

            if (_retryButton != null)
            {
                _retryButton.interactable = !navigationPending;
            }

            if (_returnToLobbyButton != null)
            {
                _returnToLobbyButton.interactable = !navigationPending;
            }

            if (_returnToTitleButton != null)
            {
                _returnToTitleButton.interactable = !navigationPending;
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
                // Hide the shield row entirely when the player has no shield
                // capacity (legacy classes, or before MetaProgression grants
                // any). Otherwise show it alongside HP/XP.
                _shieldRow.SetActive(showHud && _shieldVisible);
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

        // Mirror of LevelUpSelectionUI's panel composition: a dark backdrop
        // shrunk inside the ornate trim + a 9-slice frame sprite overlay,
        // both ignored by the parent's VerticalLayoutGroup so the content
        // children stay centered. Run BEFORE adding the VerticalLayoutGroup
        // so the backdrop / frame become the first siblings (drawn behind).
        private static void AttachOrnatePanelFrame(RectTransform panel)
        {
            // Inner dim backdrop — fills the area inside the ornate corners.
            RectTransform backdrop = CreateRect("Backdrop", panel);
            StretchFull(backdrop);
            backdrop.offsetMin = new Vector2(28f, 28f);
            backdrop.offsetMax = new Vector2(-28f, -28f);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0.10f, 0.07f, 0.16f, 0.94f);
            backdropImage.raycastTarget = false;
            var backdropLayout = backdrop.gameObject.AddComponent<LayoutElement>();
            backdropLayout.ignoreLayout = true;

            // Ornate frame overlay — sprite Sliced (9-slice border was set on
            // import: 40/40/40/36). raycastTarget=false so the frame doesn't
            // eat clicks meant for the action buttons underneath.
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
            // Layout container only — no temporary dark background. Result /
            // Reward panels add their own opaque backdrop separately when
            // they need to dim the gameplay scene behind them.
            return rect;
        }

        private Text CreateIconBarRow(RectTransform parent, string label, Texture2D icon, Color fillColor, out RectTransform fillRect)
        {
            RectTransform row = CreateRect($"{label}Row", parent);
            SetPreferredHeight(row, 60f);

            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            CreateSimpleIcon(row, icon, 56f, 56f);

            RectTransform barBg = CreateRect($"{label}BarBg", row);
            barBg.sizeDelta = new Vector2(360f, 36f);
            LayoutElement barLayout = barBg.gameObject.AddComponent<LayoutElement>();
            barLayout.preferredWidth = 360f;
            barLayout.preferredHeight = 36f;
            Image bg = barBg.gameObject.AddComponent<Image>();
            // Bar empty sprite — 9-slice (8/0/8/0) keeps the rivet ends sharp
            // while the middle stretches. Falls back to a flat dark plate if
            // the sprite is missing.
            var barEmptySprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_bar_empty");
            if (barEmptySprite != null)
            {
                bg.sprite = barEmptySprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0f, 0f, 0f, 0.55f);
            }

            fillRect = CreateRect($"{label}BarFill", barBg);
            // Inset the fill so it sits inside the bar empty's bronze trim
            // (8px corners) with a touch of breathing room — wider inset
            // keeps the trim visibly framing the fill instead of being
            // overwritten. The right anchorMax.x is what the runtime drives
            // to express progression (0..1).
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0.8f, 1f);
            fillRect.offsetMin = new Vector2(10f, 8f);
            fillRect.offsetMax = new Vector2(-10f, -8f);
            Image fill = fillRect.gameObject.AddComponent<Image>();
            // Bar fill sprite — same 9-slice border so the fill stays crisp
            // at any width. Tinted by the caller (red/cyan/steel-blue).
            var barFillSprite = Resources.Load<Sprite>("NightDash/UI/Frames/nd_ui_bar_fill");
            if (barFillSprite != null)
            {
                fill.sprite = barFillSprite;
                fill.type = Image.Type.Sliced;
            }
            fill.color = fillColor;

            Text value = CreateText(row, $"{label} 0", 32, TextAnchor.MiddleLeft, new Color(0.92f, 0.9f, 0.96f, 1f));
            SetPreferredWidth(value.rectTransform, 220f);
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
            // Warm parchment color reads cleanly on the dark HUD panel and
            // matches the menu label palette used elsewhere.
            Text text = CreateText(row, value, 38, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.82f, 1f));
            return text;
        }

        private Text CreateStatRow(RectTransform parent, Texture2D icon, string label, string value)
        {
            RectTransform row = CreateRect($"{label}Row", parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            SetPreferredHeight(row, 72f);

            CreateSimpleIcon(row, icon, 64f, 64f);
            Text labelText = CreateText(row, label, 32, TextAnchor.MiddleLeft, new Color(0.68f, 0.62f, 0.73f, 1f));
            SetPreferredWidth(labelText.rectTransform, 360f);
            Text valueText = CreateText(row, value, 32, TextAnchor.MiddleLeft, new Color(0.93f, 0.88f, 0.97f, 1f));
            return valueText;
        }

        private Button CreateActionButton(RectTransform parent, string label, Action onClick)
        {
            RectTransform rect = CreateRect($"{label}Button", parent);
            rect.sizeDelta = new Vector2(303f, 111f);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 303f;
            layout.preferredHeight = 111f;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            // Uniform 3× scale of the alpha-trimmed 101×37 button sprite,
            // matching the PauseMenu / Title menu button treatment.
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
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

            Text text = CreateText(rect, label.ToUpperInvariant(), 40, TextAnchor.MiddleCenter, new Color(0.95f, 0.91f, 0.98f, 1f));
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
                // Source PNGs are alpha-trimmed and arrive at arbitrary
                // aspect ratios (defeat_skull 36×55, victory_crown 55×52,
                // etc). Without preserveAspect Unity stretches them into
                // the RectTransform's box and the sprite reads warped.
                image.preserveAspect = true;
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
            text.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Drop a dark outline on every HUD/result label so glyphs stay
            // readable on top of arbitrary gameplay backgrounds (no temp
            // dark panels behind them anymore).
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);
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

        // Retry flow (post-S5-B-M3 simplification):
        // 1. Sweep gameplay entities + reset Player in place + reset BossSpawnState.
        //    PlayerTag stays (FallbackSystem self-disables after first run).
        // 2. Queue the Retry navigation request. RunNavigationSystem picks it
        //    up next frame, sets DataLoadState.HasLoaded=0 + Status=Loading +
        //    consumes the request. DataBootstrapSystem then re-applies the
        //    saved RunSelection on the *next* frame and flips Status=Playing.
        //
        // No SceneManager.LoadScene any more — reloading the scene tore down
        // every RuntimeInitializeOnLoadMethod hook (Title / Lobby / HUD)
        // which, combined with FallbackSystem.state.Enabled=false leftover
        // state, produced ghost Title screens and missing UI on resume.
        private static void RequestRetry()
        {
            RunTeardownBridge.DestroyCurrentRun(clearNavigation: false);
            SubmitNavigation(RunNavigationAction.Retry);
        }

        // Lobby return — directly activates NightDashLobbyScreenUI, mirroring
        // the Pause-Menu Return-to-Lobby path. SubmitNavigation would only
        // reset ECS state without surfacing the lobby UI, which is what made
        // the old "Menu" button look like it did nothing.
        private static void RequestReturnToLobby()
        {
            RunTeardownBridge.DestroyCurrentRun();
            var lobby = UnityEngine.Object.FindFirstObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include);
            if (lobby != null)
            {
                lobby.gameObject.SetActive(true);
                NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            }
            else
            {
                NightDashLog.Warn("[NightDash] Return to Lobby: NightDashLobbyScreenUI not found.");
            }
        }

        // Title return — same teardown, but routes back to the main menu.
        private static void RequestReturnToTitle()
        {
            RunTeardownBridge.DestroyCurrentRun();
            var title = UnityEngine.Object.FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (title != null)
            {
                title.gameObject.SetActive(true);
                NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);
            }
            else
            {
                NightDashLog.Warn("[NightDash] Return to Title: NightDashTitleScreenUI not found.");
            }
        }

        private static void DestroyGameplayEntities()
        {
            // Route through RunTeardownBridge so the cleanup matches the
            // Pause-Menu Return-to-Lobby path: Player entity stays in place
            // (FallbackSystem self-disables after first run; destroying
            // PlayerTag would leave the next run with no Player). Enemies,
            // bosses, projectiles, pickups, and pooled views are all swept,
            // and BossSpawnState is reset so EnemySpawnSystem will spawn
            // again. clearNavigation=false keeps the just-queued Retry
            // request alive for the next-scene Title.OnEnable to pick up.
            RunTeardownBridge.DestroyCurrentRun(clearNavigation: false);
        }

        private static void DestroyByTag<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
            {
                em.DestroyEntity(query);
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
