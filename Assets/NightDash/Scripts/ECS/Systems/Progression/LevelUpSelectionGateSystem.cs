using NightDash.ECS.Components;
using Unity.Entities;

namespace NightDash.ECS.Systems.Progression
{
    /// <summary>
    /// M1 — Gate system that transitions Status from Playing to LevelUpSelection
    /// when PendingLevelUps > 0. All further option generation and selection
    /// handling is delegated to UpgradeOptionGeneratorSystem (M2) and
    /// UpgradeApplySystem (M3) which run after this system in the same frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct LevelUpSelectionGateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();

            if (loop.ValueRO.PendingLevelUps > 0 && loop.ValueRO.Status == RunStatus.Playing)
            {
                loop.ValueRW.Status = RunStatus.LevelUpSelection;
            }
        }
    }
}
