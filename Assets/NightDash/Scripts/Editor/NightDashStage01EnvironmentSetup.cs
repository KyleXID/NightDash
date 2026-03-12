using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightDash.Editor
{
    public static class NightDashStage01EnvironmentSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EnvironmentRootName = "Stage01Environment";
        private const string LegacyRootName = "NightDashStageEnvironmentArt";

        private static readonly string[] GroundPaths =
        {
            "Assets/NightDash/Art/Stage01/Tilesets/spr_stage_burning_wastes_ground_tileset.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_stage_burning_wastes_crack_tileset.png"
        };

        private static readonly string[] PropPaths =
        {
            "Assets/NightDash/Art/Stage01/Props/spr_stage_burning_wastes_lava_patch.png",
            "Assets/NightDash/Art/Stage01/Props/spr_stage_burning_wastes_rock_set.png",
            "Assets/NightDash/Art/Stage01/Props/spr_stage_burning_wastes_dead_tree_set.png",
            "Assets/NightDash/Art/Stage01/Props/spr_stage_burning_wastes_ruin_prop_set.png"
        };

        private const string BorderPath = "Assets/NightDash/Art/Stage01/Tilesets/spr_stage_burning_wastes_border_fade.png";
        private const string ParallaxPath = "Assets/NightDash/Art/Stage01/Tilesets/spr_stage_burning_wastes_parallax_bg.png";
        private static readonly AtlasGrid GroundAtlas = new(columns: 6, rows: 7, marginX: 25, marginY: 29, cellWidth: 154, cellHeight: 133, spacingX: 10, spacingY: 10, inset: 6);
        private static readonly AtlasGrid CrackAtlas = new(columns: 5, rows: 6, marginX: 26, marginY: 28, cellWidth: 186, cellHeight: 153, spacingX: 10, spacingY: 10, inset: 6);
        private static readonly Vector2 StageFillSize = new(88f, 54f);
        private static readonly Color BaseGroundColor = new(0.18f, 0.16f, 0.15f, 1f);
        private const float GroundTileRenderScale = 1.14f;
        private const float GroundTileStepScale = 0.90f;
        private const float CrackTileRenderScale = 1.10f;
        private const float CrackTileStepScale = 0.92f;

        [MenuItem("NightDash/Art/Apply Stage01 Environment")]
        public static void ApplyStage01Environment()
        {
            SliceAll();
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NightDash] Stage01 environment sliced and applied to SampleScene.");
        }

        public static void ApplyStage01EnvironmentBatch()
        {
            ApplyStage01Environment();
        }

        private static void SliceAll()
        {
            SliceGridTexture(GroundPaths[0], GroundAtlas);
            SliceGridTexture(GroundPaths[1], CrackAtlas);
            SliceLargestIslandTexture(PropPaths[0]);
            SliceLargestIslandsTexture(PropPaths[1], 14, 5000);
            SliceLargestIslandsTexture(PropPaths[2], 15, 10000);
            SliceLargestIslandsTexture(PropPaths[3], 11, 10000);
            SliceSingleTexture(BorderPath);
            SliceSingleTexture(ParallaxPath);
        }

        private static void SliceSingleTexture(string assetPath)
        {
            var importer = GetConfiguredImporter(assetPath);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritesheet = Array.Empty<SpriteMetaData>();
            importer.SaveAndReimport();
        }

        private static void SliceGridTexture(string assetPath, AtlasGrid atlas)
        {
            var importer = GetConfiguredImporter(assetPath);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = false;
            importer.SaveAndReimport();

            var sprites = new List<SpriteMetaData>();
            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int index = 0;
            for (int row = atlas.rows - 1; row >= 0; row--)
            {
                for (int col = 0; col < atlas.columns; col++)
                {
                    float x = atlas.marginX + col * (atlas.cellWidth + atlas.spacingX) + atlas.inset;
                    float y = atlas.marginY + row * (atlas.cellHeight + atlas.spacingY) + atlas.inset;
                    float width = atlas.cellWidth - (atlas.inset * 2);
                    float height = atlas.cellHeight - (atlas.inset * 2);
                    var rect = new Rect(x, y, width, height);
                    sprites.Add(CreateMeta($"{baseName}_{index:00}", rect));
                    index++;
                }
            }

            importer = GetConfiguredImporter(assetPath);
            importer.isReadable = false;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = sprites.ToArray();
            importer.SaveAndReimport();
        }

        private static void SliceLargestIslandTexture(string assetPath)
        {
            var importer = GetConfiguredImporter(assetPath);
            importer.isReadable = true;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                return;
            }

            List<Rect> rects = FindOpaqueIslandRects(texture, 1);
            if (rects.Count == 0)
            {
                rects.Add(new Rect(0f, 0f, texture.width, texture.height));
            }
            else
            {
                rects = new List<Rect> { rects.OrderByDescending(r => r.width * r.height).First() };
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var metas = rects
                .OrderByDescending(r => r.yMin)
                .ThenBy(r => r.xMin)
                .Select((rect, i) => CreateMeta($"{baseName}_{i:00}", rect))
                .ToArray();

            importer = GetConfiguredImporter(assetPath);
            importer.isReadable = false;
            importer.spriteImportMode = metas.Length == 1 ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritesheet = metas.Length == 1 ? Array.Empty<SpriteMetaData>() : metas;
            importer.SaveAndReimport();
        }

        private static void SliceLargestIslandsTexture(string assetPath, int maxSprites, int minIslandArea)
        {
            var importer = GetConfiguredImporter(assetPath);
            importer.isReadable = true;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                return;
            }

            List<Rect> rects = FindOpaqueIslandRects(texture, minIslandArea)
                .OrderByDescending(r => r.width * r.height)
                .Take(maxSprites)
                .OrderByDescending(r => r.yMin)
                .ThenBy(r => r.xMin)
                .ToList();

            if (rects.Count == 0)
            {
                rects.Add(new Rect(0f, 0f, texture.width, texture.height));
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var metas = rects
                .Select((rect, i) => CreateMeta($"{baseName}_{i:00}", rect))
                .ToArray();

            importer = GetConfiguredImporter(assetPath);
            importer.isReadable = false;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = metas;
            importer.SaveAndReimport();
        }

        private static TextureImporter GetConfiguredImporter(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 128f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            return importer;
        }

        private static List<(int start, int end)> FindOpaqueSpans(Texture2D texture, bool horizontal)
        {
            int primary = horizontal ? texture.width : texture.height;
            int secondary = horizontal ? texture.height : texture.width;
            var spans = new List<(int start, int end)>();
            bool active = false;
            int spanStart = 0;

            for (int p = 0; p < primary; p++)
            {
                bool hasOpaque = false;
                for (int s = 0; s < secondary; s++)
                {
                    Color pixel = horizontal ? texture.GetPixel(p, s) : texture.GetPixel(s, p);
                    if (pixel.a > 0.05f)
                    {
                        hasOpaque = true;
                        break;
                    }
                }

                if (hasOpaque && !active)
                {
                    active = true;
                    spanStart = p;
                }
                else if (!hasOpaque && active)
                {
                    spans.Add((spanStart, p - 1));
                    active = false;
                }
            }

            if (active)
            {
                spans.Add((spanStart, primary - 1));
            }

            return spans;
        }

        private static List<Rect> FindOpaqueIslandRects(Texture2D texture, int minIslandArea)
        {
            int width = texture.width;
            int height = texture.height;
            var pixels = texture.GetPixels32();
            var visited = new bool[width * height];
            var rects = new List<Rect>();
            var queue = new Queue<int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int start = y * width + x;
                    if (visited[start] || pixels[start].a < 16)
                    {
                        continue;
                    }

                    visited[start] = true;
                    queue.Enqueue(start);

                    int minX = x;
                    int maxX = x;
                    int minY = y;
                    int maxY = y;
                    int count = 0;

                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        int cx = current % width;
                        int cy = current / width;
                        count++;

                        minX = Mathf.Min(minX, cx);
                        maxX = Mathf.Max(maxX, cx);
                        minY = Mathf.Min(minY, cy);
                        maxY = Mathf.Max(maxY, cy);

                        EnqueueNeighbor(cx + 1, cy);
                        EnqueueNeighbor(cx - 1, cy);
                        EnqueueNeighbor(cx, cy + 1);
                        EnqueueNeighbor(cx, cy - 1);
                    }

                    if (count >= minIslandArea)
                    {
                        rects.Add(Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1));
                    }
                }
            }

            return rects;

            void EnqueueNeighbor(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    return;
                }

                int index = ny * width + nx;
                if (visited[index] || pixels[index].a < 16)
                {
                    return;
                }

                visited[index] = true;
                queue.Enqueue(index);
            }
        }

        private static SpriteMetaData CreateMeta(string name, Rect rect)
        {
            return new SpriteMetaData
            {
                name = name,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                rect = rect,
                border = Vector4.zero
            };
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            DestroyImmediateIfExists(EnvironmentRootName);
            DestroyImmediateIfExists(LegacyRootName);

            var root = new GameObject(EnvironmentRootName);
            var backgroundRoot = new GameObject("Background");
            backgroundRoot.transform.SetParent(root.transform, false);
            var groundRoot = new GameObject("Ground");
            groundRoot.transform.SetParent(root.transform, false);
            var crackRoot = new GameObject("Cracks");
            crackRoot.transform.SetParent(root.transform, false);
            var propsRoot = new GameObject("Props");
            propsRoot.transform.SetParent(root.transform, false);
            var overlayRoot = new GameObject("Overlay");
            overlayRoot.transform.SetParent(root.transform, false);

            CreateSprite(backgroundRoot.transform, "Parallax", LoadPrimarySprite(ParallaxPath), new Vector3(0f, 0f, 8f), new Vector2(92f, 56f), -100, 0.92f);
            CreateSolidColorSprite(backgroundRoot.transform, "BaseGround", BaseGroundColor, new Vector3(0f, 0f, 2f), StageFillSize, -10);

            var groundSprites = LoadSprites(GroundPaths[0]);
            var crackSprites = LoadSprites(GroundPaths[1]);
            CreateTileGrid(groundRoot.transform, groundSprites, GroundAtlas, StageFillSize, GroundTileRenderScale, GroundTileStepScale, 0.82f, 20260309);
            CreateOverlayGrid(crackRoot.transform, crackSprites, CrackAtlas, StageFillSize, CrackTileRenderScale, CrackTileStepScale, 0.55f, 20260310, 0.22f);

            CreateSprite(overlayRoot.transform, "Border", LoadPrimarySprite(BorderPath), new Vector3(0f, 0f, -2f), StageFillSize, 40, 0.92f);

            LayoutPropSet(propsRoot.transform, LoadSprites(PropPaths[0]), new[]
            {
                new PropPlacement(new Vector3(-16f, -9f, 0.8f), new Vector2(10f, 10f)),
                new PropPlacement(new Vector3(18f, 8f, 0.8f), new Vector2(8f, 8f))
            }, 10, 1f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void DestroyImmediateIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static List<Sprite> LoadSprites(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(x => x.name, StringComparer.Ordinal)
                .ToList();
        }

        private static Sprite LoadPrimarySprite(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault()
                ?? AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void CreateTileGrid(Transform parent, List<Sprite> sprites, AtlasGrid atlas, Vector2 fillSize, float renderScale, float stepScale, float alpha, int seed)
        {
            if (sprites.Count == 0)
            {
                return;
            }

            var random = new System.Random(seed);
            float renderedWidth = (atlas.cellWidth / 128f) * renderScale;
            float renderedHeight = (atlas.cellHeight / 128f) * renderScale;
            float stepX = renderedWidth * stepScale;
            float stepY = renderedHeight * stepScale;
            int columns = Mathf.CeilToInt(fillSize.x / stepX) + 2;
            int rows = Mathf.CeilToInt(fillSize.y / stepY) + 2;
            float startX = -((columns - 1) * stepX) * 0.5f;
            float startY = ((rows - 1) * stepY) * 0.5f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Sprite sprite = sprites[random.Next(sprites.Count)];
                    var position = new Vector3(startX + x * stepX, startY - y * stepY, 1f);
                    CreateSprite(parent, $"Ground_{x}_{y}", sprite, position, new Vector2(renderedWidth, renderedHeight), 1, alpha);
                }
            }
        }

        private static void CreateOverlayGrid(Transform parent, List<Sprite> sprites, AtlasGrid atlas, Vector2 fillSize, float renderScale, float stepScale, float alpha, int seed, float placementChance)
        {
            if (sprites.Count == 0)
            {
                return;
            }

            var random = new System.Random(seed);
            float renderedWidth = (atlas.cellWidth / 128f) * renderScale;
            float renderedHeight = (atlas.cellHeight / 128f) * renderScale;
            float stepX = renderedWidth * stepScale;
            float stepY = renderedHeight * stepScale;
            int columns = Mathf.CeilToInt(fillSize.x / stepX) + 2;
            int rows = Mathf.CeilToInt(fillSize.y / stepY) + 2;
            float startX = -((columns - 1) * stepX) * 0.5f;
            float startY = ((rows - 1) * stepY) * 0.5f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (random.NextDouble() > placementChance)
                    {
                        continue;
                    }

                    Sprite sprite = sprites[random.Next(sprites.Count)];
                    var position = new Vector3(startX + x * stepX, startY - y * stepY, 0.95f);
                    CreateSprite(parent, $"Crack_{x}_{y}", sprite, position, new Vector2(renderedWidth, renderedHeight), 5, alpha);
                }
            }
        }

        private static void LayoutPropSet(Transform parent, List<Sprite> sprites, IReadOnlyList<PropPlacement> placements, int sortingOrder, float alpha)
        {
            if (sprites.Count == 0)
            {
                return;
            }

            sprites = sprites
                .OrderByDescending(x => x.rect.width * x.rect.height)
                .ToList();

            for (int i = 0; i < placements.Count; i++)
            {
                Sprite sprite = sprites[i % sprites.Count];
                CreateSprite(parent, $"{sprite.name}_{i:00}", sprite, placements[i].position, placements[i].size, sortingOrder + i, alpha);
            }
        }

        private static void CreateSprite(Transform parent, string name, Sprite sprite, Vector3 position, Vector2 targetSize, int sortingOrder, float alpha)
        {
            if (sprite == null)
            {
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(1f, 1f, 1f, alpha);

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                go.transform.localScale = new Vector3(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y, 1f);
            }
        }

        private static void CreateSolidColorSprite(Transform parent, string name, Color color, Vector3 position, Vector2 targetSize, int sortingOrder)
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(targetSize.x, targetSize.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
        }

        private readonly struct PropPlacement
        {
            public readonly Vector3 position;
            public readonly Vector2 size;

            public PropPlacement(Vector3 position, Vector2 size)
            {
                this.position = position;
                this.size = size;
            }
        }

        private readonly struct AtlasGrid
        {
            public readonly int columns;
            public readonly int rows;
            public readonly int marginX;
            public readonly int marginY;
            public readonly int cellWidth;
            public readonly int cellHeight;
            public readonly int spacingX;
            public readonly int spacingY;
            public readonly int inset;

            public AtlasGrid(int columns, int rows, int marginX, int marginY, int cellWidth, int cellHeight, int spacingX, int spacingY, int inset)
            {
                this.columns = columns;
                this.rows = rows;
                this.marginX = marginX;
                this.marginY = marginY;
                this.cellWidth = cellWidth;
                this.cellHeight = cellHeight;
                this.spacingX = spacingX;
                this.spacingY = spacingY;
                this.inset = inset;
            }
        }
    }
}
