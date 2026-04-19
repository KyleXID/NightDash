// S1-10 (redefined): EnemySpawnSystem player/enemy speed invariants.
//
// The original ticket framed this as a "extreme MoveSpeed stress test", but
// the Stage 1 balance pass (commit balance(stage1): ...) confirmed the values
// are intentional, not runaway. The follow-up that still has lasting value is
// the *invariant* — Stage 1 must keep the player strictly faster than any
// non-boss enemy, and the boss must be slow enough to kite.
//
// These tests pin that contract so any future balance edit that breaks it
// fails fast in CI.

using NUnit.Framework;
using Unity.Collections;
using NightDash.ECS.Systems;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class EnemySpawnInvariantTests
    {
        // Class MoveSpeed after the Stage 1 balance pass (2026-04-19).
        // Source: Assets/NightDash/Data/Classes/*.asset (warrior/mage/astrologer).
        // Sync with S3-08 if class values are revised again.
        private const float SlowestClassMoveSpeed = 3.5f; // warrior

        // Known enemy IDs in ResolveProfile's switch chain (see EnemySpawnSystem).
        private static readonly string[] NonBossIds =
        {
            "ember_bat",
            "wasteland_brute",
            "ash_caster",
            "ghoul_scout",
        };

        private const string BossId = "boss_agron";

        [Test]
        public void All_NonBoss_Enemies_Are_Slower_Than_Slowest_Class()
        {
            foreach (string id in NonBossIds)
            {
                var profile = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes(id));
                Assert.That(profile.MoveSpeed, Is.LessThan(SlowestClassMoveSpeed),
                    $"Non-boss '{id}' MoveSpeed {profile.MoveSpeed} must stay below " +
                    $"the slowest class ({SlowestClassMoveSpeed}) — player must always be able to kite.");
                Assert.That(profile.IsBoss, Is.False, $"'{id}' must not be flagged as boss");
            }
        }

        [Test]
        public void Boss_Is_Slower_Than_Slowest_Class_But_Not_Trivially_Slow()
        {
            var profile = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes(BossId));

            Assert.That(profile.IsBoss, Is.True, "boss_agron must be flagged as boss");
            Assert.That(profile.MoveSpeed, Is.LessThan(SlowestClassMoveSpeed),
                "Boss MoveSpeed must stay below slowest class so kiting remains possible.");
            Assert.That(profile.MoveSpeed, Is.GreaterThanOrEqualTo(SlowestClassMoveSpeed * 0.4f),
                "Boss MoveSpeed must not be trivially low (≥ 40% of slowest class) — keep pressure.");
        }

        [Test]
        public void All_Known_Profiles_Have_Positive_Stats()
        {
            foreach (string id in NonBossIds)
            {
                AssertPositiveStats(id);
            }
            AssertPositiveStats(BossId);
        }

        [Test]
        public void Boss_Has_Much_More_HP_Than_Any_NonBoss()
        {
            var boss = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes(BossId));

            foreach (string id in NonBossIds)
            {
                var profile = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes(id));
                Assert.That(boss.MaxHealth, Is.GreaterThan(profile.MaxHealth * 3f),
                    $"Boss HP {boss.MaxHealth} should be ≥ 3x non-boss '{id}' HP {profile.MaxHealth}.");
            }
        }

        [Test]
        public void Unknown_Enemy_Id_Falls_Back_To_GhoulScout()
        {
            var unknown = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes("definitely_not_a_real_enemy"));
            var ghoul   = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes("ghoul_scout"));

            Assert.That(unknown.MaxHealth, Is.EqualTo(ghoul.MaxHealth), "Fallback must route to ghoul_scout HP");
            Assert.That(unknown.MoveSpeed, Is.EqualTo(ghoul.MoveSpeed), "Fallback must route to ghoul_scout MoveSpeed");
            Assert.That(unknown.IsBoss,    Is.EqualTo(ghoul.IsBoss),    "Fallback must route to ghoul_scout IsBoss flag");
        }

        private static void AssertPositiveStats(string id)
        {
            var profile = EnemySpawnSystem.ResolveProfile(new FixedString64Bytes(id));
            Assert.That(profile.MaxHealth, Is.GreaterThan(0f), $"{id} MaxHealth must be positive");
            Assert.That(profile.Damage,    Is.GreaterThan(0f), $"{id} Damage must be positive");
            Assert.That(profile.MoveSpeed, Is.GreaterThan(0f), $"{id} MoveSpeed must be positive");
        }
    }
}
