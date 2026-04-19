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

            ValidateStage1MinimumSlice(classes, weaponIds, passiveIds, weapons, passives, evolutions, stages, errors);

            foreach (var c in classes)
            {
                if (string.IsNullOrWhiteSpace(c.id))
                {
                    errors.Add("ClassData has empty id.");
                }

                if (string.IsNullOrWhiteSpace(c.startWeaponId))
                {
                    errors.Add($"Class '{c.id}' is missing startWeaponId.");
                }

                if (!string.IsNullOrWhiteSpace(c.startWeaponId) && !weaponIds.Contains(c.startWeaponId))
                {
                    errors.Add($"Class '{c.id}' references missing weapon '{c.startWeaponId}'.");
                }

                if (string.IsNullOrWhiteSpace(c.uniquePassiveId))
                {
                    errors.Add($"Class '{c.id}' is missing uniquePassiveId.");
                }

                if (!string.IsNullOrWhiteSpace(c.uniquePassiveId) && !passiveIds.Contains(c.uniquePassiveId))
                {
                    errors.Add($"Class '{c.id}' references missing passive '{c.uniquePassiveId}'.");
                }
            }

            foreach (var e in evolutions)
            {
                if (string.IsNullOrWhiteSpace(e.id))
                {
                    errors.Add("EvolutionData has empty id.");
                }

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

            foreach (var w in weapons)
            {
                if (string.IsNullOrWhiteSpace(w.id))
                {
                    errors.Add("WeaponData has empty id.");
                }

                if (w.maxLevel < 1)
                {
                    errors.Add($"Weapon '{w.id}' has maxLevel < 1.");
                }

                if (w.baseCooldown <= 0f)
                {
                    errors.Add($"Weapon '{w.id}' has baseCooldown <= 0.");
                }

                if (w.baseRange <= 0f)
                {
                    errors.Add($"Weapon '{w.id}' has baseRange <= 0.");
                }

                if (w.weaponType == WeaponType.Projectile && w.baseProjectileSpeed <= 0f)
                {
                    errors.Add($"Projectile weapon '{w.id}' has baseProjectileSpeed <= 0.");
                }
            }

            foreach (var p in passives)
            {
                if (string.IsNullOrWhiteSpace(p.id))
                {
                    errors.Add("PassiveData has empty id.");
                }

                if (p.maxLevel < 1)
                {
                    errors.Add($"Passive '{p.id}' has maxLevel < 1.");
                }
            }

            foreach (var s in stages)
            {
                if (string.IsNullOrWhiteSpace(s.id))
                {
                    errors.Add("StageData has empty id.");
                }

                if (s.durationSec <= 0)
                {
                    errors.Add($"Stage '{s.id}' has invalid durationSec '{s.durationSec}'.");
                }

                if (s.bossSpawnSec > s.durationSec)
                {
                    errors.Add($"Stage '{s.id}' has bossSpawnSec > durationSec.");
                }

                if (string.IsNullOrWhiteSpace(s.bossId))
                {
                    errors.Add($"Stage '{s.id}' is missing bossId.");
                }

                if (s.baseRewardPoints <= 0)
                {
                    errors.Add($"Stage '{s.id}' has baseRewardPoints <= 0.");
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
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
                else
                {
                    EditorUtility.DisplayDialog("NightDash Validation", "Data validation passed.", "OK");
                }
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"[NightDash][DataValidation] {error}");
            }

            if (Application.isBatchMode)
            {
                Debug.LogError($"[NightDash] Data validation failed with {errors.Count} issues.");
                EditorApplication.Exit(1);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "NightDash Validation",
                    $"Validation failed with {errors.Count} issues. Check Console for details.",
                    "OK");
            }
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

        private static void ValidateStage1MinimumSlice(
            List<ClassData> classes,
            HashSet<string> weaponIds,
            HashSet<string> passiveIds,
            List<WeaponData> weapons,
            List<PassiveData> passives,
            List<EvolutionData> evolutions,
            List<StageData> stages,
            List<string> errors)
        {
            var classIds = new HashSet<string>(classes.Select(x => x.id));
            var stageIds = new HashSet<string>(stages.Select(x => x.id));

            if (!stageIds.Contains("stage_01"))
            {
                errors.Add("Stage 1 MVP requires stage_01.");
            }

            if (!classIds.Contains("class_warrior"))
            {
                errors.Add("Stage 1 MVP requires class_warrior.");
            }

            string[] requiredWeapons =
            {
                "weapon_demon_greatsword",
                "weapon_demon_orb",
                "weapon_starfall"
            };

            foreach (var weaponId in requiredWeapons)
            {
                if (!weaponIds.Contains(weaponId))
                {
                    errors.Add($"Stage 1 MVP requires weapon '{weaponId}'.");
                }
            }

            string[] requiredPassives =
            {
                "passive_warrior_guard_stack",
                "passive_mage_free_upgrade",
                "passive_astrologer_reroll_free",
                "passive_strength",
                "passive_vitality",
                "passive_swiftness"
            };

            foreach (var passiveId in requiredPassives)
            {
                if (!passiveIds.Contains(passiveId))
                {
                    errors.Add($"Stage 1 MVP requires passive '{passiveId}'.");
                }
            }

            if (evolutions.Count < 2)
            {
                errors.Add("Stage 1 MVP requires at least 2 evolution assets.");
            }

            if (weapons.Count(x => x != null && x.includeInUpgradePool) < 3)
            {
                errors.Add("Stage 1 MVP requires at least 3 weapons in the upgrade pool.");
            }

            if (passives.Count(x => x != null && !string.IsNullOrWhiteSpace(x.id)) < 6)
            {
                errors.Add("Stage 1 MVP requires at least 6 passive assets.");
            }

            var stage01 = stages.FirstOrDefault(x => x != null && x.id == "stage_01");
            if (stage01 != null)
            {
                if (stage01.spawnPhases == null || stage01.spawnPhases.Count == 0)
                {
                    errors.Add("Stage 1 MVP requires stage_01 spawnPhases.");
                }

                if (string.IsNullOrWhiteSpace(stage01.bossId))
                {
                    errors.Add("Stage 1 MVP requires stage_01 bossId.");
                }

                bool hasBrutePhase = false;
                bool hasCasterPhase = false;
                bool hasBossEntry = false;

                foreach (var phase in stage01.spawnPhases)
                {
                    if (phase.entries == null)
                    {
                        continue;
                    }

                    foreach (var entry in phase.entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.enemyId))
                        {
                            continue;
                        }

                        if (entry.enemyId == "wasteland_brute")
                        {
                            hasBrutePhase = true;
                        }

                        if (entry.enemyId == "ash_caster")
                        {
                            hasCasterPhase = true;
                        }

                        if (entry.enemyId == stage01.bossId)
                        {
                            hasBossEntry = true;
                        }
                    }
                }

                if (!hasBrutePhase)
                {
                    errors.Add("Stage 1 MVP requires stage_01 to include wasteland_brute in spawn phases.");
                }

                if (!hasCasterPhase)
                {
                    errors.Add("Stage 1 MVP requires stage_01 to include ash_caster in spawn phases.");
                }

                if (!hasBossEntry)
                {
                    errors.Add("Stage 1 MVP requires stage_01 boss entry in spawn phases.");
                }
            }
        }
    }
}
