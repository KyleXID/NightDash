using System.Collections.Generic;
using NightDash.Data;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public static class DataCatalogBuilder
    {
        private const string CatalogPath = "Assets/NightDash/Data/data_catalog.asset";

        [MenuItem("NightDash/Data/Rebuild Data Catalog")]
        public static void RebuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DataCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.classes = LoadAll<ClassData>();
            catalog.weapons = LoadAll<WeaponData>();
            catalog.passives = LoadAll<PassiveData>();
            catalog.evolutions = LoadAll<EvolutionData>();
            catalog.stages = LoadAll<StageData>();
            catalog.difficultyModifiers = LoadAll<DifficultyModifierData>();
            catalog.metaTrees = LoadAll<MetaTreeData>();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[NightDash] DataCatalog rebuilt: classes={catalog.classes.Count}, weapons={catalog.weapons.Count}, passives={catalog.passives.Count}, " +
                $"evolutions={catalog.evolutions.Count}, stages={catalog.stages.Count}, difficulty={catalog.difficultyModifiers.Count}, meta={catalog.metaTrees.Count}");
        }

        private static List<T> LoadAll<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var list = new List<T>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    list.Add(asset);
                }
            }

            return list;
        }
    }
}
