using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightDash.Runtime;

namespace NightDash.Editor
{
    // Tools menu: NightDash > Auto Setup Characters & Enemies
    // Generates prefabs from PixelLab sprites + arranges them in an "AutoSetupPreview" scene
    // to verify walk/idle animations playing in the editor.
    public static class CharacterAutoSetupTool
    {
        const string CharactersRoot = "Assets/NightDash/Art/Stage01/Characters";
        const string EnemiesRoot = "Assets/NightDash/Art/Stage01/Enemies";
        const string PrefabsRoot = "Assets/NightDash/Prefabs/AutoGen";

        // Scale applied to enemies+boss except ember_bat — brings ghoul_scout (84px)
        // up to warrior (140px) reference size: 140 / 84 ≈ 1.67. Same multiplier for
        // other monsters per visual-consistency requirement.
        const float EnemyScaleMultiplier = 1.67f;

        // Map: prefab id -> base sprite path + animation folders
        struct EntrySpec
        {
            public string id;
            public string basePngPath;
            public string walkFolder;
            public string idleFolder;
            public string attackFolder;
            public float walkFps;
            public float idleFps;
            public float scale;
        }

        static List<EntrySpec> GetCharacterSpecs()
        {
            var list = new List<EntrySpec>();
            string[] classes = { "Warrior", "Mage", "Astrologer", "Paladin", "Priest", "Archer", "Gunslinger" };
            foreach (string cls in classes)
            {
                string lower = cls.ToLower();
                list.Add(new EntrySpec
                {
                    id = "spr_char_" + lower,
                    basePngPath = $"{CharactersRoot}/spr_char_{lower}.png",
                    walkFolder = $"{CharactersRoot}/Animations/{cls}/Walk",
                    idleFolder = $"{CharactersRoot}/Animations/{cls}/Idle",
                    attackFolder = null,
                    walkFps = 12f,
                    idleFps = 6f,
                    scale = 1f,
                });
            }
            return list;
        }

        static List<EntrySpec> GetEnemySpecs()
        {
            var list = new List<EntrySpec>();
            string[] enemies = { "ghoul_scout", "ember_bat", "ash_caster", "wasteland_brute", "elt_wastes_executor" };
            foreach (string id in enemies)
            {
                string prefix = id.StartsWith("elt_") ? "spr_" : "spr_enemy_";
                // ember_bat: wing-flap hover (fast); ash_caster: slow drift hover; others: walk
                float fps = id == "ember_bat" ? 14f
                          : id == "ash_caster" ? 8f
                          : 10f;
                // ember_bat keeps native scale; others scale up to match player size
                float scale = id == "ember_bat" ? 1f : EnemyScaleMultiplier;
                list.Add(new EntrySpec
                {
                    id = prefix + id,
                    basePngPath = $"{EnemiesRoot}/{prefix}{id}.png",
                    walkFolder = $"{EnemiesRoot}/Animations/{id}/Walk",
                    idleFolder = null,
                    attackFolder = null,
                    walkFps = fps,
                    idleFps = 0f,
                    scale = scale,
                });
            }
            // Boss
            list.Add(new EntrySpec
            {
                id = "spr_boss_agron",
                basePngPath = $"{EnemiesRoot}/spr_boss_agron.png",
                walkFolder = $"{EnemiesRoot}/Animations/boss_agron/Walk",
                idleFolder = null,
                attackFolder = $"{EnemiesRoot}/Animations/boss_agron/Attack",
                walkFps = 10f,
                idleFps = 0f,
                scale = EnemyScaleMultiplier,
            });
            return list;
        }

        [MenuItem("NightDash/Preview Movable Warrior (WASD)", priority = 201)]
        public static void RunMovablePreview()
        {
            try
            {
                EnsureFolder("Assets/NightDash/Scenes");
                string scenePath = "Assets/NightDash/Scenes/MovablePreview.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Camera
                var cam = new GameObject("Main Camera");
                var camComp = cam.AddComponent<Camera>();
                camComp.orthographic = true;
                camComp.orthographicSize = 4f;
                camComp.backgroundColor = new Color(0.18f, 0.13f, 0.20f);
                camComp.clearFlags = CameraClearFlags.SolidColor;
                cam.transform.position = new Vector3(0, 0, -10);
                cam.tag = "MainCamera";
                var follow = cam.AddComponent<SimpleCameraFollow>();

                // Warrior character
                var warriorBase = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"{CharactersRoot}/spr_char_warrior.png");
                if (warriorBase == null)
                {
                    EditorUtility.DisplayDialog("Missing Sprite",
                        $"Could not find {CharactersRoot}/spr_char_warrior.png", "OK");
                    return;
                }

                var warrior = new GameObject("Warrior");
                var sr = warrior.AddComponent<SpriteRenderer>();
                sr.sprite = warriorBase;

                var walkFrames = LoadFramesFromFolder(
                    $"{CharactersRoot}/Animations/Warrior/Walk");
                var idleFrames = LoadFramesFromFolder(
                    $"{CharactersRoot}/Animations/Warrior/Idle");

                var ctrl = warrior.AddComponent<SimpleMovementController>();
                ctrl.walkFrames = walkFrames;
                ctrl.idleFrames = idleFrames;
                ctrl.walkFps = 12f;
                ctrl.idleFps = 6f;
                ctrl.moveSpeed = 4f;

                follow.target = warrior.transform;

                // Optional: simple ground reference
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                EditorUtility.DisplayDialog("Movable Preview Ready",
                    "Press Play, then use WASD or arrow keys to move.\n" +
                    "Walk plays while moving, idle while stopped.\n" +
                    "Sprite flips left/right based on direction.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Preview Failed", e.Message, "OK");
            }
        }

        [MenuItem("NightDash/Auto Setup Characters & Enemies", priority = 200)]
        public static void RunAutoSetup()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                EnsureFolder(PrefabsRoot);
                EnsureFolder(PrefabsRoot + "/Characters");
                EnsureFolder(PrefabsRoot + "/Enemies");

                var charPrefabs = new List<GameObject>();
                foreach (var spec in GetCharacterSpecs())
                {
                    var prefab = CreatePrefabForEntry(spec, PrefabsRoot + "/Characters");
                    if (prefab != null) charPrefabs.Add(prefab);
                }

                var enemyPrefabs = new List<GameObject>();
                foreach (var spec in GetEnemySpecs())
                {
                    var prefab = CreatePrefabForEntry(spec, PrefabsRoot + "/Enemies");
                    if (prefab != null) enemyPrefabs.Add(prefab);
                }

                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                CreatePreviewScene(charPrefabs, enemyPrefabs);

                EditorUtility.DisplayDialog(
                    "Auto Setup Complete",
                    $"Generated:\n - {charPrefabs.Count} character prefabs\n - {enemyPrefabs.Count} enemy/boss prefabs\n\nPreview scene opened. Press Play to see them animate.",
                    "OK");
            }
            catch (System.Exception e)
            {
                AssetDatabase.StopAssetEditing();
                Debug.LogError($"[CharacterAutoSetupTool] {e}");
                EditorUtility.DisplayDialog("Auto Setup Failed", e.Message, "OK");
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static GameObject CreatePrefabForEntry(EntrySpec spec, string outFolder)
        {
            Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.basePngPath);
            if (baseSprite == null)
            {
                Debug.LogWarning($"[CharacterAutoSetupTool] Base sprite not found: {spec.basePngPath}");
                return null;
            }

            var go = new GameObject(spec.id);
            float s = spec.scale > 0f ? spec.scale : 1f;
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = baseSprite;
            sr.sortingLayerName = "Default";

            // Walk animation (the visible one in preview).
            // Use all frames PixelLab generated.
            var walk = LoadFramesFromFolder(spec.walkFolder);
            if (walk != null && walk.Length > 0)
            {
                var anim = go.AddComponent<SpriteAnimator>();
                anim.frames = walk;
                anim.fps = spec.walkFps;
                anim.loop = true;
                anim.playOnStart = true;
            }

            // Save prefab
            string prefabPath = $"{outFolder}/{spec.id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Sprite[] LoadFramesFromFolder(string folder, bool dropLastFrame = false)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            if (guids == null || guids.Length == 0) return null;

            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);

            var frames = new List<Sprite>(paths.Count);
            foreach (string p in paths)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp != null) frames.Add(sp);
            }
            if (dropLastFrame && frames.Count > 1)
            {
                frames.RemoveAt(frames.Count - 1);
            }
            return frames.ToArray();
        }

        static void CreatePreviewScene(List<GameObject> chars, List<GameObject> enemies)
        {
            const string scenePath = "Assets/NightDash/Scenes/AutoSetupPreview.unity";
            EnsureFolder("Assets/NightDash/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cam = new GameObject("Main Camera");
            var camComp = cam.AddComponent<Camera>();
            camComp.orthographic = true;
            camComp.orthographicSize = 12f;
            camComp.backgroundColor = new Color(0.35f, 0.30f, 0.40f); // 보라회색 — sprite 가시성 우선
            camComp.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.tag = "MainCamera";

            // Layout: characters in row 1, enemies in row 2.
            // Enemies are scaled up so use wider spacing for them.
            float charSpacing = 3f;
            float enemySpacing = 5f;
            float startXChars = -((chars.Count - 1) * charSpacing) * 0.5f;
            for (int i = 0; i < chars.Count; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(chars[i]);
                inst.transform.position = new Vector3(startXChars + i * charSpacing, 3f, 0);
            }

            float startXEnemies = -((enemies.Count - 1) * enemySpacing) * 0.5f;
            for (int i = 0; i < enemies.Count; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(enemies[i]);
                inst.transform.position = new Vector3(startXEnemies + i * enemySpacing, -4f, 0);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }
}
