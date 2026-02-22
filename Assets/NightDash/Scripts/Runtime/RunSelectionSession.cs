using UnityEngine;

namespace NightDash.Runtime
{
    public static class RunSelectionSession
    {
        private const string DefaultStageId = "stage_01";
        private const string DefaultClassId = "class_warrior";
        private const string StagePrefKey = "nightdash.run.stage_id";
        private const string ClassPrefKey = "nightdash.run.class_id";

        private static bool _initialized;
        private static bool _hasPendingSelection;
        private static string _pendingStageId = DefaultStageId;
        private static string _pendingClassId = DefaultClassId;

        public static void SetPending(string stageId, string classId)
        {
            EnsureInitialized();

            _pendingStageId = Normalize(stageId, DefaultStageId);
            _pendingClassId = Normalize(classId, DefaultClassId);
            _hasPendingSelection = true;

            PlayerPrefs.SetString(StagePrefKey, _pendingStageId);
            PlayerPrefs.SetString(ClassPrefKey, _pendingClassId);
            PlayerPrefs.Save();
        }

        public static bool TryConsumePending(out string stageId, out string classId)
        {
            EnsureInitialized();
            if (!_hasPendingSelection)
            {
                stageId = DefaultStageId;
                classId = DefaultClassId;
                return false;
            }

            stageId = _pendingStageId;
            classId = _pendingClassId;
            _hasPendingSelection = false;
            return true;
        }

        public static void GetCurrent(out string stageId, out string classId)
        {
            EnsureInitialized();
            stageId = _pendingStageId;
            classId = _pendingClassId;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _pendingStageId = Normalize(PlayerPrefs.GetString(StagePrefKey, DefaultStageId), DefaultStageId);
            _pendingClassId = Normalize(PlayerPrefs.GetString(ClassPrefKey, DefaultClassId), DefaultClassId);
            _hasPendingSelection = true;
            _initialized = true;
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
