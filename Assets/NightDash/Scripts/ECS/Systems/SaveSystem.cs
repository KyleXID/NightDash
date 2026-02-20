using Unity.Entities;
using UnityEngine;
using NightDash.ECS.Components;

namespace NightDash.ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SaveSystem : ISystem
    {
        private const string ConquestPointKey = "NightDash_ConquestPoints";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaProgress>();
            state.RequireForUpdate<SaveState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<MetaProgress> meta = SystemAPI.GetSingletonRW<MetaProgress>();
            RefRW<SaveState> saveState = SystemAPI.GetSingletonRW<SaveState>();

            if (saveState.ValueRO.LastSavedConquestPoints == meta.ValueRO.ConquestPoints)
            {
                return;
            }

            PlayerPrefs.SetInt(ConquestPointKey, meta.ValueRO.ConquestPoints);
            PlayerPrefs.Save();
            saveState.ValueRW.LastSavedConquestPoints = meta.ValueRO.ConquestPoints;
        }
    }
}
