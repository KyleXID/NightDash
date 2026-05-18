// Sprint B / M3 — Player potion buffer.
// Polls NightDashPlayerInputRuntime for Q-key edge triggers. Each potion
// spend restores PotionHealAmount to CurrentHealth (capped at MaxHealth)
// and decrements PotionCount. PotionCount refills on run reset via
// RunTeardownBridge.ResetPlayerForNextRun.

using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PotionSystem : ISystem
    {
        // HP restored per potion spend. Tuned around the fallback player's
        // 100 MaxHealth so a single potion is meaningful but not full-heal.
        private const float PotionHealAmount = 40f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<CombatStats>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing)
            {
                // Drop any queued press so the player can't stockpile presses
                // while the game is paused / on results screen.
                NightDashPlayerInputRuntime.ConsumePotionRequest();
                return;
            }

            if (!NightDashPlayerInputRuntime.ConsumePotionRequest()) return;

            // Difficulty modifier: scale potion heal amount (no_heal sets this to 0).
            float healMultiplier = 1f;
            if (SystemAPI.HasSingleton<DifficultyState>())
            {
                healMultiplier = SystemAPI.GetSingleton<DifficultyState>().HealRateMultiplier;
            }
            float scaledHeal = PotionHealAmount * healMultiplier;

            foreach (var stats in SystemAPI.Query<RefRW<CombatStats>>().WithAll<PlayerTag>())
            {
                CombatStats s = stats.ValueRO;
                if (s.PotionCount <= 0) continue;
                if (s.CurrentHealth >= s.MaxHealth) continue; // No room — refuse spend.

                // Still consume the potion when healMultiplier==0 (no_heal) so the
                // modifier is visibly punitive rather than silently no-op-ing.
                s.CurrentHealth = math.min(s.MaxHealth, s.CurrentHealth + scaledHeal);
                s.PotionCount = math.max(0, s.PotionCount - 1);
                stats.ValueRW = s;
            }
        }
    }
}
