using NightDash.Data;
using NightDash.ECS.Authoring;
using NightDash.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDash.Editor
{
    public static class NightDashBootstrapSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CatalogPath = "Assets/NightDash/Data/data_catalog.asset";

        [MenuItem("NightDash/Scene/Setup SampleScene Bootstrap")]
        public static void SetupSampleSceneBootstrap()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError($"[NightDash] Missing catalog at '{CatalogPath}'. Run NightDash/Data/Rebuild Data Catalog first.");
                return;
            }

            var bootstrap = Object.FindFirstObjectByType<NightDashBootstrapAuthoring>();
            if (bootstrap == null)
            {
                var bootstrapGo = new GameObject("NightDashBootstrap");
                bootstrap = bootstrapGo.AddComponent<NightDashBootstrapAuthoring>();
            }
            bootstrap.TryAutoAssignSpawnPrefabs();

            var registry = Object.FindFirstObjectByType<DataRegistry>();
            if (registry == null)
            {
                var registryGo = new GameObject("DataRegistry");
                registry = registryGo.AddComponent<DataRegistry>();
            }

            var serialized = new SerializedObject(registry);
            serialized.FindProperty("dataCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[NightDash] SampleScene bootstrap setup completed (NightDashBootstrap + DataRegistry + catalog).");
        }
    }
}
