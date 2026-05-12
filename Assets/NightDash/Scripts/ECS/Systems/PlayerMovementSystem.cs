using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Collections;
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

        // Caster classes (mage / astrologer) replace the sprint with an
        // instant teleport in the move direction. TeleportDistance is in
        // world units; tuned to roughly match the distance a regular dash
        // would cover (DashDuration × MoveSpeed × DashSpeedMultiplier).
        private const float TeleportDistance = 3.6f;

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

            // Caster check — mage / astrologer get the instant-teleport
            // dash variant instead of the sprint burst.
            bool isCaster = false;
            if (SystemAPI.HasSingleton<RunSelection>())
            {
                FixedString64Bytes cls = SystemAPI.GetSingleton<RunSelection>().ClassId;
                isCaster = cls == "class_mage" || cls == "class_astrologer";
            }

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
                bool dashFiredThisFrame = false;
                if (dashRequested && math.lengthsq(input) > 0f && s.DashCooldownRemaining <= 0f)
                {
                    s.DashTimer = DashDuration;
                    s.DashCooldownRemaining = DashCooldown;
                    dashFiredThisFrame = true;
                }

                float2 inputDir = math.lengthsq(input) > 0f ? math.normalize(input) : float2.zero;
                float3 position = transform.ValueRO.Position;

                // Caster teleport: on the frame the dash fires, snap the
                // player a fixed distance in the input direction. Stash the
                // start position so the trail bridge can drop a fan of
                // afterimages along the teleport path.
                if (isCaster && dashFiredThisFrame)
                {
                    float3 startPos = position;
                    float3 endPos = position + new float3(inputDir.x, inputDir.y, 0f) * TeleportDistance;
                    if (stage.UseBounds == 1)
                    {
                        endPos.x = math.clamp(endPos.x, stage.BoundsMin.x, stage.BoundsMax.x);
                        endPos.y = math.clamp(endPos.y, stage.BoundsMin.y, stage.BoundsMax.y);
                    }
                    transform.ValueRW.Position = endPos;
                    stats.ValueRW = s;
                    NightDashTeleportEvents.Fire(startPos, endPos);
                    continue;
                }

                stats.ValueRW = s;

                if (math.lengthsq(input) <= 0f)
                {
                    continue;
                }

                float speed = s.MoveSpeed;
                // Non-caster classes get the sprint burst during DashTimer.
                if (!isCaster && s.DashTimer > 0f) speed *= DashSpeedMultiplier;

                float3 delta = new float3(inputDir.x, inputDir.y, 0f) * speed * dt;
                position += delta;
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
