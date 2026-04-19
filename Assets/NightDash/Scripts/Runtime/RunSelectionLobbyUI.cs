using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
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
            RunSelectionLobbyWorldBridge.SetRunActiveInCurrentWorld(false);
            NightDashButtonFrameStyle.LoadAndCropFrameTextures(
                ref buttonDefaultTexture,
                ref buttonHoverTexture,
                ref buttonPressedTexture,
                ref buttonDisabledTexture);
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
                    RunSelectionLobbyWorldBridge.SetRunActiveInCurrentWorld(false);
                }
            }

            if (!_isVisible || _isStartingRun)
            {
                return;
            }

            DrawRunSelectionScreen();
        }

        public void SetLobbyVisible(bool visible)
        {
            _showTitle = false;
            _isVisible = visible;
            if (visible)
            {
                SuppressInteractions(0.2f);
            }
            RunSelectionLobbyWorldBridge.SetRunActiveInCurrentWorld(!visible);
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

        private void RefreshOptions()
        {
            _stageIds.Clear();
            _classIds.Clear();

            var registry = DataRegistry.Instance;
            DataCatalog catalog = registry != null ? registry.Catalog : null;
            if (catalog != null)
            {
                RunSelectionLobbyOptions.AddStageIds(catalog.stages, _stageIds);
                RunSelectionLobbyOptions.AddClassIds(catalog.classes, _classIds);
            }

            RunSelectionLobbyOptions.EnsureFallbackIds(_stageIds, "stage_01");
            RunSelectionLobbyOptions.EnsureFallbackIds(_classIds, "class_warrior");

            RunSelectionSession.GetCurrent(out string currentStage, out string currentClass);
            _stageIndex = RunSelectionLobbyOptions.FindIndexOrDefault(_stageIds, currentStage);
            _classIndex = RunSelectionLobbyOptions.FindIndexOrDefault(_classIds, currentClass);
        }

        private void StartRunWithSelectedOptions()
        {
            string selectedStage = RunSelectionLobbyOptions.SafeGet(_stageIds, _stageIndex, "stage_01");
            string selectedClass = RunSelectionLobbyOptions.SafeGet(_classIds, _classIndex, "class_warrior");
            StartRun(selectedStage, selectedClass);
        }

        private void StartRun(string stageId, string classId)
        {
            RunSelectionSession.SetPending(stageId, classId);
            _showTitle = false;
            _isVisible = false;
            _isStartingRun = true;
            NightDashLog.Info($"[NightDash] Start Run requested: stage='{stageId}', class='{classId}'.");
            bool appliedInWorld = RunSelectionLobbyWorldBridge.TryApplySelectionToCurrentWorld(stageId, classId);

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

        private GUIStyle GetActionButtonStyle()
        {
            if (_actionButtonStyle != null)
            {
                return _actionButtonStyle;
            }

            _actionButtonStyle = NightDashButtonFrameStyle.BuildActionButtonStyle(
                buttonDefaultTexture,
                buttonHoverTexture,
                buttonPressedTexture,
                buttonDisabledTexture);
            return _actionButtonStyle;
        }

        private void SyncLobbyVisibilityFromNavigationRequest()
        {
            if (!RunSelectionLobbyWorldBridge.TryGetPendingNavigation(out RunNavigationAction action, out string stageId, out string classId))
            {
                return;
            }

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
            RunSelectionLobbyWorldBridge.SetRunActiveInCurrentWorld(false);
        }

        private void SuppressInteractions(float durationSeconds)
        {
            _interactionUnlockTime = Mathf.Max(_interactionUnlockTime, Time.unscaledTime + Mathf.Max(0f, durationSeconds));
        }
    }
}
