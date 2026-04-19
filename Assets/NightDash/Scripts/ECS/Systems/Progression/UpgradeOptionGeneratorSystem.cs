using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems.Progression
{
    /// <summary>
    /// M2 — Fills the UpgradeOptionElement buffer with up to 3 options whenever:
    ///   - Status == LevelUpSelection and the buffer is empty (initial generation), or
    ///   - RerollRequested == 1 (player requested a reroll).
    /// If no candidates can be generated the system drains PendingLevelUps and,
    /// when fully drained, returns Status to Playing.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LevelUpSelectionGateSystem))]
    public partial struct UpgradeOptionGeneratorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameLoopState>();
            state.RequireForUpdate<PlayerProgressionState>();
            state.RequireForUpdate<UpgradeSelectionRequest>();
            state.RequireForUpdate<OwnedWeaponElement>();
            state.RequireForUpdate<OwnedPassiveElement>();
            state.RequireForUpdate<UpgradeOptionElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var registry = DataRegistry.Instance;
            if (registry?.Catalog == null)
            {
                return;
            }

            RefRW<GameLoopState> loop = SystemAPI.GetSingletonRW<GameLoopState>();

            if (loop.ValueRO.Status != RunStatus.LevelUpSelection)
            {
                return;
            }

            RefRW<PlayerProgressionState> progression = SystemAPI.GetSingletonRW<PlayerProgressionState>();
            RefRW<UpgradeSelectionRequest> request = SystemAPI.GetSingletonRW<UpgradeSelectionRequest>();
            DynamicBuffer<UpgradeOptionElement> options = SystemAPI.GetSingletonBuffer<UpgradeOptionElement>();
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            DynamicBuffer<AvailableWeaponElement> availableWeapons = SystemAPI.GetSingletonBuffer<AvailableWeaponElement>();
            DynamicBuffer<AvailablePassiveElement> availablePassives = SystemAPI.GetSingletonBuffer<AvailablePassiveElement>();

            // Reroll path: consume a reroll charge and rebuild options from a fresh seed.
            if (request.ValueRO.RerollRequested == 1)
            {
                request.ValueRW.RerollRequested = 0;
                request.ValueRW.HasSelection = 0;
                request.ValueRW.SelectedOptionIndex = -1;

                if (progression.ValueRO.RerollsRemaining > 0)
                {
                    progression.ValueRW.RerollsRemaining -= 1;
                    UpgradeOptionUtility.BuildOptions(
                        registry,
                        ref options,
                        ownedWeapons,
                        ownedPassives,
                        availableWeapons,
                        availablePassives,
                        progression.ValueRO,
                        loop.ValueRO,
                        preferFreshOptions: true);
                }
            }

            // Initial generation path: buffer is empty, fill it normally.
            if (options.Length == 0)
            {
                UpgradeOptionUtility.BuildOptions(
                    registry,
                    ref options,
                    ownedWeapons,
                    ownedPassives,
                    availableWeapons,
                    availablePassives,
                    progression.ValueRO,
                    loop.ValueRO,
                    preferFreshOptions: false);
            }

            // No candidates at all — drain PendingLevelUps and restore state without a UI card.
            if (options.Length == 0)
            {
                loop.ValueRW.PendingLevelUps = math.max(0, loop.ValueRO.PendingLevelUps - 1);
                if (loop.ValueRO.PendingLevelUps > 1)
                {
                    UpgradeOptionUtility.BuildOptions(
                        registry,
                        ref options,
                        ownedWeapons,
                        ownedPassives,
                        availableWeapons,
                        availablePassives,
                        progression.ValueRO,
                        loop.ValueRO,
                        preferFreshOptions: false);
                }
                else
                {
                    loop.ValueRW.Status = RunStatus.Playing;
                }
            }
        }
    }
}
