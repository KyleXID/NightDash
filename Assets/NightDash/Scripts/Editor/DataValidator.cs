using System.Collections.Generic;
using System.Linq;
using NightDash.Data;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public static class DataValidator
    {
        [MenuItem("NightDash/Validation/Run Data Validation")]
        public static void RunValidation()
        {
            var errors = new List<string>();

            var classes = LoadAll<ClassData>();
            var weapons = LoadAll<WeaponData>();
            var passives = LoadAll<PassiveData>();
            var evolutions = LoadAll<EvolutionData>();
            var stages = LoadAll<StageData>();
            var difficulties = LoadAll<DifficultyModifierData>();
            var metas = LoadAll<MetaTreeData>();

            ValidateUniqueIds("ClassData", classes.Select(x => x.id), errors);
            ValidateUniqueIds("WeaponData", weapons.Select(x => x.id), errors);
            ValidateUniqueIds("PassiveData", passives.Select(x => x.id), errors);
            ValidateUniqueIds("EvolutionData", evolutions.Select(x => x.id), errors);
            ValidateUniqueIds("StageData", stages.Select(x => x.id), errors);
            ValidateUniqueIds("DifficultyModifierData", difficulties.Select(x => x.id), errors);

            var weaponIds = new HashSet<string>(weapons.Select(x => x.id));
            var passiveIds = new HashSet<string>(passives.Select(x => x.id));
            var difficultyIds = new HashSet<string>(difficulties.Select(x => x.id));

            foreach (var c in classes)
            {
                if (string.IsNullOrWhiteSpace(c.id))
                {
                    errors.Add("ClassData has empty id.");
                }

                if (!string.IsNullOrWhiteSpace(c.startWeaponId) && !weaponIds.Contains(c.startWeaponId))
                {
                    errors.Add($"Class '{c.id}' references missing weapon '{c.startWeaponId}'.");
                }

                if (!string.IsNullOrWhiteSpace(c.uniquePassiveId) && !passiveIds.Contains(c.uniquePassiveId))
                {
                    errors.Add($"Class '{c.id}' references missing passive '{c.uniquePassiveId}'.");
                }
            }

            foreach (var e in evolutions)
            {
                if (string.IsNullOrWhiteSpace(e.resultWeaponId) || !weaponIds.Contains(e.resultWeaponId))
                {
                    errors.Add($"Evolution '{e.id}' has invalid resultWeaponId '{e.resultWeaponId}'.");
                }

                if (string.IsNullOrWhiteSpace(e.requiredWeaponId) || !weaponIds.Contains(e.requiredWeaponId))
                {
                    errors.Add($"Evolution '{e.id}' has invalid requiredWeaponId '{e.requiredWeaponId}'.");
                }

                if (e.requiredWeaponLevel < 1)
                {
                    errors.Add($"Evolution '{e.id}' has requiredWeaponLevel < 1.");
                }

                foreach (var pid in e.requiredPassiveIds)
                {
                    if (!string.IsNullOrWhiteSpace(pid) && !passiveIds.Contains(pid))
                    {
                        errors.Add($"Evolution '{e.id}' references missing passive '{pid}'.");
                    }
                }

                foreach (var mod in e.requiredModifiers)
                {
                    if (!string.IsNullOrWhiteSpace(mod) && !difficultyIds.Contains(mod))
                    {
                        errors.Add($"Evolution '{e.id}' references missing modifier '{mod}'.");
                    }
                }
            }

            foreach (var s in stages)
            {
                if (s.durationSec <= 0)
                {
                    errors.Add($"Stage '{s.id}' has invalid durationSec '{s.durationSec}'.");
                }

                if (s.bossSpawnSec > s.durationSec)
                {
                    errors.Add($"Stage '{s.id}' has bossSpawnSec > durationSec.");
                }

                foreach (var phase in s.spawnPhases)
                {
                    if (phase.fromSec < 0 || phase.toSec < phase.fromSec)
                    {
                        errors.Add($"Stage '{s.id}' has invalid spawn phase range [{phase.fromSec}, {phase.toSec}].");
                    }

                    if (phase.entries == null)
                    {
                        continue;
                    }

                    foreach (var entry in phase.entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.enemyId))
                        {
                            errors.Add($"Stage '{s.id}' has spawn entry with empty enemyId.");
                        }

                        if (entry.weight <= 0)
                        {
                            errors.Add($"Stage '{s.id}' has spawn entry '{entry.enemyId}' with weight <= 0.");
                        }

                        if (entry.spawnPerMin < 0)
                        {
                            errors.Add($"Stage '{s.id}' has spawn entry '{entry.enemyId}' with spawnPerMin < 0.");
                        }
                    }
                }
            }

            foreach (var d in difficulties)
            {
                if (d.riskPoint < 0)
                {
                    errors.Add($"Difficulty '{d.id}' has riskPoint < 0.");
                }
            }

            foreach (var m in metas)
            {
                var nodeIds = new HashSet<string>();
                foreach (var node in m.nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.nodeId))
                    {
                        errors.Add($"MetaTree '{m.classId}' contains node with empty nodeId.");
                        continue;
                    }

                    if (!nodeIds.Add(node.nodeId))
                    {
                        errors.Add($"MetaTree '{m.classId}' has duplicate nodeId '{node.nodeId}'.");
                    }
                }

                foreach (var node in m.nodes)
                {
                    if (node.prereqNodeIds == null)
                    {
                        continue;
                    }

                    foreach (var prereq in node.prereqNodeIds)
                    {
                        if (!string.IsNullOrWhiteSpace(prereq) && !nodeIds.Contains(prereq))
                        {
                            errors.Add($"MetaTree '{m.classId}' node '{node.nodeId}' references missing prereq '{prereq}'.");
                        }
                    }
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log("[NightDash] Data validation passed.");
                EditorUtility.DisplayDialog("NightDash Validation", "Data validation passed.", "OK");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"[NightDash][DataValidation] {error}");
            }

            EditorUtility.DisplayDialog(
                "NightDash Validation",
                $"Validation failed with {errors.Count} issues. Check Console for details.",
                "OK");
        }

        private static List<T> LoadAll<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var result = new List<T>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        private static void ValidateUniqueIds(string typeName, IEnumerable<string> ids, List<string> errors)
        {
            var idList = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var duplicates = idList.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var duplicate in duplicates)
            {
                errors.Add($"{typeName} contains duplicate id '{duplicate}'.");
            }
        }
    }
}
