// S2-05: UpgradeOptionUtility unit tests.
// Covers the pure helpers extracted from the legacy ProgressionSystem in S2-01.
// Tests focus on buffer/list predicates that can be exercised without a
// DataRegistry or full world — BuildOptions/ApplySelection are deferred to
// integration coverage.

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using NightDash.ECS.Components;
using NightDash.ECS.Systems.Progression;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class UpgradeOptionUtilityTests
    {
        private World _world;
        private Entity _entity;

        [SetUp]
        public void CreateWorld()
        {
            _world = new World("S2-05 UpgradeOptionUtility");
            _entity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddBuffer<OwnedWeaponElement>(_entity);
            _world.EntityManager.AddBuffer<OwnedPassiveElement>(_entity);
            _world.EntityManager.AddBuffer<UpgradeOptionElement>(_entity);
        }

        [TearDown]
        public void DisposeWorld()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
            }
        }

        // --- ContainsWeapon / ContainsPassive ---------------------------------

        [Test]
        public void ContainsWeapon_True_When_Buffer_Has_Matching_Id()
        {
            var buffer = _world.EntityManager.GetBuffer<OwnedWeaponElement>(_entity);
            buffer.Add(new OwnedWeaponElement { Id = new FixedString64Bytes("weapon_demon_orb"), Level = 1, MaxLevel = 5 });

            Assert.That(UpgradeOptionUtility.ContainsWeapon(buffer, new FixedString64Bytes("weapon_demon_orb")),
                Is.True);
        }

        [Test]
        public void ContainsWeapon_False_When_Buffer_Empty_Or_Id_Missing()
        {
            var buffer = _world.EntityManager.GetBuffer<OwnedWeaponElement>(_entity);

            Assert.That(UpgradeOptionUtility.ContainsWeapon(buffer, new FixedString64Bytes("weapon_starfall")),
                Is.False, "empty buffer should never contain weapon");

            buffer.Add(new OwnedWeaponElement { Id = new FixedString64Bytes("weapon_other"), Level = 1, MaxLevel = 3 });

            Assert.That(UpgradeOptionUtility.ContainsWeapon(buffer, new FixedString64Bytes("weapon_starfall")),
                Is.False, "non-matching buffer should report absence");
        }

        [Test]
        public void ContainsPassive_Matches_Id_Case_Sensitively()
        {
            var buffer = _world.EntityManager.GetBuffer<OwnedPassiveElement>(_entity);
            buffer.Add(new OwnedPassiveElement { Id = new FixedString64Bytes("passive_swiftness"), Level = 2, MaxLevel = 5 });

            Assert.That(UpgradeOptionUtility.ContainsPassive(buffer, new FixedString64Bytes("passive_swiftness")), Is.True);
            Assert.That(UpgradeOptionUtility.ContainsPassive(buffer, new FixedString64Bytes("Passive_Swiftness")), Is.False,
                "FixedString64Bytes compares case-sensitively");
        }

        // --- ContainsOption overloads -----------------------------------------

        [Test]
        public void ContainsOption_By_Id_Matches_Regardless_Of_Kind()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            options.Add(new UpgradeOptionElement { Kind = UpgradeKind.Weapon, Id = new FixedString64Bytes("weapon_demon_orb") });

            Assert.That(UpgradeOptionUtility.ContainsOption(options, new FixedString64Bytes("weapon_demon_orb")), Is.True);
            Assert.That(UpgradeOptionUtility.ContainsOption(options, new FixedString64Bytes("missing_id")),      Is.False);
        }

        [Test]
        public void ContainsOption_By_Element_Matches_Both_Kind_And_Id()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            options.Add(new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("name_shared") });

            var weaponProbe  = new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("name_shared") };
            var passiveProbe = new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("name_shared") };

            Assert.That(UpgradeOptionUtility.ContainsOption(options, weaponProbe),  Is.True,
                "same id + same kind must match");
            Assert.That(UpgradeOptionUtility.ContainsOption(options, passiveProbe), Is.False,
                "same id but different kind must NOT match — kinds form a unique namespace");
        }

        // --- AddCandidate (List overload) -------------------------------------

        [Test]
        public void AddCandidate_Skips_Duplicate_Same_Kind_And_Id()
        {
            var candidates = new List<UpgradeOptionElement>();
            var item = new UpgradeOptionElement { Kind = UpgradeKind.Weapon, Id = new FixedString64Bytes("weapon_demon_orb") };

            UpgradeOptionUtility.AddCandidate(candidates, item);
            UpgradeOptionUtility.AddCandidate(candidates, item);
            UpgradeOptionUtility.AddCandidate(candidates, item);

            Assert.That(candidates.Count, Is.EqualTo(1), "AddCandidate must be idempotent for the same (Kind,Id)");
        }

        [Test]
        public void AddCandidate_Allows_Same_Id_With_Different_Kind()
        {
            var candidates = new List<UpgradeOptionElement>();
            UpgradeOptionUtility.AddCandidate(candidates,
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("shared_id") });
            UpgradeOptionUtility.AddCandidate(candidates,
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("shared_id") });

            Assert.That(candidates.Count, Is.EqualTo(2));
        }

        // --- HasFreshUnlock / HasOptionKind -----------------------------------

        [Test]
        public void HasFreshUnlock_True_Only_When_CurrentLevel_Is_Zero_With_Matching_Kind()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            options.Add(new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w1"), CurrentLevel = 2 });
            options.Add(new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1"), CurrentLevel = 0 });

            Assert.That(UpgradeOptionUtility.HasFreshUnlock(options, UpgradeKind.Weapon),  Is.False,
                "weapon option has CurrentLevel=2, no fresh unlock");
            Assert.That(UpgradeOptionUtility.HasFreshUnlock(options, UpgradeKind.Passive), Is.True,
                "passive option has CurrentLevel=0, counts as fresh unlock");
        }

        [Test]
        public void HasOptionKind_Distinguishes_Weapon_From_Passive()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            options.Add(new UpgradeOptionElement { Kind = UpgradeKind.Weapon, Id = new FixedString64Bytes("w1") });

            Assert.That(UpgradeOptionUtility.HasOptionKind(options, UpgradeKind.Weapon),  Is.True);
            Assert.That(UpgradeOptionUtility.HasOptionKind(options, UpgradeKind.Passive), Is.False);
        }

        // --- FindCandidateIndex -----------------------------------------------

        [Test]
        public void FindCandidateIndex_Returns_Minus_One_When_No_Candidate_Of_Kind()
        {
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon, Id = new FixedString64Bytes("w1") }
            };
            var previous = new List<UpgradeOptionElement>();

            int index = UpgradeOptionUtility.FindCandidateIndex(
                candidates, previous, offset: 0, requiredKind: UpgradeKind.Passive, includePrevious: false);

            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void FindCandidateIndex_Returns_Index_Of_Matching_Kind_Starting_From_Offset()
        {
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w1"), CurrentLevel = 1 },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1"), CurrentLevel = 0 },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p2"), CurrentLevel = 2 }
            };
            var previous = new List<UpgradeOptionElement>();

            int index = UpgradeOptionUtility.FindCandidateIndex(
                candidates, previous, offset: 0, requiredKind: UpgradeKind.Passive, includePrevious: false);

            // Offset 0 → indices iterated: 0,1,2. First Passive found at 1.
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void FindCandidateIndex_With_RequireFreshUnlock_Skips_Leveled_Candidates()
        {
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1"), CurrentLevel = 3 },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p2"), CurrentLevel = 0 }
            };
            var previous = new List<UpgradeOptionElement>();

            int index = UpgradeOptionUtility.FindCandidateIndex(
                candidates, previous, offset: 0,
                requiredKind: UpgradeKind.Passive, includePrevious: false,
                requireFreshUnlock: true);

            Assert.That(index, Is.EqualTo(1), "only candidate with CurrentLevel==0 qualifies as fresh unlock");
        }

        // --- DrawWeightedOptions (weighted random draw) -----------------------

        [Test]
        public void DrawWeight_Passive_Is_Higher_Than_Weapon()
        {
            Assert.That(UpgradeOptionUtility.DrawWeight(UpgradeKind.Passive),
                Is.GreaterThan(UpgradeOptionUtility.DrawWeight(UpgradeKind.Weapon)),
                "passives must be more likely to be offered than weapons");
        }

        [Test]
        public void DrawWeightedOptions_Draws_Three_Distinct_Cards_From_Larger_Pool()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w1") },
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w2") },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1") },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p2") },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p3") },
            };

            var rng = Unity.Mathematics.Random.CreateFromIndex(12345u);
            UpgradeOptionUtility.DrawWeightedOptions(
                ref options, candidates, new List<UpgradeOptionElement>(), ref rng, preferFreshOptions: false);

            Assert.That(options.Length, Is.EqualTo(3), "should fill 3 slots from a 5-candidate pool");

            var seen = new HashSet<string>();
            for (int i = 0; i < options.Length; i++)
            {
                Assert.That(seen.Add(options[i].Kind + ":" + options[i].Id), Is.True,
                    "drawn cards must be distinct (no duplicates)");
            }
        }

        [Test]
        public void DrawWeightedOptions_Caps_At_Candidate_Count_When_Fewer_Than_Three()
        {
            var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1") },
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w1") },
            };

            var rng = Unity.Mathematics.Random.CreateFromIndex(777u);
            UpgradeOptionUtility.DrawWeightedOptions(
                ref options, candidates, new List<UpgradeOptionElement>(), ref rng, preferFreshOptions: false);

            Assert.That(options.Length, Is.EqualTo(2), "cannot offer more cards than candidates");
        }

        [Test]
        public void DrawWeightedOptions_Favors_Passives_Statistically()
        {
            // One weapon + one passive candidate → both are drawn, but the FIRST
            // pick should favor the passive at PassiveDrawWeight:WeaponDrawWeight.
            // Bounds are derived from the weights so this test tracks tuning changes.
            var candidates = new List<UpgradeOptionElement>
            {
                new UpgradeOptionElement { Kind = UpgradeKind.Weapon,  Id = new FixedString64Bytes("w1") },
                new UpgradeOptionElement { Kind = UpgradeKind.Passive, Id = new FixedString64Bytes("p1") },
            };
            var previous = new List<UpgradeOptionElement>();
            var rng = Unity.Mathematics.Random.CreateFromIndex(0xABCDEFu);

            const int trials = 3000;
            int passiveFirst = 0;
            for (int t = 0; t < trials; t++)
            {
                var options = _world.EntityManager.GetBuffer<UpgradeOptionElement>(_entity);
                options.Clear();
                UpgradeOptionUtility.DrawWeightedOptions(
                    ref options, candidates, previous, ref rng, preferFreshOptions: false);

                if (options.Length > 0 && options[0].Kind == UpgradeKind.Passive)
                {
                    passiveFirst++;
                }
            }

            double expected = UpgradeOptionUtility.PassiveDrawWeight
                            / (UpgradeOptionUtility.PassiveDrawWeight + UpgradeOptionUtility.WeaponDrawWeight);
            double ratio = (double)passiveFirst / trials;
            Assert.That(ratio, Is.GreaterThan(expected - 0.05).And.LessThan(expected + 0.05),
                $"first-picked card should favor passives near {expected:P0}; observed {ratio:P1}");
            Assert.That(ratio, Is.GreaterThan(0.5), "passives must remain the majority of first picks");
        }
    }
}
