// Persistent record of which stages the player has cleared at least once.
// Used by the lobby to gate difficulty-modifier selection: a stage must be
// cleared once before its modifiers can be enabled for subsequent runs.
//
// Storage: PlayerPrefs string, comma-separated stage ids. Survives Editor
// recompile and standalone restarts.

using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Runtime
{
    public static class RunClearRecord
    {
        private const string PrefKey = "nightdash.run.cleared_stages";
        private const char Separator = ',';

        public static bool IsCleared(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return false;
            string raw = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return false;
            string[] parts = raw.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == stageId) return true;
            }
            return false;
        }

        public static void MarkCleared(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return;
            if (IsCleared(stageId)) return;
            string raw = PlayerPrefs.GetString(PrefKey, string.Empty);
            string updated = string.IsNullOrEmpty(raw)
                ? stageId
                : raw + Separator + stageId;
            PlayerPrefs.SetString(PrefKey, updated);
            PlayerPrefs.Save();
        }

        public static void GetAllCleared(List<string> destination)
        {
            if (destination == null) return;
            destination.Clear();
            string raw = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split(Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i])) destination.Add(parts[i]);
            }
        }

        // Test/debug only. Removes every recorded clear.
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }
    }
}
