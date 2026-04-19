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
    }
}
