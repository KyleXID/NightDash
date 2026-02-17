using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Evolution Data")]
    public class EvolutionData : ScriptableObject
    {
        public string evolutionId;
        public WeaponData sourceWeapon;
        public PassiveData requiredPassive;
        public WeaponData evolvedWeapon;
        public bool isAbyssEvolution;
        public int requiredRiskScore;
    }
}
