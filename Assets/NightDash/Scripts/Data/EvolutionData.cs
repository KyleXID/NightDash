using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Evolution Data", fileName = "evolution_")]
    public sealed class EvolutionData : ScriptableObject
    {
        [Header("Identity")]
        [FormerlySerializedAs("evolutionId")] public string id;

        [Header("Output")]
        public string resultWeaponId;

        [Header("Requirements")]
        [FormerlySerializedAs("requiredWeapon")] public string requiredWeaponId;
        [Min(1)] public int requiredWeaponLevel = 8;
        public List<string> requiredPassiveIds = new();
        [FormerlySerializedAs("requiredRiskScore")] public int requiredRiskScoreMin;
        public List<string> requiredModifiers = new();

        [Header("Priority")]
        [FormerlySerializedAs("abyssEvolution")] public bool isAbyss;
        public int priority;
    }
}
