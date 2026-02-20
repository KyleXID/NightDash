using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Passive Data", fileName = "PassiveData")]
    public sealed class PassiveData : ScriptableObject
    {
        public string passiveId;
        public string displayName;
        [TextArea] public string description;

        [Header("Stat Modifiers")]
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float cooldownMultiplier = 1f;
    }
}
