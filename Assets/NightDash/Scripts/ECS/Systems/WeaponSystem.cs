using Unity.Burst;
using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial struct WeaponSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<WeaponRuntimeData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<GameLoopState>().IsRunActive == 0)
            {
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;

            foreach (var weapon in SystemAPI.Query<RefRW<WeaponRuntimeData>>().WithAll<PlayerTag>())
            {
                weapon.ValueRW.CooldownRemaining -= dt;
                if (weapon.ValueRO.CooldownRemaining <= 0f)
                {
                    // MVP: actual projectile spawn will be added when weapon content data is connected.
                    weapon.ValueRW.CooldownRemaining = weapon.ValueRO.Cooldown;
                }
            }
        }
    }
}
