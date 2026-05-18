using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Runtime
{
    public static class RunSelectionSession
    {
        private const string DefaultStageId = "stage_01";
        private const string DefaultClassId = "class_warrior";
        private const string StagePrefKey = "nightdash.run.stage_id";
        private const string ClassPrefKey = "nightdash.run.class_id";
        // Format: "id1:lv1,id2:lv2". Level is optional — legacy entries that
        // were saved as "id1,id2" parse as Lv.1 so pre-stacking PlayerPrefs
        // continue to load. Levels of 0 are skipped at write time.
        private const string ModifierPrefKey = "nightdash.run.modifier_ids";
        private const char ModifierSeparator = ',';
        private const char LevelSeparator = ':';

        private static bool _initialized;
        private static bool _hasPendingSelection;
        private static string _pendingStageId = DefaultStageId;
        private static string _pendingClassId = DefaultClassId;
        private static string _pendingModifierIdsRaw = string.Empty;

        public static void SetPending(string stageId, string classId)
        {
            SetPending(stageId, classId, (IList<(string, int)>)null);
        }

        // Legacy overload — every id in the list is treated as Lv.1.
        public static void SetPending(string stageId, string classId, IList<string> modifierIds)
        {
            SetPending(stageId, classId, AsLevelOnePairs(modifierIds));
        }

        public static void SetPending(string stageId, string classId, IList<(string id, int level)> modifierStages)
        {
            EnsureInitialized();

            _pendingStageId = Normalize(stageId, DefaultStageId);
            _pendingClassId = Normalize(classId, DefaultClassId);
            _pendingModifierIdsRaw = JoinModifierStages(modifierStages);
            _hasPendingSelection = true;

            PlayerPrefs.SetString(StagePrefKey, _pendingStageId);
            PlayerPrefs.SetString(ClassPrefKey, _pendingClassId);
            PlayerPrefs.SetString(ModifierPrefKey, _pendingModifierIdsRaw);
            PlayerPrefs.Save();
        }

        public static void SetCurrent(string stageId, string classId)
        {
            SetCurrent(stageId, classId, (IList<(string, int)>)null);
        }

        // Legacy overload — every id in the list is treated as Lv.1.
        public static void SetCurrent(string stageId, string classId, IList<string> modifierIds)
        {
            SetCurrent(stageId, classId, AsLevelOnePairs(modifierIds));
        }

        public static void SetCurrent(string stageId, string classId, IList<(string id, int level)> modifierStages)
        {
            EnsureInitialized();

            _pendingStageId = Normalize(stageId, DefaultStageId);
            _pendingClassId = Normalize(classId, DefaultClassId);
            _pendingModifierIdsRaw = JoinModifierStages(modifierStages);
            _hasPendingSelection = false;

            PlayerPrefs.SetString(StagePrefKey, _pendingStageId);
            PlayerPrefs.SetString(ClassPrefKey, _pendingClassId);
            PlayerPrefs.SetString(ModifierPrefKey, _pendingModifierIdsRaw);
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

        // Updates only the stage + class without touching the modifier stack.
        // Useful for lobby navigation where the player browses characters
        // before pressing Start — every card hover should not clobber the
        // currently-selected difficulty modifiers.
        public static void SetCurrentStageAndClass(string stageId, string classId)
        {
            EnsureInitialized();
            _pendingStageId = Normalize(stageId, DefaultStageId);
            _pendingClassId = Normalize(classId, DefaultClassId);
            _hasPendingSelection = false;
            PlayerPrefs.SetString(StagePrefKey, _pendingStageId);
            PlayerPrefs.SetString(ClassPrefKey, _pendingClassId);
            PlayerPrefs.Save();
        }

        // Wipes the persisted modifier stack so the next lobby entry starts
        // every chip at Lv.0. Stage / class selection are left untouched so
        // the player's last-played choices still hydrate the lobby.
        public static void ClearModifierStages()
        {
            EnsureInitialized();
            _pendingModifierIdsRaw = string.Empty;
            PlayerPrefs.SetString(ModifierPrefKey, string.Empty);
            PlayerPrefs.Save();
        }

        // Legacy accessor — returns only the ids (level >= 1 entries) so older
        // call sites that don't care about stacking keep working.
        public static void GetCurrentModifierIds(List<string> destination)
        {
            EnsureInitialized();
            if (destination == null) return;
            destination.Clear();
            if (string.IsNullOrEmpty(_pendingModifierIdsRaw)) return;
            string[] parts = _pendingModifierIdsRaw.Split(ModifierSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseEntry(parts[i], out string id, out int level)) continue;
                if (level < 1) continue;
                destination.Add(id);
            }
        }

        // Returns (id, level) pairs in the order they were saved. Level is always >= 1.
        public static void GetCurrentModifierStages(List<(string id, int level)> destination)
        {
            EnsureInitialized();
            if (destination == null) return;
            destination.Clear();
            if (string.IsNullOrEmpty(_pendingModifierIdsRaw)) return;
            string[] parts = _pendingModifierIdsRaw.Split(ModifierSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseEntry(parts[i], out string id, out int level)) continue;
                if (level < 1) continue;
                destination.Add((id, level));
            }
        }

        private static IList<(string id, int level)> AsLevelOnePairs(IList<string> ids)
        {
            if (ids == null || ids.Count == 0) return null;
            var list = new List<(string, int)>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;
                list.Add((id, 1));
            }
            return list;
        }

        private static string JoinModifierStages(IList<(string id, int level)> stages)
        {
            if (stages == null || stages.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < stages.Count; i++)
            {
                (string id, int level) entry = stages[i];
                if (string.IsNullOrEmpty(entry.id)) continue;
                if (entry.level < 1) continue; // 0 = inactive, don't persist
                if (entry.id.IndexOf(ModifierSeparator) >= 0) continue; // corrupt id
                if (entry.id.IndexOf(LevelSeparator) >= 0) continue;    // corrupt id
                if (sb.Length > 0) sb.Append(ModifierSeparator);
                sb.Append(entry.id);
                sb.Append(LevelSeparator);
                sb.Append(entry.level);
            }
            return sb.ToString();
        }

        // Accepts "id:lv" (new) and "id" (legacy → Lv.1). Returns false for blanks
        // and entries whose id violates the FixedString64Bytes safety margin.
        private static bool TryParseEntry(string raw, out string id, out int level)
        {
            id = null;
            level = 0;
            if (string.IsNullOrEmpty(raw)) return false;

            int colon = raw.IndexOf(LevelSeparator);
            string parsedId;
            int parsedLevel;
            if (colon < 0)
            {
                parsedId = raw;
                parsedLevel = 1; // legacy entry — assume Lv.1
            }
            else
            {
                parsedId = raw.Substring(0, colon);
                string levelStr = raw.Substring(colon + 1);
                if (!int.TryParse(levelStr, out parsedLevel)) return false;
            }

            // Reject negative / Lv.0 entries up front so corrupted PlayerPrefs
            // can't slip past downstream filters and hit ECS lookup paths.
            if (parsedLevel < 1) return false;

            parsedId = parsedId.Trim();
            if (string.IsNullOrEmpty(parsedId)) return false;
            if (System.Text.Encoding.UTF8.GetByteCount(parsedId) > MaxIdLengthBytes) return false;

            id = parsedId;
            level = parsedLevel;
            return true;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _pendingStageId = Normalize(PlayerPrefs.GetString(StagePrefKey, DefaultStageId), DefaultStageId);
            _pendingClassId = Normalize(PlayerPrefs.GetString(ClassPrefKey, DefaultClassId), DefaultClassId);
            _pendingModifierIdsRaw = PlayerPrefs.GetString(ModifierPrefKey, string.Empty);
            _hasPendingSelection = true;
            _initialized = true;
        }

        // S4-07: FixedString64Bytes 안전 마진. 64바이트 캡을 넘는 입력이 Burst로 전달되면
        // ArgumentException 또는 silent truncation 발생 → 손상된 PlayerPrefs에 의한 크래시 방지.
        private const int MaxIdLengthBytes = 60;

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();
            return System.Text.Encoding.UTF8.GetByteCount(trimmed) <= MaxIdLengthBytes
                ? trimmed
                : fallback;
        }
    }
}
