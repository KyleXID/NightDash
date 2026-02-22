using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    [CreateAssetMenu(menuName = "NightDash/Data/Data Catalog", fileName = "data_catalog")]
    public sealed class DataCatalog : ScriptableObject
    {
        [Header("Core Data")]
        public List<ClassData> classes = new();
        public List<WeaponData> weapons = new();
        public List<PassiveData> passives = new();
        public List<EvolutionData> evolutions = new();

        [Header("Stage/Meta")]
        public List<StageData> stages = new();
        public List<DifficultyModifierData> difficultyModifiers = new();
        public List<MetaTreeData> metaTrees = new();
    }
}
