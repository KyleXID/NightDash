// Sprint B / M0 — Gameplay simulation group.
// Wraps gameplay-only systems (movement, spawn, combat, progression) so a
// single GameplayPauseTag toggle pauses simulation without touching input
// or UI bridges. Systems opt in via [UpdateInGroup(typeof(GameplaySimulationGroup))].
// Migration of existing systems is deferred to M3 (Pause Menu integration).

using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GameplaySimulationGroup : ComponentSystemGroup
    {
        private EntityQuery _pauseQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _pauseQuery = GetEntityQuery(ComponentType.ReadOnly<GameplayPauseTag>());
        }

        protected override void OnUpdate()
        {
            // Skip child systems while the pause tag exists in the world.
            if (!_pauseQuery.IsEmpty) return;
            base.OnUpdate();
        }
    }
}
