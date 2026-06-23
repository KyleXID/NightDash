// Evolution-selection unit tests.
// Purpose: lock down EvolutionUtility.ResolveEvolutionFor — the pure decision
// logic that picks which first-tier ("_evolved") evolution a weapon qualifies
// for. Conditions: base weapon at max level AND every required passive at max
// level. Debug force (requireConditions: false) bypasses both. Abyss forms are
// never returned by this path. No ECS world is needed.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NightDash.Data;
using NightDash.ECS.Systems; // internal — allowed via [InternalsVisibleTo("NightDash.Tests.EditMode")]

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class EvolutionResolverTests
    {
        private readonly List<EvolutionData> _created = new();

        private EvolutionData MakeEvolution(
            string id, string requiredWeaponId, string resultWeaponId,
            bool isAbyss = false, int priority = 10, params string[] requiredPassives)
        {
            var e = ScriptableObject.CreateInstance<EvolutionData>();
            e.id = id;
            e.requiredWeaponId = requiredWeaponId;
            e.resultWeaponId = resultWeaponId;
            e.isAbyss = isAbyss;
            e.priority = priority;
            e.requiredPassiveIds = new List<string>(requiredPassives);
            _created.Add(e);
            return e;
        }

        private static HashSet<string> Maxed(params string[] ids) => new HashSet<string>(ids);

        [TearDown]
        public void Cleanup()
        {
            foreach (var e in _created)
            {
                if (e != null) Object.DestroyImmediate(e);
            }
            _created.Clear();
        }

        [Test]
        public void Qualifies_When_Weapon_And_Passives_Maxed()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_gs", "weapon_demon_greatsword", "weapon_demon_greatsword_evolved",
                    isAbyss: false, priority: 10, "strength"),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_demon_greatsword",
                weaponMaxed: true, Maxed("strength"), requireConditions: true);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("weapon_demon_greatsword_evolved", chosen.resultWeaponId);
        }

        [Test]
        public void Weapon_Not_Maxed_Returns_Null()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_gs", "weapon_demon_greatsword", "weapon_demon_greatsword_evolved",
                    requiredPassives: "strength"),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_demon_greatsword",
                weaponMaxed: false, Maxed("strength"), requireConditions: true);

            Assert.IsNull(chosen);
        }

        [Test]
        public void Required_Passive_Not_Maxed_Returns_Null()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_gs", "weapon_demon_greatsword", "weapon_demon_greatsword_evolved",
                    requiredPassives: "strength"),
            };

            // "strength" owned but NOT at max → not in the maxed set.
            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_demon_greatsword",
                weaponMaxed: true, Maxed("focus"), requireConditions: true);

            Assert.IsNull(chosen);
        }

        [Test]
        public void Force_Bypasses_All_Conditions()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_gs", "weapon_demon_greatsword", "weapon_demon_greatsword_evolved",
                    requiredPassives: "strength"),
            };

            // Not maxed, no passives — but force (requireConditions: false) evolves anyway.
            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_demon_greatsword",
                weaponMaxed: false, Maxed(), requireConditions: false);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("weapon_demon_greatsword_evolved", chosen.resultWeaponId);
        }

        [Test]
        public void Wrong_Weapon_Returns_Null()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_gs", "weapon_demon_greatsword", "weapon_demon_greatsword_evolved"),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_chain_scythe",
                weaponMaxed: true, Maxed(), requireConditions: true);

            Assert.IsNull(chosen);
        }

        [Test]
        public void Abyss_Only_Match_Returns_Null()
        {
            // The auto/debug path targets the first tier only; an abyss-only match
            // must not be selected.
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_a", "weapon_chain_scythe", "weapon_chain_scythe_abyss", isAbyss: true),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_chain_scythe",
                weaponMaxed: true, Maxed(), requireConditions: true);

            Assert.IsNull(chosen);
        }

        [Test]
        public void Picks_Normal_Over_Abyss_For_Same_Weapon()
        {
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_a", "weapon_chain_scythe", "weapon_chain_scythe_abyss", isAbyss: true, priority: 20),
                MakeEvolution("evo_n", "weapon_chain_scythe", "weapon_chain_scythe_evolved", isAbyss: false, priority: 10),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_chain_scythe",
                weaponMaxed: true, Maxed(), requireConditions: true);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("weapon_chain_scythe_evolved", chosen.resultWeaponId);
        }

        [Test]
        public void Highest_Priority_Normal_Wins()
        {
            // Mirrors starfall: two normal evolutions on the same base weapon.
            // weapon_starfall_evolved (priority 10) must beat weapon_void_starfall (priority 9).
            var evolutions = new List<EvolutionData>
            {
                MakeEvolution("evo_void", "weapon_starfall", "weapon_void_starfall", isAbyss: false, priority: 9),
                MakeEvolution("evo_star", "weapon_starfall", "weapon_starfall_evolved", isAbyss: false, priority: 10),
            };

            var chosen = EvolutionUtility.ResolveEvolutionFor(
                evolutions, "weapon_starfall",
                weaponMaxed: true, Maxed(), requireConditions: true);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("weapon_starfall_evolved", chosen.resultWeaponId);
        }
    }
}
