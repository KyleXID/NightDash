// Status-effect ECS data — burn, freeze, poison, stun.
//
// All four effects share a single struct so we can attach one component per
// entity instead of four. Active bits live in `ActiveMask`; remaining time
// and DoT tick accumulators are per-effect floats so updates stay branch-
// friendly inside the burst-compiled tick loop.

using Unity.Entities;

namespace NightDash.ECS.Components
{
    // Bit positions matching StatusEffectKind for the ActiveMask field on
    // StatusEffectState. Defined as a separate static so callers don't have
    // to know about (byte) casts.
    public static class StatusEffectBits
    {
        public const byte Burn   = 1 << 0;
        public const byte Freeze = 1 << 1;
        public const byte Poison = 1 << 2;
        public const byte Stun   = 1 << 3;
    }

    public enum StatusEffectKind : byte
    {
        Burn   = 0,
        Freeze = 1,
        Poison = 2,
        Stun   = 3,
    }

    // Per-entity active effect state. Attached lazily to enemies / boss /
    // player only when at least one effect is applied; StatusEffectSystem
    // removes the component once everything decays so most enemies pay no
    // archetype cost.
    public struct StatusEffectState : IComponentData
    {
        public byte ActiveMask;

        public float BurnRemaining;
        public float BurnTickAccum;

        public float FreezeRemaining;

        public float PoisonRemaining;
        public float PoisonTickAccum;

        public float StunRemaining;
    }

    // Singleton tuning — gameplay values shared by every applied effect.
    // Initialised in NightDashRuntimeBootstrapSystem alongside the other
    // gameplay singletons. BossImmunityMask lets us blanket-immune freeze
    // and stun on bosses while keeping DoT enabled.
    public struct StatusEffectConfig : IComponentData
    {
        public float BurnDamagePerTick;
        public float BurnTickInterval;
        public float BurnDuration;

        public float PoisonDamagePerTick;
        public float PoisonTickInterval;
        public float PoisonDuration;

        public float FreezeDuration;
        public float StunDuration;

        // Probability (0..1) of the corresponding effect rolling on a
        // successful weapon hit. Tuning knob for the mock apply hook in
        // WeaponSystem.
        public float BurnApplyChance;
        public float PoisonApplyChance;
        public float FreezeApplyChance;
        public float StunApplyChance;

        // Bitfield of effects that bosses are immune to.
        public byte BossImmunityMask;
    }
}
