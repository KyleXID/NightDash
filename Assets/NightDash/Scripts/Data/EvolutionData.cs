using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Evolution Data", fileName = "EvolutionData")]
    public sealed class EvolutionData : ScriptableObject
    {
        public string evolutionId;
        public WeaponData requiredWeapon;
        public PassiveData requiredPassive;
        public int requiredRiskScore = 0;
        public bool abyssEvolution;
    }
}
