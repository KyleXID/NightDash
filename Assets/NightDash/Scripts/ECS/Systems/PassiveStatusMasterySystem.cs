// Reads the player's OwnedPassiveElement buffer and patches the per-hit
// status-effect apply chances on StatusEffectConfig each frame. The
// CombatSystem hit hook reads those chances directly — keeping the math
// in one place means designers tune the per-level percentage here and
// everything downstream picks it up.
//
// Mastery → status mapping:
//   passive_burn_mastery   → BurnApplyChance
//   passive_freeze_mastery → FreezeApplyChance
//   passive_poison_mastery → PoisonApplyChance
//   passive_stun_mastery   → StunApplyChance

using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(GameplaySimulationGroup))]
    [UpdateBefore(typeof(CombatSystem))]
    public partial struct PassiveStatusMasterySystem : ISystem
    {
        // Per-level apply chance. Lv.1 = 8%, Lv.5 = 28% with the +5% step.
        private const float BurnPerLevel   = 0.05f;
        private const float FreezePerLevel = 0.04f;
        private const float PoisonPerLevel = 0.05f;
        private const float StunPerLevel   = 0.03f;
        // Floor (Lv.1 base) — lets a single point in mastery already feel
        // meaningful instead of crawling up from near zero.
        private const float BurnFloor   = 0.03f;
        private const float FreezeFloor = 0.02f;
        private const float PoisonFloor = 0.03f;
        private const float StunFloor   = 0.02f;

        private const string BurnId   = "passive_burn_mastery";
        private const string FreezeId = "passive_freeze_mastery";
        private const string PoisonId = "passive_poison_mastery";
        private const string StunId   = "passive_stun_mastery";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StatusEffectConfig>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            // Reset to zero when the run is idle so a residual chance from
            // last run can't leak into the next teardown.
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing)
            {
                ResetChances();
                return;
            }

            int burnLv = 0, freezeLv = 0, poisonLv = 0, stunLv = 0;
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<OwnedPassiveElement>().Build();
            if (!query.IsEmptyIgnoreFilter)
            {
                Entity ent = query.GetSingletonEntity();
                DynamicBuffer<OwnedPassiveElement> owned =
                    state.EntityManager.GetBuffer<OwnedPassiveElement>(ent);
                for (int i = 0; i < owned.Length; i++)
                {
                    string id = owned[i].Id.ToString();
                    int lv = math.max(0, owned[i].Level);
                    if (id == BurnId)   burnLv   = lv;
                    if (id == FreezeId) freezeLv = lv;
                    if (id == PoisonId) poisonLv = lv;
                    if (id == StunId)   stunLv   = lv;
                }
            }

            RefRW<StatusEffectConfig> cfg = SystemAPI.GetSingletonRW<StatusEffectConfig>();
            cfg.ValueRW.BurnApplyChance   = burnLv   > 0 ? BurnFloor   + (burnLv   - 1) * BurnPerLevel   : 0f;
            cfg.ValueRW.FreezeApplyChance = freezeLv > 0 ? FreezeFloor + (freezeLv - 1) * FreezePerLevel : 0f;
            cfg.ValueRW.PoisonApplyChance = poisonLv > 0 ? PoisonFloor + (poisonLv - 1) * PoisonPerLevel : 0f;
            cfg.ValueRW.StunApplyChance   = stunLv   > 0 ? StunFloor   + (stunLv   - 1) * StunPerLevel   : 0f;
        }

        private void ResetChances()
        {
            if (!SystemAPI.HasSingleton<StatusEffectConfig>()) return;
            RefRW<StatusEffectConfig> cfg = SystemAPI.GetSingletonRW<StatusEffectConfig>();
            cfg.ValueRW.BurnApplyChance = 0f;
            cfg.ValueRW.FreezeApplyChance = 0f;
            cfg.ValueRW.PoisonApplyChance = 0f;
            cfg.ValueRW.StunApplyChance = 0f;
        }
    }
}
