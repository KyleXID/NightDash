// Sprint B / M0 — Gameplay pause signal.
// Singleton tag. Presence in the world signals GameplaySimulationGroup to
// skip its children. Created/destroyed by NightDashPauseMenuUI via
// IComponentData add/remove.

using Unity.Entities;

namespace NightDash.ECS.Components
{
    public struct GameplayPauseTag : IComponentData { }
}
