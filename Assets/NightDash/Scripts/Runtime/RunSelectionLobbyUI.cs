using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDash.Runtime
{
    public sealed class RunSelectionLobbyUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Texture2D titleImage = null;
        [SerializeField] private Texture2D titleLogoImage = null;
        [SerializeField, Range(0.5f, 1.2f)] private float titleBrightness = 0.85f;
        [SerializeField] private bool showTitleOnStart = true;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private string gameplaySceneName = "";
        [SerializeField] private Texture2D buttonDefaultTexture = null;
        [SerializeField] private Texture2D buttonHoverTexture = null;
        [SerializeField] private Texture2D buttonPressedTexture = null;
        [SerializeField] private Texture2D buttonDisabledTexture = null;

        private readonly List<string> _stageIds = new();
        private readonly List<string> _classIds = new();
        private int _stageIndex;
        private int _classIndex;
        private bool _isVisible;
        private bool _isStartingRun;
        private bool _showTitle;
        private float _startFallbackTimer;
        private float _interactionUnlockTime;
        private GUIStyle _actionButtonStyle;

        public Texture2D TitleTexture => titleImage;
        public Texture2D TitleLogoTexture => titleLogoImage;
        public bool IsTitleVisible => _showTitle;
        public bool IsLobbyVisible => _isVisible && !_isStartingRun;

        private void Awake()
        {
            if (titleImage == null)
            {
                titleImage = Resources.Load<Texture2D>("NightDash/UI/Title/nightdash_title");
            }

            if (titleLogoImage == null)
            {
                titleLogoImage = Resources.Load<Texture2D>("NightDash/UI/Title/nightdash_logo");
            }

            _isVisible = showOnStart;
            _showTitle = showTitleOnStart;
            _showTitle = false;
            _isVisible = false;
            RefreshOptions();
            SetRunActiveInCurrentWorld(false);
            LoadButtonFrameTextures();
        }

        private void OnGUI()
        {
            _startFallbackTimer += Time.deltaTime;
            SyncLobbyVisibilityFromNavigationRequest();
            HandleToggleEvent();
            if (_showTitle)
            {
                DrawTitleScreen();
                return;
            }

            // Fallback: if title UI failed to spawn, force run selection visible shortly after boot.
            if (!_isVisible && _startFallbackTimer > 0.6f)
            {
                var titleUi = FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
                if (titleUi == null)
                {
                    _isVisible = true;
                    SetRunActiveInCurrentWorld(false);
                }
            }

            if (!_isVisible || _isStartingRun)
            {
                return;
            }

            DrawRunSelectionScreen();
        }

        private void DrawRunSelectionScreen()
        {
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (titleImage != null)
            {
                Color prev = GUI.color;
                GUI.color = new Color(titleBrightness, titleBrightness, titleBrightness, 1f);
                GUI.DrawTexture(full, titleImage, ScaleMode.ScaleAndCrop, false);
                GUI.color = prev;
            }
            else
            {
                GUI.Box(full, GUIContent.none);
            }

            if (titleLogoImage != null)
            {
                float logoWidth = Mathf.Min(680f, Screen.width * 0.42f);
                float logoHeight = logoWidth * ((float)titleLogoImage.height / Mathf.Max(1f, titleLogoImage.width));
                Rect logoRect = new Rect((Screen.width - logoWidth) * 0.5f, Screen.height * 0.09f, logoWidth, logoHeight);
                GUI.DrawTexture(logoRect, titleLogoImage, ScaleMode.ScaleToFit, true);
            }

            const float width = 620f;
            const float height = 400f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.58f, width, height);
            GUI.Box(panel, "NightDash Run Selection");

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 38f, panel.width - 40f, panel.height - 52f));
            DrawSelectionRow("Stage", _stageIds, ref _stageIndex);
            GUILayout.Space(16f);
            DrawSelectionRow("Class", _classIds, ref _classIndex);
            GUILayout.Space(20f);

            GUILayout.BeginHorizontal();
            bool interactionsEnabled = Time.unscaledTime >= _interactionUnlockTime;
            GUI.enabled = interactionsEnabled;
            if (GUILayout.Button("Refresh", GetActionButtonStyle(), GUILayout.Height(78f)))
            {
                RefreshOptions();
            }

            if (GUILayout.Button("Start Run", GetActionButtonStyle(), GUILayout.Height(78f)))
            {
                StartRunWithSelectedOptions();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawTitleScreen()
        {
            Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (titleImage != null)
            {
                // Keep immediate-mode title path as fallback; disable alpha blending to avoid washed-out brightness.
                Color prev = GUI.color;
                GUI.color = new Color(titleBrightness, titleBrightness, titleBrightness, 1f);
                GUI.DrawTexture(full, titleImage, ScaleMode.ScaleAndCrop, false);
                GUI.color = prev;
            }
            else
            {
                GUI.Box(full, "NightDash");
            }

            const float buttonWidth = 280f;
            const float buttonHeight = 56f;
            Rect buttonRect = new Rect(
                (Screen.width - buttonWidth) * 0.5f,
                Screen.height * 0.78f,
                buttonWidth,
                buttonHeight);

            if (GUI.Button(buttonRect, "Start"))
            {
                _showTitle = false;
                _isVisible = true;
                NightDashLog.Info("[NightDash] Title Start clicked.");
            }
        }

        public void SetLobbyVisible(bool visible)
        {
            _showTitle = false;
            _isVisible = visible;
            if (visible)
            {
                SuppressInteractions(0.2f);
            }
            SetRunActiveInCurrentWorld(!visible);
        }

        private void RefreshOptions()
        {
            _stageIds.Clear();
            _classIds.Clear();

            var registry = DataRegistry.Instance;
            DataCatalog catalog = registry != null ? registry.Catalog : null;
            if (catalog != null)
            {
                AddStageIds(catalog.stages, _stageIds);
                AddClassIds(catalog.classes, _classIds);
            }

            EnsureFallbackIds(_stageIds, "stage_01");
            EnsureFallbackIds(_classIds, "class_warrior");

            RunSelectionSession.GetCurrent(out string currentStage, out string currentClass);
            _stageIndex = FindIndexOrDefault(_stageIds, currentStage);
            _classIndex = FindIndexOrDefault(_classIds, currentClass);
        }

        private void StartRunWithSelectedOptions()
        {
            string selectedStage = SafeGet(_stageIds, _stageIndex, "stage_01");
            string selectedClass = SafeGet(_classIds, _classIndex, "class_warrior");
            StartRun(selectedStage, selectedClass);
        }

        private void StartRun(string stageId, string classId)
        {
            RunSelectionSession.SetPending(stageId, classId);
            _showTitle = false;
            _isVisible = false;
            _isStartingRun = true;
            NightDashLog.Info($"[NightDash] Start Run requested: stage='{stageId}', class='{classId}'.");
            bool appliedInWorld = TryApplySelectionToCurrentWorld(stageId, classId);

            if (appliedInWorld)
            {
                _isStartingRun = false;
                NightDashLog.Info("[NightDash] Run started in current ECS world (no scene reload).");
                return;
            }

            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            int buildIndex = activeScene.buildIndex;
            if (buildIndex < 0 && !string.IsNullOrWhiteSpace(activeScene.path))
            {
                buildIndex = SceneUtility.GetBuildIndexByScenePath(activeScene.path);
            }

            if (buildIndex >= 0)
            {
                SceneManager.LoadScene(buildIndex);
                return;
            }

            NightDashLog.Warn("[NightDash] Could not apply selection to ECS world directly. Falling back to scene-name reload.");
            SceneManager.LoadScene(activeScene.name);
        }

        private static void DrawSelectionRow(string label, List<string> ids, ref int index)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(56f));

            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                index = ids.Count == 0 ? 0 : (index - 1 + ids.Count) % ids.Count;
            }

            GUILayout.Label(ids.Count == 0 ? "-" : ids[index], GUILayout.Width(220f));

            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                index = ids.Count == 0 ? 0 : (index + 1) % ids.Count;
            }
            GUILayout.EndHorizontal();
        }

        private static void AddStageIds(List<StageData> stages, List<string> target)
        {
            if (stages == null)
            {
                return;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null || string.IsNullOrWhiteSpace(stages[i].id))
                {
                    continue;
                }

                string id = stages[i].id.Trim();
                if (!target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        private static void AddClassIds(List<ClassData> classes, List<string> target)
        {
            if (classes == null)
            {
                return;
            }

            for (int i = 0; i < classes.Count; i++)
            {
                if (classes[i] == null || string.IsNullOrWhiteSpace(classes[i].id))
                {
                    continue;
                }

                string id = classes[i].id.Trim();
                if (!target.Contains(id))
                {
                    target.Add(id);
                }
            }
        }

        private static void EnsureFallbackIds(List<string> target, string fallback)
        {
            if (target.Count > 0)
            {
                return;
            }

            target.Add(fallback);
        }

        private static int FindIndexOrDefault(List<string> values, string current)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            int idx = values.IndexOf(current);
            return idx >= 0 ? idx : 0;
        }

        private static string SafeGet(List<string> values, int index, string fallback)
        {
            if (values.Count == 0)
            {
                return fallback;
            }

            if (index < 0 || index >= values.Count)
            {
                return values[0];
            }

            return values[index];
        }

        private void HandleToggleEvent()
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            if (e.type == EventType.KeyDown && e.keyCode == toggleKey)
            {
                _isVisible = !_isVisible;
                e.Use();
            }
        }

        private static bool TryApplySelectionToCurrentWorld(string stageId, string classId)
        {
            int worldCount = World.All.Count;
            if (worldCount == 0)
            {
                NightDashLog.Warn("[NightDash] No ECS world exists yet.");
                return false;
            }

            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<RunSelection>(),
                    ComponentType.ReadWrite<DataLoadState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                entityManager.SetComponentData(singleton, new RunSelection
                {
                    StageId = new FixedString64Bytes(stageId),
                    ClassId = new FixedString64Bytes(classId)
                });
                entityManager.SetComponentData(singleton, new DataLoadState { HasLoaded = 0 });

                if (entityManager.HasComponent<GameLoopState>(singleton))
                {
                    entityManager.SetComponentData(singleton, new GameLoopState
                    {
                        ElapsedTime = 0f,
                        Level = 1,
                        Experience = 0f,
                        NextLevelExperience = 10f,
                        IsRunActive = 0,
                        Status = RunStatus.Loading,
                        PendingLevelUps = 0
                    });
                }

                if (entityManager.HasComponent<StageRuntimeConfig>(singleton))
                {
                    StageRuntimeConfig stageRuntime = entityManager.GetComponentData<StageRuntimeConfig>(singleton);
                    stageRuntime.IsStageCleared = 0;
                    entityManager.SetComponentData(singleton, stageRuntime);
                }

                if (entityManager.HasComponent<BossSpawnState>(singleton))
                {
                    entityManager.SetComponentData(singleton, new BossSpawnState
                    {
                        HasSpawnedBoss = 0,
                        BossKilled = 0,
                        ChestPending = 0,
                        ChestOpened = 0
                    });
                }

                if (entityManager.HasComponent<RunResultStats>(singleton))
                {
                    entityManager.SetComponentData(singleton, new RunResultStats
                    {
                        KillCount = 0,
                        GoldEarned = 0,
                        SoulsEarned = 0,
                        CurrentWave = 0,
                        RewardCommitted = 0
                    });
                }

                if (entityManager.HasComponent<BossRewardState>(singleton))
                {
                    entityManager.SetComponentData(singleton, new BossRewardState
                    {
                        HasPendingReward = 0,
                        EvolutionResolved = 0
                    });
                }

                if (entityManager.HasComponent<BossRewardConfirmRequest>(singleton))
                {
                    entityManager.SetComponentData(singleton, new BossRewardConfirmRequest
                    {
                        IsPending = 0
                    });
                }

                if (entityManager.HasComponent<ResultSnapshot>(singleton))
                {
                    entityManager.SetComponentData(singleton, new ResultSnapshot
                    {
                        HasSnapshot = 0,
                        IsVictory = 0,
                        ElapsedTime = 0f,
                        FinalLevel = 1,
                        KillCount = 0,
                        GoldEarned = 0,
                        SoulsEarned = 0,
                        RewardGranted = 0
                    });
                }

                if (entityManager.HasComponent<UpgradeSelectionRequest>(singleton))
                {
                    entityManager.SetComponentData(singleton, new UpgradeSelectionRequest
                    {
                        SelectedOptionIndex = -1,
                        HasSelection = 0,
                        RerollRequested = 0
                    });
                }

                if (entityManager.HasComponent<RunNavigationRequest>(singleton))
                {
                    entityManager.SetComponentData(singleton, new RunNavigationRequest
                    {
                        Action = RunNavigationAction.None,
                        IsPending = 0
                    });
                }

                NightDashLog.Info($"[NightDash] RunSelection applied directly to ECS world '{world.Name}': stage='{stageId}', class='{classId}'.");
                return true;
            }

            NightDashLog.Warn("[NightDash] No entity with RunSelection + DataLoadState found in any ECS world.");
            return false;
        }

        private void LoadButtonFrameTextures()
        {
            buttonDefaultTexture = buttonDefaultTexture != null ? buttonDefaultTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_default");
            buttonHoverTexture = buttonHoverTexture != null ? buttonHoverTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_hover");
            buttonPressedTexture = buttonPressedTexture != null ? buttonPressedTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_pressed");
            buttonDisabledTexture = buttonDisabledTexture != null ? buttonDisabledTexture : Resources.Load<Texture2D>("NightDash/UI/Frames/nd_ui_frame_button_disabled");

            buttonDefaultTexture = CropButtonFrameTexture(buttonDefaultTexture);
            buttonHoverTexture = CropButtonFrameTexture(buttonHoverTexture);
            buttonPressedTexture = CropButtonFrameTexture(buttonPressedTexture);
            buttonDisabledTexture = CropButtonFrameTexture(buttonDisabledTexture);
        }

        private GUIStyle GetActionButtonStyle()
        {
            if (_actionButtonStyle != null)
            {
                return _actionButtonStyle;
            }

            _actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(24, 24, 20, 20),
                margin = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(30, 30, 12, 14)
            };

            _actionButtonStyle.normal.background = buttonDefaultTexture;
            _actionButtonStyle.hover.background = buttonHoverTexture != null ? buttonHoverTexture : buttonDefaultTexture;
            _actionButtonStyle.active.background = buttonPressedTexture != null ? buttonPressedTexture : buttonDefaultTexture;
            _actionButtonStyle.focused.background = _actionButtonStyle.hover.background;

            if (buttonDisabledTexture != null)
            {
                _actionButtonStyle.onNormal.background = buttonDisabledTexture;
                _actionButtonStyle.onActive.background = buttonDisabledTexture;
            }

            _actionButtonStyle.normal.textColor = new Color(0.94f, 0.89f, 0.97f, 1f);
            _actionButtonStyle.hover.textColor = Color.white;
            _actionButtonStyle.active.textColor = new Color(0.92f, 0.86f, 0.96f, 1f);
            _actionButtonStyle.focused.textColor = Color.white;
            return _actionButtonStyle;
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

        private static void SetRunActiveInCurrentWorld(bool active)
        {
            int worldCount = World.All.Count;
            for (int i = 0; i < worldCount; i++)
            {
                World world = World.All[i];
                if (world == null || !world.IsCreated)
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<GameLoopState>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                Entity singleton = query.GetSingletonEntity();
                GameLoopState loop = entityManager.GetComponentData<GameLoopState>(singleton);
                loop.IsRunActive = active ? (byte)1 : (byte)0;
                if (!active)
                {
                    loop.ElapsedTime = 0f;
                    loop.Level = 1;
                    loop.Experience = 0f;
                    if (loop.NextLevelExperience <= 0f)
                    {
                        loop.NextLevelExperience = 10f;
                    }
                }
                entityManager.SetComponentData(singleton, loop);
            }
        }

        private void SyncLobbyVisibilityFromNavigationRequest()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<RunNavigationRequest>(),
                ComponentType.ReadOnly<GameLoopState>(),
                ComponentType.ReadOnly<RunSelection>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            Entity singleton = query.GetSingletonEntity();
            RunNavigationRequest navigation = entityManager.GetComponentData<RunNavigationRequest>(singleton);
            if (navigation.IsPending == 0 || navigation.Action == RunNavigationAction.None)
            {
                return;
            }

            RunNavigationAction action = navigation.Action;
            RunSelection selection = entityManager.GetComponentData<RunSelection>(singleton);
            string stageId = selection.StageId.IsEmpty ? "stage_01" : selection.StageId.ToString();
            string classId = selection.ClassId.IsEmpty ? "class_warrior" : selection.ClassId.ToString();

            navigation.IsPending = 0;
            navigation.Action = RunNavigationAction.None;
            entityManager.SetComponentData(singleton, navigation);

            if (action == RunNavigationAction.Retry)
            {
                StartRun(stageId, classId);
                return;
            }

            RunSelectionSession.SetCurrent(stageId, classId);
            RefreshOptions();
            _showTitle = false;
            _isVisible = true;
            _isStartingRun = false;
            SuppressInteractions(0.2f);
            SetRunActiveInCurrentWorld(false);
        }

        private void SuppressInteractions(float durationSeconds)
        {
            _interactionUnlockTime = Mathf.Max(_interactionUnlockTime, Time.unscaledTime + Mathf.Max(0f, durationSeconds));
        }
    }
}
