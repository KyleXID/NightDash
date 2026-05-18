// Risk-return passive: passive_life_drain.
//
// Heals the player a percentage of their max HP each time an enemy dies.
// Drives off RunResultStats.KillCount (already incremented by CombatSystem)
// so we don't need a parallel event channel — the system just watches the
// counter and converts the delta into health.

using NightDash.Data;
using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    // CombatSystem lives in SimulationSystemGroup; if we both anchor to the
    // same group the ordering attribute is valid, but using the gameplay
    // wrapper here would orphan the ordering ([UpdateAfter] only works
    // within one group). Drop the explicit ordering and just sit in the
    // simulation group — KillCount tracking is delta-based so a 1-frame lag
    // before the heal lands is fine.
    [UpdateInGroup(typeof(GameplaySimulationGroup))]
    public partial struct PassiveLifeDrainSystem : ISystem
    {
        // Two passives share the "heal on kill" hook. life_drain (the
        // risk-return version) heals more per kill; lifesteal (mechanic
        // category) heals less per kill. Both stack additively if the
        // player somehow has both.
        private const float LifeDrainPerLevelPercent = 0.03f;
        private const float LifeStealPerLevelPercent = 0.015f;
        private const string LifeDrainId = "passive_life_drain";
        private const string LifeStealId = "passive_lifesteal";

        private int _prevKillCount;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunResultStats>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing)
            {
                // Reset baseline so a fresh run doesn't dump heals for kills
                // accumulated in the previous one.
                _prevKillCount = 0;
                _initialized = false;
                return;
            }

            RunResultStats stats = SystemAPI.GetSingleton<RunResultStats>();
            if (!_initialized)
            {
                _prevKillCount = stats.KillCount;
                _initialized = true;
                return;
            }

            int newKills = stats.KillCount - _prevKillCount;
            _prevKillCount = stats.KillCount;
            if (newKills <= 0) return;

            float perKillPercent = GetCombinedHealPerKill(ref state);
            if (perKillPercent <= 0f) return;

            // Apply the heal to the player's CombatStats. PlayerTag is a
            // singleton in practice, so SystemAPI.Query is fine here.
            foreach (var stats4 in SystemAPI.Query<RefRW<CombatStats>>().WithAll<PlayerTag>())
            {
                CombatStats cs = stats4.ValueRO;
                if (cs.MaxHealth <= 0f) break;
                float healAmount = cs.MaxHealth * perKillPercent * newKills;
                cs.CurrentHealth = math.min(cs.MaxHealth, cs.CurrentHealth + healAmount);
                stats4.ValueRW = cs;
                break;
            }
        }

        private float GetCombinedHealPerKill(ref SystemState state)
        {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<OwnedPassiveElement>().Build();
            if (query.IsEmptyIgnoreFilter) return 0f;
            var entity = query.GetSingletonEntity();
            DynamicBuffer<OwnedPassiveElement> owned =
                state.EntityManager.GetBuffer<OwnedPassiveElement>(entity);
            float total = 0f;
            for (int i = 0; i < owned.Length; i++)
            {
                string id = owned[i].Id.ToString();
                int level = math.max(1, owned[i].Level);
                if (id == LifeDrainId)
                    total += LifeDrainPerLevelPercent * level;
                else if (id == LifeStealId)
                    total += LifeStealPerLevelPercent * level;
            }
            return total;
        }
    }
}
