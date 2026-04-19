using NightDash.Data;
using NightDash.ECS.Components;
using NightDash.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace NightDash.ECS.Systems.Progression
{
    /// <summary>
    /// M3 — Applies the player's chosen upgrade option to the owned weapon/passive buffers,
    /// triggers a runtime stat refresh, clears the option list, and returns the game to Playing.
    /// Runs after UpgradeOptionGeneratorSystem so options are always populated before a selection
    /// can be evaluated in the same frame.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UpgradeOptionGeneratorSystem))]
    public partial struct UpgradeApplySystem : ISystem
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

            RefRW<UpgradeSelectionRequest> request = SystemAPI.GetSingletonRW<UpgradeSelectionRequest>();

            if (request.ValueRO.HasSelection == 0)
            {
                return;
            }

            DynamicBuffer<UpgradeOptionElement> options = SystemAPI.GetSingletonBuffer<UpgradeOptionElement>();

            int optionIndex = request.ValueRO.SelectedOptionIndex;
            request.ValueRW.HasSelection = 0;
            request.ValueRW.SelectedOptionIndex = -1;

            if (optionIndex < 0 || optionIndex >= options.Length)
            {
                return;
            }

            RefRW<PlayerProgressionState> progression = SystemAPI.GetSingletonRW<PlayerProgressionState>();
            DynamicBuffer<OwnedWeaponElement> ownedWeapons = SystemAPI.GetSingletonBuffer<OwnedWeaponElement>();
            DynamicBuffer<OwnedPassiveElement> ownedPassives = SystemAPI.GetSingletonBuffer<OwnedPassiveElement>();
            DynamicBuffer<AvailableWeaponElement> availableWeapons = SystemAPI.GetSingletonBuffer<AvailableWeaponElement>();
            DynamicBuffer<AvailablePassiveElement> availablePassives = SystemAPI.GetSingletonBuffer<AvailablePassiveElement>();

            string classId = SystemAPI.GetSingleton<RunSelection>().ClassId.ToString();

            UpgradeOptionUtility.ApplySelection(
                registry,
                options[optionIndex],
                progression.ValueRO,
                ref ownedWeapons,
                ref ownedPassives);

            // Single call site for stat recalculation — never called from M2 (reroll has no ownership change).
            RuntimeBalanceUtility.RefreshPlayerRuntime(ref state, registry, classId);

            options.Clear();

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
