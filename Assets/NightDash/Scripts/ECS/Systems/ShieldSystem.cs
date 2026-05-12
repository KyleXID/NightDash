// Sprint B / M3 — Player shield buffer.
// Damage absorption happens in CombatSystem.ApplyDamageWithShield. This
// system advances the time-since-last-hit counter and regenerates the
// shield while the player has been clear of incoming damage for a grace
// window. Boss and enemy stats are untouched (shield is player-only).

using NightDash.ECS.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ShieldSystem : ISystem
    {
        // Seconds the player must stay un-hit before shield begins to refill.
        private const float RegenGraceSeconds = 2.5f;
        // Shield points restored per second once regen is active.
        private const float RegenPerSecond = 8f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<CombatStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            foreach (var stats in SystemAPI.Query<RefRW<CombatStats>>().WithAll<PlayerTag>())
            {
                CombatStats s = stats.ValueRO;
                if (s.MaxShield <= 0f)
                {
                    // No shield configured for this player yet — leave stats
                    // alone so MetaProgression or upgrades can grow MaxShield
                    // later without surprise resets.
                    continue;
                }

                s.TimeSinceLastHit += dt;

                if (s.TimeSinceLastHit >= RegenGraceSeconds && s.CurrentShield < s.MaxShield)
                {
                    s.CurrentShield = math.min(s.MaxShield, s.CurrentShield + RegenPerSecond * dt);
                }

                stats.ValueRW = s;
            }
        }
    }
}
