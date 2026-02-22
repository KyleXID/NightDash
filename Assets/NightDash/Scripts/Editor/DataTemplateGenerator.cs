using System;
using NightDash.Data;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public static class DataTemplateGenerator
    {
        private const string Root = "Assets/NightDash/Data";

        [MenuItem("NightDash/Data/Create All Template Assets")]
        public static void CreateAllTemplates()
        {
            EnsureFolders();

            CreateAssetIfMissing<ClassData>(
                $"{Root}/Classes/tpl_class_warrior.asset",
                asset =>
                {
                    asset.id = "class_warrior";
                    asset.displayName = "전사";
                    asset.baseHp = 125;
                    asset.baseMoveSpeed = 4.8f;
                    asset.basePower = 11f;
                    asset.startWeaponId = "weapon_demon_greatsword";
                    asset.uniquePassiveId = "passive_warrior_guard_stack";
                    asset.ultimateSkillId = "ult_berserker_unleash";
                });

            CreateAssetIfMissing<WeaponData>(
                $"{Root}/Weapons/tpl_weapon_demon_greatsword.asset",
                asset =>
                {
                    asset.id = "weapon_demon_greatsword";
                    asset.displayName = "마계 대검";
                    asset.weaponType = WeaponType.Melee;
                    asset.maxLevel = 8;
                    asset.baseCooldown = 1.2f;
                    asset.basePowerCoeff = 1.1f;
                    asset.baseRange = 2.4f;
                    asset.baseProjectileSpeed = 0f;
                });

            CreateAssetIfMissing<PassiveData>(
                $"{Root}/Passives/tpl_passive_strength.asset",
                asset =>
                {
                    asset.id = "passive_strength";
                    asset.displayName = "근력 강화";
                    asset.description = "공격력을 증가시킵니다.";
                    asset.category = PassiveCategory.Stat;
                    asset.maxLevel = 5;
                });

            CreateAssetIfMissing<EvolutionData>(
                $"{Root}/Evolutions/tpl_evolution_hellflame_slash.asset",
                asset =>
                {
                    asset.id = "evolution_hellflame_slash";
                    asset.resultWeaponId = "weapon_abyss_hellflame_slash";
                    asset.requiredWeaponId = "weapon_hellflame_slash";
                    asset.requiredWeaponLevel = 8;
                    asset.requiredRiskScoreMin = 0;
                    asset.isAbyss = false;
                    asset.priority = 10;
                });

            CreateAssetIfMissing<StageData>(
                $"{Root}/Stages/tpl_stage_01.asset",
                asset =>
                {
                    asset.id = "stage_01";
                    asset.displayName = "불타는 황야";
                    asset.durationSec = 900;
                    asset.bossSpawnSec = 900;
                    asset.bossId = "boss_agron";
                    asset.rewardTableId = "reward_stage_01";
                    asset.baseRewardPoints = 100;
                });

            CreateAssetIfMissing<DifficultyModifierData>(
                $"{Root}/Difficulty/tpl_modifier_enemy_hp_up.asset",
                asset =>
                {
                    asset.id = "mod_enemy_hp_up";
                    asset.displayName = "마계의 각성";
                    asset.category = DifficultyCategory.Combat;
                    asset.riskPoint = 2;
                    asset.enemyModifiers = new EnemyModifierValues
                    {
                        hpPct = 0.4f,
                        moveSpeedPct = 0f,
                        spawnRatePct = 0f,
                    };
                    asset.rewardBonusPct = 0.2f;
                });

            CreateAssetIfMissing<MetaTreeData>(
                $"{Root}/Meta/tpl_meta_warrior.asset",
                asset =>
                {
                    asset.classId = "class_warrior";
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NightDash] Template assets created/verified.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/NightDash");
            EnsureFolder(Root);
            EnsureFolder($"{Root}/Classes");
            EnsureFolder($"{Root}/Weapons");
            EnsureFolder($"{Root}/Passives");
            EnsureFolder($"{Root}/Evolutions");
            EnsureFolder($"{Root}/Stages");
            EnsureFolder($"{Root}/Difficulty");
            EnsureFolder($"{Root}/Meta");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            if (slash <= 0)
            {
                return;
            }

            var parent = path.Substring(0, slash);
            var folderName = path.Substring(slash + 1);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void CreateAssetIfMissing<T>(string assetPath, Action<T> initializer)
            where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            initializer?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, assetPath);
            EditorUtility.SetDirty(asset);
        }
    }
}
