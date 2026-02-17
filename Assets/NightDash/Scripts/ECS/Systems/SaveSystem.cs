using System;
using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.ECS.Systems
{
    [Serializable]
    public struct MetaSaveData
    {
        public int conquestPoint;
        public int attackNodeLevel;
        public int survivalNodeLevel;
        public int abyssNodeLevel;
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SaveSystem : ISystem
    {
        private const string SaveKey = "nightdash_meta_progress_v1";

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaProgress>();
            state.RequireForUpdate<GameLoopState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var loop = SystemAPI.GetSingleton<GameLoopState>();
            if (loop.RunEnded == 0)
            {
                return;
            }

            var meta = SystemAPI.GetSingleton<MetaProgress>();
            var saveData = new MetaSaveData
            {
                conquestPoint = meta.ConquestPoint,
                attackNodeLevel = meta.AttackNodeLevel,
                survivalNodeLevel = meta.SurvivalNodeLevel,
                abyssNodeLevel = meta.AbyssNodeLevel
            };

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }
    }
}
