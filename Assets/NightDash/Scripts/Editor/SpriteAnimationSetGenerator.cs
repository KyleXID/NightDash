// Editor utility: scans Animations/* folders and creates/refreshes
// SpriteAnimationSetSO assets. Re-runnable — preserves user-edited fps,
// renderScale, sourceFacesLeft on existing SOs and only refreshes frames.
//
// Menu: NightDash > Generate Animation SOs
//
// Layout convention:
//   Assets/NightDash/Art/Stage01/Characters/Animations/{Class}/{Clip}/frame_*.png
//     → id = "class_{class.ToLowerInvariant()}", e.g. class_warrior
//   Assets/NightDash/Art/Stage01/Enemies/Animations/{archetype}/{Clip}/frame_*.png
//     → id = "{archetype}", e.g. ghoul_scout, boss_agron, elt_wastes_executor
//
// Output:
//   Assets/NightDash/Data/Animations/anim_{id}.asset
//   Auto-appended to DataCatalog.animationSets

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NightDash.Data;

namespace NightDash.Editor
{
    public static class SpriteAnimationSetGenerator
    {
        private const string CharactersRoot = "Assets/NightDash/Art/Stage01/Characters/Animations";
        private const string EnemiesRoot    = "Assets/NightDash/Art/Stage01/Enemies/Animations";
        private const string OutputRoot     = "Assets/NightDash/Data/Animations";
        private const string CatalogPath    = "Assets/NightDash/Data/data_catalog.asset";

        // Per-archetype default tuning. Used only when creating a brand-new SO.
        // Existing SOs keep whatever the designer changed.
        private struct Defaults
        {
            public float WalkFps;
            public float IdleFps;
            public float RenderScale;
            public bool SourceFacesLeft;
        }

        // RenderScale targets: camera ortho=3.8 → screen height ≈ 7.6 units.
        // Players ~28% screen, small enemies ~18%, mid enemies ~25%, boss ~90%.
        // PNG sizes (PPU 32): chars 136~148px, ghoul/ash 84, ember 92,
        // brute 124, executor 140, boss 256.
        private static readonly Dictionary<string, Defaults> ArchetypeDefaults = new()
        {
            // Characters — 0.5 → ~2.1 units (28% screen)
            { "class_warrior",    new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_mage",       new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_astrologer", new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_paladin",    new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_priest",     new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_archer",     new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },
            { "class_gunslinger", new Defaults { WalkFps = 12f, IdleFps = 6f, RenderScale = 0.50f, SourceFacesLeft = false } },

            // Small enemies — 0.55 → ~1.5 units (~20% screen)
            { "ghoul_scout",          new Defaults { WalkFps = 10f, IdleFps = 6f, RenderScale = 0.55f, SourceFacesLeft = false } },
            { "ember_bat",            new Defaults { WalkFps = 14f, IdleFps = 14f, RenderScale = 0.55f, SourceFacesLeft = false } },
            { "ash_caster",           new Defaults { WalkFps = 8f,  IdleFps = 8f,  RenderScale = 0.55f, SourceFacesLeft = false } },

            // Mid enemies — 0.60 → ~2.4 units (~32% screen)
            { "wasteland_brute",      new Defaults { WalkFps = 10f, IdleFps = 6f,  RenderScale = 0.60f, SourceFacesLeft = false } },
            { "elt_wastes_executor",  new Defaults { WalkFps = 10f, IdleFps = 6f,  RenderScale = 0.60f, SourceFacesLeft = false } },

            // Boss — 1.0 → 8 units (~105% screen, slightly overflowing for boss feel)
            { "boss_agron",           new Defaults { WalkFps = 10f, IdleFps = 6f,  RenderScale = 1.00f, SourceFacesLeft = false } },
        };

        private static Defaults DefaultsFor(string id)
        {
            return ArchetypeDefaults.TryGetValue(id, out var d)
                ? d
                : new Defaults { WalkFps = 10f, IdleFps = 6f, RenderScale = 1.5f, SourceFacesLeft = false };
        }

        // One-shot maintenance menu: rewrites renderScale on every existing SO
        // back to ArchetypeDefaults. Use after tuning the table when you don't
        // want to inspect 14 assets one by one. fps/loop/frames untouched.
        [MenuItem("NightDash/Reset Animation Render Scales", priority = 211)]
        public static void ResetRenderScales()
        {
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:SpriteAnimationSetSO", new[] { OutputRoot });
                int updated = 0;

                foreach (string g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var so = AssetDatabase.LoadAssetAtPath<SpriteAnimationSetSO>(path);
                    if (so == null || string.IsNullOrEmpty(so.id)) continue;

                    var d = DefaultsFor(so.id);
                    so.renderScale = d.RenderScale;
                    so.sourceFacesLeft = d.SourceFacesLeft;
                    EditorUtility.SetDirty(so);
                    updated++;
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "Reset Render Scales Complete",
                    $"Updated renderScale + sourceFacesLeft on {updated} SOs from ArchetypeDefaults.",
                    "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpriteAnimationSetGenerator] {e}");
                EditorUtility.DisplayDialog("Reset Failed", e.Message, "OK");
            }
        }

        [MenuItem("NightDash/Generate Animation SOs", priority = 210)]
        public static void Generate()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                EnsureFolder(OutputRoot);

                var generated = new List<SpriteAnimationSetSO>();

                // Characters: Animations/{Class}/{Clip}/
                ScanRoot(CharactersRoot, charDir =>
                {
                    string className = Path.GetFileName(charDir);
                    string id = "class_" + className.ToLowerInvariant();
                    var so = CreateOrUpdate(id, charDir);
                    if (so != null) generated.Add(so);
                });

                // Enemies + boss: Animations/{archetype}/{Clip}/
                ScanRoot(EnemiesRoot, enemyDir =>
                {
                    string id = Path.GetFileName(enemyDir);
                    var so = CreateOrUpdate(id, enemyDir);
                    if (so != null) generated.Add(so);
                });

                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                int catalogAdds = AppendToCatalog(generated);

                EditorUtility.DisplayDialog(
                    "Generate Animation SOs Complete",
                    $"Generated/refreshed: {generated.Count} SOs\nAdded to DataCatalog: {catalogAdds}\n\nLocation: {OutputRoot}",
                    "OK");
            }
            catch (System.Exception e)
            {
                AssetDatabase.StopAssetEditing();
                Debug.LogError($"[SpriteAnimationSetGenerator] {e}");
                EditorUtility.DisplayDialog("Generate Failed", e.Message, "OK");
            }
        }

        private static void ScanRoot(string root, System.Action<string> perEntry)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[SpriteAnimationSetGenerator] Missing folder: {root}");
                return;
            }

            string[] subFolders = AssetDatabase.GetSubFolders(root);
            foreach (string sub in subFolders) perEntry(sub);
        }

        private static SpriteAnimationSetSO CreateOrUpdate(string id, string entryFolder)
        {
            string assetPath = $"{OutputRoot}/anim_{id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<SpriteAnimationSetSO>(assetPath);
            bool isNew = so == null;

            if (isNew)
            {
                so = ScriptableObject.CreateInstance<SpriteAnimationSetSO>();
                so.id = id;
                var d = DefaultsFor(id);
                so.renderScale = d.RenderScale;
                so.sourceFacesLeft = d.SourceFacesLeft;
                AssetDatabase.CreateAsset(so, assetPath);
            }
            else
            {
                if (string.IsNullOrEmpty(so.id)) so.id = id;
            }

            // Always re-apply facing default — keeps flipX direction consistent
            // across regenerations. fps/loop/renderScale stay user-tunable.
            so.sourceFacesLeft = DefaultsFor(id).SourceFacesLeft;

            // Refresh clips: scan {entryFolder}/{Clip}/ subfolders.
            string[] clipFolders = AssetDatabase.GetSubFolders(entryFolder);
            int clipsRefreshed = 0;

            foreach (string clipFolder in clipFolders)
            {
                string clipName = Path.GetFileName(clipFolder); // e.g. "Walk", "Idle"
                Sprite[] frames = LoadFrameSprites(clipFolder);
                if (frames == null || frames.Length == 0) continue;

                AnimationClipDef existing = FindClip(so, clipName);
                if (existing == null)
                {
                    var d = DefaultsFor(id);
                    float fps = clipName.Equals("Idle", System.StringComparison.OrdinalIgnoreCase)
                        ? d.IdleFps : d.WalkFps;
                    so.clips.Add(new AnimationClipDef
                    {
                        name = clipName,
                        frames = frames,
                        fps = fps,
                        loop = true,
                    });
                }
                else
                {
                    // Preserve user-edited fps / loop; refresh frames only.
                    existing.frames = frames;
                }
                clipsRefreshed++;
            }

            EditorUtility.SetDirty(so);
            Debug.Log($"[SpriteAnimationSetGenerator] {(isNew ? "Created" : "Refreshed")} {assetPath} — {clipsRefreshed} clips");
            return so;
        }

        private static AnimationClipDef FindClip(SpriteAnimationSetSO so, string name)
        {
            for (int i = 0; i < so.clips.Count; i++)
            {
                var c = so.clips[i];
                if (c != null && string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        private static Sprite[] LoadFrameSprites(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            if (guids == null || guids.Length == 0) return System.Array.Empty<Sprite>();

            var paths = new List<string>(guids.Length);
            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            // Natural sort: frame_2 < frame_10. Falls back to Ordinal if no digits.
            paths.Sort(NaturalCompare);

            var list = new List<Sprite>(paths.Count);
            foreach (string p in paths)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sp != null) list.Add(sp);
            }
            return list.ToArray();
        }

        private static int NaturalCompare(string a, string b)
        {
            string fa = Path.GetFileNameWithoutExtension(a);
            string fb = Path.GetFileNameWithoutExtension(b);
            var ma = System.Text.RegularExpressions.Regex.Match(fa, @"\d+");
            var mb = System.Text.RegularExpressions.Regex.Match(fb, @"\d+");
            if (ma.Success && mb.Success && int.TryParse(ma.Value, out int na) && int.TryParse(mb.Value, out int nb))
            {
                int cmp = na.CompareTo(nb);
                if (cmp != 0) return cmp;
            }
            return string.Compare(a, b, System.StringComparison.Ordinal);
        }

        private static int AppendToCatalog(List<SpriteAnimationSetSO> generated)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[SpriteAnimationSetGenerator] DataCatalog not found at {CatalogPath} — skipping auto-register.");
                return 0;
            }

            var existing = new HashSet<SpriteAnimationSetSO>(catalog.animationSets);
            int added = 0;
            foreach (var so in generated)
            {
                if (so == null) continue;
                if (existing.Add(so))
                {
                    catalog.animationSets.Add(so);
                    added++;
                }
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            return added;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
