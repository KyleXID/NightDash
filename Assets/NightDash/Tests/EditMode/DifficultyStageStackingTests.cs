// Stack-aware difficulty modifier regression tests.
//   - DifficultyModifierData.TryGetStage fallback to legacy single-value block
//   - DifficultyModifierData.TryGetStage indexing into populated stages
//   - RunSelectionSession round-trip for (id, level) pairs
//   - RunSelectionSession backwards compatibility with the old "id,id" format
//   - RunSelectionSession skips Lv.0 entries when persisting

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NightDash.Data;
using NightDash.Runtime;
using UnityEngine;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class DifficultyStageStackingTests
    {
        private const string PlayerPrefKey = "nightdash.run.modifier_ids";

        [SetUp]
        public void SetUp()
        {
            // RunSelectionSession holds static state across tests — reset both
            // the PlayerPrefs entry and the in-memory cache so each test
            // starts from a clean slate regardless of run order.
            PlayerPrefs.DeleteKey(PlayerPrefKey);
            ResetSessionStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PlayerPrefKey);
            ResetSessionStaticState();
        }

        private static void ResetSessionStaticState()
        {
            var type = typeof(RunSelectionSession);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            type.GetField("_initialized", flags)!.SetValue(null, false);
            type.GetField("_hasPendingSelection", flags)!.SetValue(null, false);
            type.GetField("_pendingModifierIdsRaw", flags)!.SetValue(null, string.Empty);
        }

        [Test]
        public void TryGetStage_LegacyAsset_SynthesizesThreeStagesFromSingleValueFields()
        {
            var data = ScriptableObject.CreateInstance<DifficultyModifierData>();
            data.id = "legacy_modifier";
            data.riskPoint = 4;
            data.enemyModifiers = new EnemyModifierValues { hpPct = 0.20f };
            data.rewardBonusPct = 0.10f;
            // stages left null → IsLegacy path

            Assert.That(data.IsLegacy, Is.True);
            Assert.That(data.MaxLevel, Is.EqualTo(3),
                "Legacy assets now expose 3 synthesized levels so stacking works pre-migration.");

            Assert.That(data.TryGetStage(1, out DifficultyStage lv1), Is.True);
            Assert.That(lv1.enemyModifiers.hpPct, Is.EqualTo(0.20f).Within(1e-4f), "Lv.1 = 1.0x scale");
            Assert.That(lv1.riskPoint, Is.EqualTo(4));

            Assert.That(data.TryGetStage(2, out DifficultyStage lv2), Is.True);
            Assert.That(lv2.enemyModifiers.hpPct, Is.EqualTo(0.30f).Within(1e-4f), "Lv.2 = 1.5x scale");
            Assert.That(lv2.riskPoint, Is.EqualTo(6));

            Assert.That(data.TryGetStage(3, out DifficultyStage lv3), Is.True);
            Assert.That(lv3.enemyModifiers.hpPct, Is.EqualTo(0.40f).Within(1e-4f), "Lv.3 = 2.0x scale");
            Assert.That(lv3.riskPoint, Is.EqualTo(8));

            Assert.That(data.TryGetStage(4, out _), Is.False, "Out of range above legacy max.");
            Assert.That(data.TryGetStage(0, out _), Is.False, "Lv.0 means inactive.");

            ScriptableObject.DestroyImmediate(data);
        }

        [Test]
        public void TryGetStage_PopulatedStages_IndexesCorrectly()
        {
            var data = ScriptableObject.CreateInstance<DifficultyModifierData>();
            data.id = "stacked_modifier";
            data.stages = new[]
            {
                new DifficultyStage { riskPoint = 2, enemyModifiers = new EnemyModifierValues { hpPct = 0.20f } },
                new DifficultyStage { riskPoint = 4, enemyModifiers = new EnemyModifierValues { hpPct = 0.40f } },
                new DifficultyStage { riskPoint = 6, enemyModifiers = new EnemyModifierValues { hpPct = 0.60f } },
            };

            Assert.That(data.IsLegacy, Is.False);
            Assert.That(data.MaxLevel, Is.EqualTo(3));

            Assert.That(data.TryGetStage(2, out DifficultyStage lv2), Is.True);
            Assert.That(lv2.riskPoint, Is.EqualTo(4));
            Assert.That(lv2.enemyModifiers.hpPct, Is.EqualTo(0.40f).Within(1e-5f));

            Assert.That(data.TryGetStage(4, out _), Is.False, "Out of range above MaxLevel.");

            ScriptableObject.DestroyImmediate(data);
        }

        [Test]
        public void RunSelectionSession_RoundTripsModifierStages()
        {
            var stages = new List<(string id, int level)>
            {
                ("modifier_enemy_hp_up", 2),
                ("modifier_no_heal", 1),
                ("modifier_on_kill_explosion", 3),
            };
            RunSelectionSession.SetCurrent("stage_01", "class_warrior", stages);

            var loaded = new List<(string id, int level)>();
            RunSelectionSession.GetCurrentModifierStages(loaded);

            Assert.That(loaded.Count, Is.EqualTo(3));
            Assert.That(loaded[0], Is.EqualTo(("modifier_enemy_hp_up", 2)));
            Assert.That(loaded[1], Is.EqualTo(("modifier_no_heal", 1)));
            Assert.That(loaded[2], Is.EqualTo(("modifier_on_kill_explosion", 3)));

            // Persisted string should embed levels so external migrations can
            // round-trip without losing stack state.
            string raw = PlayerPrefs.GetString(PlayerPrefKey, string.Empty);
            Assert.That(raw, Does.Contain("modifier_enemy_hp_up:2"));
            Assert.That(raw, Does.Contain("modifier_on_kill_explosion:3"));
        }

        [Test]
        public void RunSelectionSession_LegacyFormat_ParsesAsLv1()
        {
            // Simulate a player who upgraded from the pre-stacking build —
            // PlayerPrefs already contains "id,id" without level suffixes. We
            // poke the raw cache directly so the parser exercised is the real
            // GetCurrentModifierStages path (not whatever Set* would write).
            SetPrivateRaw("modifier_enemy_hp_up,modifier_no_heal");

            var loaded = new List<(string id, int level)>();
            RunSelectionSession.GetCurrentModifierStages(loaded);

            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded[0], Is.EqualTo(("modifier_enemy_hp_up", 1)));
            Assert.That(loaded[1], Is.EqualTo(("modifier_no_heal", 1)));
        }

        [Test]
        public void RunSelectionSession_Lv0Entries_AreNotPersisted()
        {
            var stages = new List<(string id, int level)>
            {
                ("active", 2),
                ("inactive", 0),
                ("another_active", 1),
            };
            RunSelectionSession.SetCurrent("stage_01", "class_warrior", stages);

            string raw = PlayerPrefs.GetString(PlayerPrefKey, string.Empty);
            Assert.That(raw, Does.Not.Contain("inactive"));
            Assert.That(raw, Does.Contain("active:2"));
            Assert.That(raw, Does.Contain("another_active:1"));

            var loaded = new List<(string id, int level)>();
            RunSelectionSession.GetCurrentModifierStages(loaded);
            Assert.That(loaded.Count, Is.EqualTo(2));
            foreach (var entry in loaded)
            {
                Assert.That(entry.id, Is.Not.EqualTo("inactive"));
            }
        }

        [Test]
        public void RunSelectionSession_LegacyIdAccessor_FiltersLv0Entries()
        {
            var stages = new List<(string id, int level)>
            {
                ("a", 1),
                ("b", 0), // inactive — must not show up
                ("c", 3),
            };
            RunSelectionSession.SetCurrent("stage_01", "class_warrior", stages);

            var ids = new List<string>();
            RunSelectionSession.GetCurrentModifierIds(ids);

            Assert.That(ids, Is.EquivalentTo(new[] { "a", "c" }));
        }

        // Reflection helper: writes a raw modifier string into the static
        // cache (and marks the session as initialized) so we can exercise the
        // parser without going through Set*, which would re-encode the input.
        private static void SetPrivateRaw(string raw)
        {
            var type = typeof(RunSelectionSession);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            type.GetField("_pendingModifierIdsRaw", flags)!.SetValue(null, raw ?? string.Empty);
            type.GetField("_initialized", flags)!.SetValue(null, true);
        }
    }
}
