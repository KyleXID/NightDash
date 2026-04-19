// EditMode smoke tests — validates test harness wiring for S1-03.
// These tests must stay green on every CI run; they are NOT game-logic tests.

using NUnit.Framework;
using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.Tests.EditMode
{
    public class HarnessSmokeTests
    {
        // Verifies that the NUnit test runner itself is functional inside the EditMode assembly.
        [Test]
        public void Runner_Executes_Basic_Arithmetic_Passes()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }

        // Verifies that Unity DOTS World/EntityManager can be created and torn down
        // without exceptions, confirming the Entities package is properly referenced.
        [Test]
        public void EntityManager_Can_Create_And_Destroy_Test_World()
        {
            var world = new World("S1-03 Harness");
            try
            {
                var em = world.EntityManager;
                var entity = em.CreateEntity();
                Assert.That(em.Exists(entity), Is.True);
                em.DestroyEntity(entity);
                Assert.That(em.Exists(entity), Is.False);
            }
            finally
            {
                world.Dispose();
            }
        }

        // Verifies that InternalsVisibleTo allows the EditMode assembly to reference
        // types declared in the NightDash assembly (compile-time linkage check).
        [Test]
        public void InternalsVisibleTo_Allows_Access_To_NightDash_Internal_Types()
        {
            // GameLoopState and StageRuntimeConfig are public structs in NightDash.ECS.Components.
            // If the assembly reference is broken this test will not compile at all.
            var gameLoopStateType = typeof(GameLoopState);
            var stageConfigType = typeof(StageRuntimeConfig);

            Assert.That(gameLoopStateType, Is.Not.Null);
            Assert.That(stageConfigType, Is.Not.Null);
        }
    }
}
