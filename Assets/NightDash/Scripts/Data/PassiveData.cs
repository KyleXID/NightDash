using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Passive Data")]
    public class PassiveData : ScriptableObject
    {
        public string passiveId;
        public string displayName;
        public string description;
        public float flatValue;
        public float multiplierValue = 1f;
        public int maxLevel = 5;
    }
}
