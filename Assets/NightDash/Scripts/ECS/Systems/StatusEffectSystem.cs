// Status-effect tick + decay system.
//
// Runs in GameplaySimulationGroup BEFORE CombatSystem so that movement /
// projectile gates inside CombatSystem can read the fresh ActiveMask each
// frame. DoT effects subtract from CurrentHealth directly (shield is by
// design bypassed for now — "internal damage"). Once every timer falls to
// zero the StatusEffectState component is removed via ECB so most enemies
// pay no archetype cost.

using NightDash.ECS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(GameplaySimulationGroup))]
    [UpdateBefore(typeof(CombatSystem))]
    public partial struct StatusEffectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StatusEffectConfig>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing) return;

            StatusEffectConfig cfg = SystemAPI.GetSingleton<StatusEffectConfig>();
            // Clamp dt so an editor hiccup or background resume doesn't dump
            // a dozen DoT ticks at once. 0.2s is a generous frame budget.
            float dt = math.min(SystemAPI.Time.DeltaTime, 0.2f);
            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (sfx, stats, entity) in SystemAPI
                .Query<RefRW<StatusEffectState>, RefRW<CombatStats>>()
                .WithEntityAccess())
            {
                ref StatusEffectState s = ref sfx.ValueRW;
                byte mask = s.ActiveMask;
                if (mask == 0)
                {
                    ecb.RemoveComponent<StatusEffectState>(entity);
                    continue;
                }

                // Burn
                if ((mask & StatusEffectBits.Burn) != 0)
                {
                    s.BurnRemaining -= dt;
                    s.BurnTickAccum += dt;
                    while (s.BurnTickAccum >= cfg.BurnTickInterval && s.BurnRemaining > 0f)
                    {
                        ApplyDot(ref stats.ValueRW, cfg.BurnDamagePerTick);
                        s.BurnTickAccum -= cfg.BurnTickInterval;
                    }
                    if (s.BurnRemaining <= 0f)
                    {
                        s.BurnRemaining = 0f;
                        s.BurnTickAccum = 0f;
                        mask &= unchecked((byte)~StatusEffectBits.Burn);
                    }
                }

                // Poison
                if ((mask & StatusEffectBits.Poison) != 0)
                {
                    s.PoisonRemaining -= dt;
                    s.PoisonTickAccum += dt;
                    while (s.PoisonTickAccum >= cfg.PoisonTickInterval && s.PoisonRemaining > 0f)
                    {
                        ApplyDot(ref stats.ValueRW, cfg.PoisonDamagePerTick);
                        s.PoisonTickAccum -= cfg.PoisonTickInterval;
                    }
                    if (s.PoisonRemaining <= 0f)
                    {
                        s.PoisonRemaining = 0f;
                        s.PoisonTickAccum = 0f;
                        mask &= unchecked((byte)~StatusEffectBits.Poison);
                    }
                }

                // Freeze
                if ((mask & StatusEffectBits.Freeze) != 0)
                {
                    s.FreezeRemaining -= dt;
                    if (s.FreezeRemaining <= 0f)
                    {
                        s.FreezeRemaining = 0f;
                        mask &= unchecked((byte)~StatusEffectBits.Freeze);
                    }
                }

                // Stun
                if ((mask & StatusEffectBits.Stun) != 0)
                {
                    s.StunRemaining -= dt;
                    if (s.StunRemaining <= 0f)
                    {
                        s.StunRemaining = 0f;
                        mask &= unchecked((byte)~StatusEffectBits.Stun);
                    }
                }

                s.ActiveMask = mask;
                if (mask == 0)
                {
                    ecb.RemoveComponent<StatusEffectState>(entity);
                }
            }
        }

        private static void ApplyDot(ref CombatStats stats, float damage)
        {
            // Shield bypass — DoT represents "inside the body" damage.
            stats.CurrentHealth = math.max(0f, stats.CurrentHealth - damage);
        }

        // ----------------------------------------------------------------
        // Static apply helper (used by WeaponSystem / CombatSystem etc.)
        // ----------------------------------------------------------------
        public static void ApplyEffect(
            EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            EntityManager em,
            Entity target,
            StatusEffectKind kind,
            StatusEffectConfig cfg,
            bool isBoss)
        {
            byte bit = KindBit(kind);
            if (isBoss && (cfg.BossImmunityMask & bit) != 0) return;

            StatusEffectState s = em.HasComponent<StatusEffectState>(target)
                ? em.GetComponentData<StatusEffectState>(target)
                : default;

            ApplyRefresh(ref s, kind, cfg);

            if (em.HasComponent<StatusEffectState>(target))
            {
                ecb.SetComponent(sortKey, target, s);
            }
            else
            {
                ecb.AddComponent(sortKey, target, s);
            }
        }

        // Non-parallel variant — writes synchronously through the
        // EntityManager so the component is queryable the same frame
        // (no ECB playback delay). Callers must be on the main thread.
        public static void ApplyEffect(
            EntityManager em,
            Entity target,
            StatusEffectKind kind,
            StatusEffectConfig cfg,
            bool isBoss)
        {
            byte bit = KindBit(kind);
            if (isBoss && (cfg.BossImmunityMask & bit) != 0) return;

            bool has = em.HasComponent<StatusEffectState>(target);
            StatusEffectState s = has
                ? em.GetComponentData<StatusEffectState>(target)
                : default;

            ApplyRefresh(ref s, kind, cfg);

            if (has)
            {
                em.SetComponentData(target, s);
            }
            else
            {
                em.AddComponentData(target, s);
            }
        }

        private static byte KindBit(StatusEffectKind kind) => kind switch
        {
            StatusEffectKind.Burn   => StatusEffectBits.Burn,
            StatusEffectKind.Freeze => StatusEffectBits.Freeze,
            StatusEffectKind.Poison => StatusEffectBits.Poison,
            StatusEffectKind.Stun   => StatusEffectBits.Stun,
            _ => 0,
        };

        private static void ApplyRefresh(ref StatusEffectState s, StatusEffectKind kind, StatusEffectConfig cfg)
        {
            switch (kind)
            {
                case StatusEffectKind.Burn:
                    s.ActiveMask |= StatusEffectBits.Burn;
                    s.BurnRemaining = math.max(s.BurnRemaining, cfg.BurnDuration);
                    if (s.BurnTickAccum <= 0f) s.BurnTickAccum = 0f;
                    break;
                case StatusEffectKind.Poison:
                    s.ActiveMask |= StatusEffectBits.Poison;
                    s.PoisonRemaining = math.max(s.PoisonRemaining, cfg.PoisonDuration);
                    if (s.PoisonTickAccum <= 0f) s.PoisonTickAccum = 0f;
                    break;
                case StatusEffectKind.Freeze:
                    s.ActiveMask |= StatusEffectBits.Freeze;
                    s.FreezeRemaining = math.max(s.FreezeRemaining, cfg.FreezeDuration);
                    break;
                case StatusEffectKind.Stun:
                    s.ActiveMask |= StatusEffectBits.Stun;
                    s.StunRemaining = math.max(s.StunRemaining, cfg.StunDuration);
                    break;
            }
        }
    }
}
