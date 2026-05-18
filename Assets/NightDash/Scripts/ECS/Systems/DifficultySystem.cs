using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct DifficultySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DifficultyState>();
            state.RequireForUpdate<DifficultyModifierElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (difficulty, modifiers) in SystemAPI
                         .Query<RefRW<DifficultyState>, DynamicBuffer<DifficultyModifierElement>>())
            {
                int risk = 0;
                float rewardMultiplier = 1f;
                float hpPctSum = 0f;
                float speedPctSum = 0f;
                float spawnRatePctSum = 0f;
                float healRatePctSum = 0f;
                float cooldownPctSum = 0f;
                float hazardSum = 0f;
                byte onKillExplosion = 0;

                for (int i = 0; i < modifiers.Length; i++)
                {
                    DifficultyModifierElement m = modifiers[i];
                    risk += m.RiskScore;
                    rewardMultiplier += m.RewardMultiplierBonus;
                    hpPctSum += m.HpPct;
                    speedPctSum += m.MoveSpeedPct;
                    spawnRatePctSum += m.SpawnRatePct;
                    healRatePctSum += m.HealRatePct;
                    cooldownPctSum += m.CooldownPct;
                    hazardSum += m.HazardMultiplier;
                    if (m.OnKillExplosion != 0) onKillExplosion = 1;
                }

                difficulty.ValueRW.RiskScore = risk;
                difficulty.ValueRW.RewardMultiplier = rewardMultiplier;
                // Multipliers clamp to a sane floor so a stack of negative pct
                // doesn't invert behavior (e.g. negative enemy HP).
                difficulty.ValueRW.EnemyHpMultiplier = math.max(0.1f, 1f + hpPctSum);
                difficulty.ValueRW.EnemySpeedMultiplier = math.max(0.1f, 1f + speedPctSum);
                difficulty.ValueRW.SpawnRateMultiplier = math.max(0.1f, 1f + spawnRatePctSum);
                // HealRate clamps at 0 — sum of -1.0 (no_heal) drives healing off entirely
                // rather than reversing into damage.
                difficulty.ValueRW.HealRateMultiplier = math.max(0f, 1f + healRatePctSum);
                // CooldownPct is intentionally interpreted so positive pct = longer cooldown
                // (harder). To make a -0.2 (faster) modifier valid, we add. Floor at 0.1
                // so weapons still tick at a sane rate.
                difficulty.ValueRW.CooldownMultiplier = math.max(0.1f, 1f + cooldownPctSum);
                difficulty.ValueRW.HazardMultiplier = math.max(0f, 1f + hazardSum);
                difficulty.ValueRW.OnKillExplosionEnabled = onKillExplosion;
                break;
            }
        }
    }
}
