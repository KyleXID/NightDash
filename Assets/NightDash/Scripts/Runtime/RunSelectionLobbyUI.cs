using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDash.Runtime
{
    public sealed class RunSelectionLobbyUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private string gameplaySceneName;

        private readonly List<string> _stageIds = new();
        private readonly List<string> _classIds = new();
        private int _stageIndex;
        private int _classIndex;
        private bool _isVisible;
        private bool _isStartingRun;

        private void Awake()
        {
            _isVisible = showOnStart;
            RefreshOptions();
        }

        private void OnGUI()
        {
            HandleToggleEvent();
            if (!_isVisible || _isStartingRun)
            {
                return;
            }

            const float width = 420f;
            const float height = 210f;
            Rect panel = new Rect(24f, 24f, width, height);
            GUI.Box(panel, "NightDash Run Selection");

            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 28f, panel.width - 24f, panel.height - 36f));
            DrawSelectionRow("Stage", _stageIds, ref _stageIndex);
            GUILayout.Space(8f);
            DrawSelectionRow("Class", _classIds, ref _classIndex);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(28f)))
            {
                RefreshOptions();
            }

            if (GUILayout.Button("Start Run", GUILayout.Height(28f)))
            {
                StartRunWithSelectedOptions();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Toggle UI: {toggleKey}");
            GUILayout.EndArea();
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

            RunSelectionSession.SetPending(selectedStage, selectedClass);
            _isStartingRun = true;

            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
    }
}
