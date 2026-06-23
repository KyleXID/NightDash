// ============================================================================
// WeaponBehaviorComponents.cs
// Vampire-Survivors-style weapon behavior archetypes layered on top of the
// base projectile entity. Default values keep the original straight-line,
// single-hit projectile behavior so existing weapons are unaffected.
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Components
{
    // Movement archetype for a player weapon entity. Stored as a byte on
    // ProjectileData (0 = Linear, the original behavior).
    public enum ProjectileBehavior : byte
    {
        Linear     = 0, // travels along PhysicsVelocity2D (bullets, arrows, melee, sky-fall descent)
        Orbit      = 1, // re-anchored to the player each frame (ring, spinning blades, barrier)
        GroundZone = 2, // stationary; persists in place where it was spawned (abyss tentacle)
        Whip       = 3, // extends out from the player and retracts (chain scythe), damaging along the path
    }

    // Present only on Orbit-behavior weapon entities. The orbit center is the
    // live player position (recomputed every frame) so the weapon stays
    // attached as the player moves. Radius 0 = centered on the player
    // (e.g. the barrier / the big light ring).
    // Present on Whip-behavior weapon entities (chain scythe). The blade's
    // distance from the player follows sin(π·t) over its lifetime — 0 → MaxReach
    // → 0 — so it shoots out along Direction and snaps back like a rubber band,
    // damaging everything along the path.
    public struct WhipState : IComponentData
    {
        public float2 Direction;     // fixed aim direction (toward the target at spawn)
        public float MaxReach;       // peak extension distance from the player
        public float TotalLifetime;  // full extend+retract duration (drives the sin phase)
    }

    // Present on a chain-lightning projectile (evolved dark lightning / 암흑번개).
    // Each enemy the bolt damages also arcs reduced damage to nearby enemies.
    public struct ChainLightningState : IComponentData
    {
        public float Radius; // arc reach from each struck enemy
        public float Factor; // fraction of the bolt's damage dealt to arced enemies (0..1)
    }

    // Present on a ricocheting projectile (evolved demon orb / 심연파열구). On
    // contact it damages one enemy, then redirects toward the next nearest enemy
    // it has not hit yet, until Remaining hits are spent or none is in Range.
    public struct BounceState : IComponentData
    {
        public int Remaining; // remaining enemies it can still hit (incl. the current one)
        public float Range;   // max distance to seek the next bounce target
    }

    public struct OrbitState : IComponentData
    {
        public float Radius;        // distance from the player, in world units
        public float AngularSpeed;  // radians per second
        public float Angle;         // current angle, radians
        public float CenterYOffset; // raises the orbit center above the player (e.g. ring/barrier sit at the torso, not the feet)
        public float RadiusGrowth;  // world units/sec the radius expands (0 = fixed orbit; >0 = spirals outward, e.g. light ring)
    }
}
