// S2-04: CombatHelpers unit tests.
// Targets the pure helpers extracted from CombatSystem during S2-02.
// The full CombatSystem OnUpdate path (AI, projectile collisions, death
// loop) still requires a richer world fixture and is covered by PlayMode
// integration tests.

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using NightDash.ECS.Components;
using NightDash.ECS.Systems;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class CombatHelpersTests
    {
        // ------------------------------------------------------------------
        // ResolveEnemyRewards — reward table regression tests.
        // ------------------------------------------------------------------

        [Test]
        public void ResolveEnemyRewards_Boss_Flag_Overrides_Archetype()
        {
            CombatHelpers.ResolveEnemyRewards(
                new FixedString64Bytes("ember_bat"), isBoss: true,
                out int gold, out int souls, out float xp);

            Assert.That(gold,  Is.EqualTo(25));
            Assert.That(souls, Is.EqualTo(6));
            Assert.That(xp,    Is.EqualTo(45f));
        }

        [TestCase("boss_agron",      25, 6, 45f)]
        [TestCase("wasteland_brute",  2, 2, 10f)]
        [TestCase("ash_caster",       2, 2,  9f)]
        [TestCase("ember_bat",        1, 1,  5f)]
        [TestCase("ghoul_scout",      1, 1,  6f)] // fallback branch
        [TestCase("",                 1, 1,  6f)] // unknown/empty → fallback
        public void ResolveEnemyRewards_Archetype_Table(
            string archetype, int expectedGold, int expectedSouls, float expectedXp)
        {
            CombatHelpers.ResolveEnemyRewards(
                new FixedString64Bytes(archetype), isBoss: false,
                out int gold, out int souls, out float xp);

            Assert.That(gold,  Is.EqualTo(expectedGold),  $"gold for '{archetype}'");
            Assert.That(souls, Is.EqualTo(expectedSouls), $"souls for '{archetype}'");
            Assert.That(xp,    Is.EqualTo(expectedXp),    $"xp for '{archetype}'");
        }

        [Test]
        public void ResolveEnemyRewards_Non_Boss_Values_Are_All_Strictly_Lower_Than_Boss()
        {
            // Sanity: boss rewards should dominate every non-boss archetype.
            CombatHelpers.ResolveEnemyRewards(
                new FixedString64Bytes("boss_agron"), isBoss: true,
                out int bossGold, out int bossSouls, out float bossXp);

            string[] nonBoss = { "wasteland_brute", "ash_caster", "ember_bat", "ghoul_scout" };
            foreach (var id in nonBoss)
            {
                CombatHelpers.ResolveEnemyRewards(
                    new FixedString64Bytes(id), isBoss: false,
                    out int gold, out int souls, out float xp);

                Assert.That(gold,  Is.LessThan(bossGold),  $"{id} gold");
                Assert.That(souls, Is.LessThan(bossSouls), $"{id} souls");
                Assert.That(xp,    Is.LessThan(bossXp),    $"{id} xp");
            }
        }

        // ------------------------------------------------------------------
        // ContainsEntity — dead-enemy dedup helper.
        // ------------------------------------------------------------------

        [Test]
        public void ContainsEntity_Returns_False_For_Empty_List()
        {
            using var entities = new NativeList<Entity>(4, Allocator.Temp);
            Assert.That(CombatHelpers.ContainsEntity(entities, new Entity { Index = 1, Version = 1 }), Is.False);
        }

        [Test]
        public void ContainsEntity_Matches_Both_Index_And_Version()
        {
            using var entities = new NativeList<Entity>(4, Allocator.Temp);
            var a = new Entity { Index = 7, Version = 2 };
            var b = new Entity { Index = 7, Version = 3 }; // same index, different version
            entities.Add(a);

            Assert.That(CombatHelpers.ContainsEntity(entities, a), Is.True);
            Assert.That(CombatHelpers.ContainsEntity(entities, b), Is.False,
                "entity equality must consider both Index and Version");
        }

        // ------------------------------------------------------------------
        // SpawnEnemyProjectile — command-buffer population contract.
        // ------------------------------------------------------------------

        [Test]
        public void SpawnEnemyProjectile_Populates_All_Required_Components()
        {
            var world = new World("S2-04 SpawnEnemyProjectile");
            try
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                float3 origin = new float3(0f, 0f, 0f);
                float3 target = new float3(5f, 0f, 0f);

                CombatHelpers.SpawnEnemyProjectile(ref ecb, origin, target, damage: 7f);
                ecb.Playback(world.EntityManager);
                ecb.Dispose();

                using var query = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<ProjectileData>(),
                    ComponentType.ReadOnly<PhysicsVelocity2D>());

                Assert.That(query.CalculateEntityCount(), Is.EqualTo(1),
                    "exactly one projectile entity with all three components");

                using var entities = query.ToEntityArray(Allocator.Temp);
                var projectile = world.EntityManager.GetComponentData<ProjectileData>(entities[0]);
                var velocity   = world.EntityManager.GetComponentData<PhysicsVelocity2D>(entities[0]);

                Assert.That(projectile.Damage,        Is.EqualTo(7f));
                Assert.That(projectile.IsPlayerOwned, Is.EqualTo((byte)0), "enemy-owned");
                Assert.That(projectile.IsMelee,       Is.EqualTo((byte)0));
                Assert.That(projectile.Lifetime,      Is.GreaterThan(0f));
                Assert.That(projectile.Radius,        Is.EqualTo(CombatHelpers.CasterProjectileRadius));

                // Velocity direction should be +X (origin→target unit vector) and magnitude == CasterProjectileSpeed.
                Assert.That(velocity.Value.x, Is.EqualTo(CombatHelpers.CasterProjectileSpeed).Within(0.0001f));
                Assert.That(math.abs(velocity.Value.y), Is.LessThan(0.0001f));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void SpawnEnemyProjectile_Falls_Back_To_Unit_X_When_Target_Equals_Origin()
        {
            var world = new World("S2-04 SpawnEnemyProjectile degenerate");
            try
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                float3 sameSpot = new float3(3f, 3f, 0f);

                CombatHelpers.SpawnEnemyProjectile(ref ecb, sameSpot, sameSpot, damage: 1f);
                ecb.Playback(world.EntityManager);
                ecb.Dispose();

                using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsVelocity2D>());
                using var entities = query.ToEntityArray(Allocator.Temp);
                var velocity = world.EntityManager.GetComponentData<PhysicsVelocity2D>(entities[0]);

                // Fallback direction is (1, 0) → velocity should be (CasterProjectileSpeed, 0).
                Assert.That(velocity.Value.x, Is.EqualTo(CombatHelpers.CasterProjectileSpeed).Within(0.0001f));
                Assert.That(velocity.Value.y, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
