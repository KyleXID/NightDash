using Unity.Entities;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SaveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaProgress>();
            state.RequireForUpdate<SaveState>();
            state.RequireForUpdate<RunResultStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
            RefRW<SaveState> saveState = SystemAPI.GetSingletonRW<SaveState>();
            RunResultStats result = SystemAPI.GetSingleton<RunResultStats>();

            if (saveState.ValueRO.LastSavedConquestPoints < 0)
            {
                SaveDataHelper.TryLoad(out int loadedPoints);
                meta.ValueRW.ConquestPoints = loadedPoints;
                saveState.ValueRW.LastSavedConquestPoints = loadedPoints;
                return;
            }

            if (result.RewardCommitted == 0)
            {
                return;
            }

            if (saveState.ValueRO.LastSavedConquestPoints == meta.ValueRO.ConquestPoints)
            {
                return;
            }

            SaveDataHelper.Save(meta.ValueRO.ConquestPoints);
            saveState.ValueRW.LastSavedConquestPoints = meta.ValueRO.ConquestPoints;
        }
    }
}
