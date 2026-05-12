using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(EnemySpawnSystem))]
    public partial struct PlayerMovementSystem : ISystem
    {
        // Dash tuning. While DashTimer > 0 the player moves at a multiple
        // of MoveSpeed; afterwards the cooldown ticks down before another
        // dash can fire. DashDuration is short enough that the burst feels
        // like a snap, not a sustained sprint.
        private const float DashDuration = 0.18f;
        private const float DashCooldown = 1.6f;
        private const float DashSpeedMultiplier = 3.2f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<StageRuntimeConfig>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<CombatStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameLoopState loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.IsRunActive == 0 || loop.Status != RunStatus.Playing)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            StageRuntimeConfig stage = SystemAPI.GetSingleton<StageRuntimeConfig>();
            float2 input = new float2(NightDashPlayerInputRuntime.MoveAxis.x, NightDashPlayerInputRuntime.MoveAxis.y);
            bool dashRequested = NightDashPlayerInputRuntime.ConsumeDashRequest();

            foreach (var (transform, stats) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<CombatStats>>().WithAll<PlayerTag>())
            {
                CombatStats s = stats.ValueRO;

                // Tick down dash state every frame so the burst ends + the
                // cooldown elapses even when the player is idle.
                if (s.DashTimer > 0f) s.DashTimer = math.max(0f, s.DashTimer - dt);
                if (s.DashCooldownRemaining > 0f) s.DashCooldownRemaining = math.max(0f, s.DashCooldownRemaining - dt);

                // Edge-triggered dash. Requires (1) directional input so we
                // know which way to dash, and (2) cooldown ready. We do NOT
                // require DashTimer==0 so weaving inputs feels responsive.
                if (dashRequested && math.lengthsq(input) > 0f && s.DashCooldownRemaining <= 0f)
                {
                    s.DashTimer = DashDuration;
                    s.DashCooldownRemaining = DashCooldown;
                }

                stats.ValueRW = s;

                if (math.lengthsq(input) <= 0f)
                {
                    continue;
                }

                float2 dir = math.normalize(input);
                float speed = s.MoveSpeed;
                if (s.DashTimer > 0f) speed *= DashSpeedMultiplier;

                float3 delta = new float3(dir.x, dir.y, 0f) * speed * dt;
                float3 position = transform.ValueRO.Position + delta;
                if (stage.UseBounds == 1)
                {
                    position.x = math.clamp(position.x, stage.BoundsMin.x, stage.BoundsMax.x);
                    position.y = math.clamp(position.y, stage.BoundsMin.y, stage.BoundsMax.y);
                }

                transform.ValueRW.Position = position;
            }
        }
    }
}
