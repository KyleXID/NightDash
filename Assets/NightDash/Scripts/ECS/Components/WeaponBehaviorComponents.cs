// ============================================================================
// WeaponBehaviorComponents.cs
// Vampire-Survivors-style weapon behavior archetypes layered on top of the
// base projectile entity. Default values keep the original straight-line,
// single-hit projectile behavior so existing weapons are unaffected.
// ============================================================================

using Unity.Entities;

namespace NightDash.ECS.Components
{
    // Movement archetype for a player weapon entity. Stored as a byte on
    // ProjectileData (0 = Linear, the original behavior).
    public enum ProjectileBehavior : byte
    {
        Linear     = 0, // travels along PhysicsVelocity2D (bullets, arrows, melee, sky-fall descent)
        Orbit      = 1, // re-anchored to the player each frame (ring, spinning blades, barrier)
        GroundZone = 2, // stationary; persists in place where it was spawned (abyss tentacle)
    }

    // Present only on Orbit-behavior weapon entities. The orbit center is the
    // live player position (recomputed every frame) so the weapon stays
    // attached as the player moves. Radius 0 = centered on the player
    // (e.g. the barrier / the big light ring).
    public struct OrbitState : IComponentData
    {
        public float Radius;       // distance from the player, in world units
        public float AngularSpeed; // radians per second
        public float Angle;        // current angle, radians
    }
}
