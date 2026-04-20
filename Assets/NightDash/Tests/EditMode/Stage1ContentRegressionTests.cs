// S3-08: Stage 1 신규 콘텐츠 회귀 테스트
//
// S3-02 (4 신규 클래스)와 S3-03 (5 난이도 modifier)의 스탯·리스크가 GDD 기준(±20%) 밖으로
// 드리프트하지 않도록 AssetDatabase 경로 기반 고정 fixture로 잠근다.
// 밸런스 조정 시 본 테스트가 변경을 알람(테스트 명시적 업데이트) 경로로 강제한다.

using NUnit.Framework;
using UnityEditor;
using NightDash.Data;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class Stage1ContentRegressionTests
    {
        private const float Tolerance = 0.20f; // GDD 공통 회귀 허용폭

        private const string ClassesRoot = "Assets/NightDash/Data/Classes/";
        private const string DifficultyRoot = "Assets/NightDash/Data/Difficulty/";

        // -------------------------------------------------------------------
        // 클래스 스탯 테이블 (S3-02 커밋 기준 확정치)
        // 드리프트 발생 시 이 테이블을 GDD 업데이트와 함께 변경.
        // -------------------------------------------------------------------
        [TestCase("class_warrior.asset", "class_warrior", 125, 3.5f, 11f)]
        [TestCase("class_mage.asset", "class_mage", 85, 3.7f, 13f)]
        [TestCase("class_astrologer.asset", "class_astrologer", 95, 3.8f, 12f)]
        [TestCase("class_paladin.asset", "class_paladin", 115, 3.8f, 11f)]
        [TestCase("class_priest.asset", "class_priest", 90, 4.2f, 9f)]
        [TestCase("class_archer.asset", "class_archer", 95, 4.5f, 12f)]
        [TestCase("class_gunslinger.asset", "class_gunslinger", 90, 4.0f, 13f)]
        public void ClassData_Stats_Match_Fixture_Within_20Pct(
            string fileName, string expectedId, int expectedHp, float expectedMoveSpeed, float expectedPower)
        {
            string path = ClassesRoot + fileName;
            ClassData data = AssetDatabase.LoadAssetAtPath<ClassData>(path);
            Assert.That(data, Is.Not.Null, $"ClassData at '{path}' must exist.");

            Assert.That(data.id, Is.EqualTo(expectedId), "id mismatch");
            Assert.That((float)data.baseHp, Is.EqualTo((float)expectedHp).Within(expectedHp * Tolerance),
                $"baseHp drift > 20% (got {data.baseHp}, fixture {expectedHp})");
            Assert.That(data.baseMoveSpeed, Is.EqualTo(expectedMoveSpeed).Within(expectedMoveSpeed * Tolerance),
                $"baseMoveSpeed drift > 20% (got {data.baseMoveSpeed}, fixture {expectedMoveSpeed})");
            Assert.That(data.basePower, Is.EqualTo(expectedPower).Within(expectedPower * Tolerance),
                $"basePower drift > 20% (got {data.basePower}, fixture {expectedPower})");
        }

        [TestCase("class_paladin.asset", "weapon_holy_wave", "passive_paladin_hit_burst", "ult_judgment")]
        [TestCase("class_priest.asset", "weapon_light_ring", "passive_priest_periodic_heal", "ult_salvation")]
        [TestCase("class_archer.asset", "weapon_rapid_shot", "passive_archer_move_attack", "ult_rain_shot")]
        [TestCase("class_gunslinger.asset", "weapon_revolver", "passive_gunslinger_reload_buff", "ult_spray_mode")]
        public void NewClassData_Loadout_Ids_Stay_Stable(
            string fileName, string expectedWeaponId, string expectedPassiveId, string expectedUltId)
        {
            string path = ClassesRoot + fileName;
            ClassData data = AssetDatabase.LoadAssetAtPath<ClassData>(path);
            Assert.That(data, Is.Not.Null, $"ClassData at '{path}' must exist.");

            Assert.That(data.startWeaponId, Is.EqualTo(expectedWeaponId));
            Assert.That(data.uniquePassiveId, Is.EqualTo(expectedPassiveId));
            Assert.That(data.ultimateSkillId, Is.EqualTo(expectedUltId));
        }

        // -------------------------------------------------------------------
        // Difficulty modifier — riskPoint·rewardBonus 테이블 (S3-03 확정치)
        // rewardBonus = riskPoint × 0.1 규칙을 회귀로 강제.
        // -------------------------------------------------------------------
        [TestCase("modifier_enemy_hp_up.asset", "mod_enemy_hp_up", DifficultyCategory.Combat, 2, 0.2f)]
        [TestCase("modifier_enemy_speed_up.asset", "mod_enemy_speed_up", DifficultyCategory.Combat, 2, 0.2f)]
        [TestCase("modifier_enemy_surge.asset", "mod_enemy_surge", DifficultyCategory.Combat, 3, 0.3f)]
        [TestCase("modifier_no_heal.asset", "mod_no_heal", DifficultyCategory.Survival, 2, 0.2f)]
        [TestCase("modifier_on_kill_explosion.asset", "mod_on_kill_explosion", DifficultyCategory.Mechanic, 2, 0.2f)]
        public void Modifier_Risk_And_Reward_Match_Fixture(
            string fileName, string expectedId, DifficultyCategory expectedCategory,
            int expectedRiskPoint, float expectedRewardBonus)
        {
            string path = DifficultyRoot + fileName;
            DifficultyModifierData data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(path);
            Assert.That(data, Is.Not.Null, $"DifficultyModifierData at '{path}' must exist.");

            Assert.That(data.id, Is.EqualTo(expectedId));
            Assert.That(data.category, Is.EqualTo(expectedCategory));
            Assert.That(data.riskPoint, Is.EqualTo(expectedRiskPoint),
                "riskPoint 변경은 밸런스 영향 — fixture와 함께 업데이트 필요");
            Assert.That(data.rewardBonusPct, Is.EqualTo(expectedRewardBonus).Within(0.001f),
                "rewardBonus 는 riskPoint × 0.1 규칙 (S3-03) — 불일치는 밸런스 리뷰 필요");
        }

        // 회귀 대상 effect 필드별 검증 (개별 filed drift 탐지)
        [Test]
        public void Modifier_EnemyHpUp_Raises_Hp_Only()
        {
            var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(
                DifficultyRoot + "modifier_enemy_hp_up.asset");
            Assert.That(data.enemyModifiers.hpPct, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(data.enemyModifiers.moveSpeedPct, Is.EqualTo(0f));
            Assert.That(data.enemyModifiers.spawnRatePct, Is.EqualTo(0f));
        }

        [Test]
        public void Modifier_EnemySpeedUp_Raises_Speed_Only()
        {
            var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(
                DifficultyRoot + "modifier_enemy_speed_up.asset");
            Assert.That(data.enemyModifiers.hpPct, Is.EqualTo(0f));
            Assert.That(data.enemyModifiers.moveSpeedPct, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(data.enemyModifiers.spawnRatePct, Is.EqualTo(0f));
        }

        [Test]
        public void Modifier_EnemySurge_Raises_SpawnRate_Only()
        {
            var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(
                DifficultyRoot + "modifier_enemy_surge.asset");
            Assert.That(data.enemyModifiers.spawnRatePct, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Modifier_NoHeal_Disables_PlayerHeal()
        {
            var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(
                DifficultyRoot + "modifier_no_heal.asset");
            Assert.That(data.playerModifiers.healRatePct, Is.EqualTo(-1.0f).Within(0.001f),
                "healRatePct=-1.0f 은 회복 완전 차단 신호 — 값 변경은 Survival 밸런스 리뷰 필요");
        }

        [Test]
        public void Modifier_OnKillExplosion_Flag_Is_Set()
        {
            var data = AssetDatabase.LoadAssetAtPath<DifficultyModifierData>(
                DifficultyRoot + "modifier_on_kill_explosion.asset");
            Assert.That(data.runtimeEffects.onKillExplosion, Is.True,
                "onKillExplosion 플래그 off로 바뀌면 Mechanic 카테고리 대표성이 사라짐");
        }

        [Test]
        public void All_New_Classes_Registered_In_DataCatalog()
        {
            DataCatalog catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(
                "Assets/NightDash/Data/data_catalog.asset");
            Assert.That(catalog, Is.Not.Null, "data_catalog.asset must exist");

            string[] requiredIds =
            {
                "class_warrior",
                "class_mage",
                "class_astrologer",
                "class_paladin",
                "class_priest",
                "class_archer",
                "class_gunslinger",
            };

            foreach (string id in requiredIds)
            {
                bool found = false;
                foreach (ClassData c in catalog.classes)
                {
                    if (c != null && c.id == id)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, $"ClassData id '{id}' must be registered in data_catalog.classes");
            }
        }

        [Test]
        public void All_New_Modifiers_Registered_In_DataCatalog()
        {
            DataCatalog catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(
                "Assets/NightDash/Data/data_catalog.asset");
            Assert.That(catalog, Is.Not.Null, "data_catalog.asset must exist");

            string[] requiredIds =
            {
                "mod_enemy_hp_up",
                "mod_enemy_speed_up",
                "mod_enemy_surge",
                "mod_no_heal",
                "mod_on_kill_explosion",
            };

            foreach (string id in requiredIds)
            {
                bool found = false;
                foreach (DifficultyModifierData d in catalog.difficultyModifiers)
                {
                    if (d != null && d.id == id)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.That(found, Is.True, $"DifficultyModifierData id '{id}' must be registered in data_catalog.difficultyModifiers");
            }
        }
    }
}
