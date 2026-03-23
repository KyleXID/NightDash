using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightDash.Editor
{
    public static class NightDashStage01EnvironmentSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string EnvironmentRootName = "Stage01Environment";
        private const string LegacyRootName = "NightDashStageEnvironmentArt";

        private static readonly string[] GroundPaths =
        {
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_base.png",
        };

        private static readonly string[] DecoTilePaths =
        {
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_lava_crack_01.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_lava_crack_02.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_lava_crack_03.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_lava_crack_04.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_embers_01.png",
            "Assets/NightDash/Art/Stage01/Tilesets/spr_tile_deco_ash_01.png",
        };

        // (path, baseScale) - scale relative to player (~0.9 unit).
        // Player ≈ 0.9u. Rock small=1u, Rock big=2u, Tree=2.5u, Stump=1u, Ruin pillar=2.5u, Ruin wall=3u, Ruin arch=3.5u
        private static readonly (string path, float baseScale)[] EnvPropEntries =
        {
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_rock_01.png", 3.0f),   // 용암 맥 바위 (중형)
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_rock_02.png", 2.0f),   // 소형 돌무더기
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_rock_03.png", 3.5f),   // 갈라진 대형 바위
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_rock_04.png", 2.5f),   // 흑요석 결정
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_tree_01.png", 4.5f),   // 숯검은 고목 (큰 나무)
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_tree_02.png", 3.5f),   // 쓰러진 통나무
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_tree_03.png", 2.2f),   // 그루터기
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_ruin_01.png", 4.0f),   // 부서진 기둥
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_ruin_02.png", 5.0f),   // 무너진 벽
            ("Assets/NightDash/Art/Stage01/Props/spr_prop_ruin_03.png", 5.5f),   // 돌 아치 잔해
        };

        private const string ParallaxPath = "Assets/NightDash/Art/Stage01/Tilesets/spr_stage_burning_wastes_parallax_bg.png";
        private static readonly Vector2 StageFillSize = new(88f, 54f);
        private static readonly Vector2 PlayableBounds = new(72f, 42f); // actual play area
        private static readonly Color BaseGroundColor = new(0.12f, 0.10f, 0.09f, 1f);
        private const float BaseTileSize = 4f;
        private const float BaseTileStep = 4f;
        private const float DecoPlacementChance = 0.25f;
        private const int EnvPropCount = 30;
        private const float EnvPropMinDistance = 10f;
        private const float SpawnSafeRadius = 7f;

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
            foreach (var p in GroundPaths) SliceSingleTexture(p);
            foreach (var p in DecoTilePaths) SliceSingleTexture(p);
            foreach (var (path, _) in EnvPropEntries) SliceSingleTexture(path);
            SliceSingleTexture(ParallaxPath);
        }

        private static void SliceSingleTexture(string assetPath)
        {
            var importer = GetConfiguredImporter(assetPath);
            if (importer == null) return;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritesheet = Array.Empty<SpriteMetaData>();
            importer.SaveAndReimport();
        }

        private static void SliceGridTexture(string assetPath, AtlasGrid atlas)
        {
            var importer = GetConfiguredImporter(assetPath);
            if (importer == null) return;
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
            if (importer == null) return;
            importer.isReadable = false;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = sprites.ToArray();
            importer.SaveAndReimport();
        }

        private static void SliceLargestIslandTexture(string assetPath)
        {
            var importer = GetConfiguredImporter(assetPath);
            if (importer == null) return;
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
            if (importer == null) return;
            importer.isReadable = false;
            importer.spriteImportMode = metas.Length == 1 ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritesheet = metas.Length == 1 ? Array.Empty<SpriteMetaData>() : metas;
            importer.SaveAndReimport();
        }

        private static void SliceLargestIslandsTexture(string assetPath, int maxSprites, int minIslandArea)
        {
            var importer = GetConfiguredImporter(assetPath);
            if (importer == null) return;
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
            if (importer == null) return;
            importer.isReadable = false;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = metas;
            importer.SaveAndReimport();
        }

        private static TextureImporter GetConfiguredImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[NightDash] Texture not found at '{assetPath}', skipping.");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 128f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point; // crisp pixel art

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

            // Base tile grid
            var baseTileSprite = LoadPrimarySprite(GroundPaths[0]);
            CreateBaseTileGrid(groundRoot.transform, baseTileSprite, StageFillSize, 1f, 1);

            // Place env props FIRST so we know their positions
            var envEntries = new List<(Sprite sprite, float baseScale)>();
            foreach (var (path, baseScale) in EnvPropEntries)
            {
                var s = LoadPrimarySprite(path);
                if (s != null) envEntries.Add((s, baseScale));
            }
            var propPositions = PlaceRandomEnvProps(propsRoot.transform, envEntries, PlayableBounds, 0.5f);

            // Deco overlays - avoid prop positions
            var decoSprites = new List<Sprite>();
            foreach (var p in DecoTilePaths)
            {
                var s = LoadPrimarySprite(p);
                if (s != null) decoSprites.Add(s);
            }
            CreateDecoOverlay(crackRoot.transform, decoSprites, StageFillSize, 0.95f, 5, propPositions);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateBaseTileGrid(Transform parent, Sprite tileSprite, Vector2 fillSize, float zDepth, int sortingOrder)
        {
            if (tileSprite == null) return;

            float tileSize = BaseTileSize;
            float step = BaseTileStep;
            int cols = Mathf.CeilToInt(fillSize.x / step) + 2;
            int rows = Mathf.CeilToInt(fillSize.y / step) + 2;
            float startX = -((cols - 1) * step) * 0.5f;
            float startY = ((rows - 1) * step) * 0.5f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var pos = new Vector3(startX + x * step, startY - y * step, zDepth);
                    CreateSprite(parent, $"Base_{x}_{y}", tileSprite, pos, new Vector2(tileSize, tileSize), sortingOrder, 1f);
                }
            }
        }

        private static void CreateDecoOverlay(Transform parent, List<Sprite> decoSprites, Vector2 fillSize, float zDepth, int sortingOrder, List<Vector2> avoidPositions)
        {
            if (decoSprites.Count == 0) return;

            const float avoidRadius = 5f; // keep decos away from props

            float step = BaseTileStep;
            int cols = Mathf.CeilToInt(fillSize.x / step) + 2;
            int rows = Mathf.CeilToInt(fillSize.y / step) + 2;
            float startX = -((cols - 1) * step) * 0.5f;
            float startY = ((rows - 1) * step) * 0.5f;

            var random = new System.Random(20260323);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (random.NextDouble() > DecoPlacementChance) continue;

                    float posX = startX + x * step + (float)(random.NextDouble() - 0.5) * 0.5f;
                    float posY = startY - y * step + (float)(random.NextDouble() - 0.5) * 0.5f;

                    // Skip if too close to any env prop
                    bool tooClose = false;
                    if (avoidPositions != null)
                    {
                        for (int i = 0; i < avoidPositions.Count; i++)
                        {
                            float dx = posX - avoidPositions[i].x;
                            float dy = posY - avoidPositions[i].y;
                            if (dx * dx + dy * dy < avoidRadius * avoidRadius)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                    }
                    if (tooClose) continue;

                    Sprite deco = decoSprites[random.Next(decoSprites.Count)];
                    var pos = new Vector3(posX, posY, zDepth);
                    float alpha = 0.6f + (float)random.NextDouble() * 0.4f;
                    CreateSprite(parent, $"Deco_{x}_{y}", deco, pos, new Vector2(BaseTileSize, BaseTileSize), sortingOrder, alpha);
                }
            }
        }

        private static List<Vector2> PlaceRandomEnvProps(Transform parent, List<(Sprite sprite, float baseScale)> entries, Vector2 bounds, float zDepth)
        {
            if (entries.Count == 0) return new List<Vector2>();

            // Sorting order 200+ ensures props render ON TOP of everything
            const int baseSortOrder = 200;

            var random = new System.Random(20260323);
            var placed = new List<Vector2>();
            var placedIndices = new List<int>();
            float halfW = bounds.x * 0.5f;
            float halfH = bounds.y * 0.5f;
            int attempts = 0;

            while (placed.Count < EnvPropCount && attempts < EnvPropCount * 20)
            {
                attempts++;
                float x = (float)(random.NextDouble() * 2 - 1) * halfW;
                float y = (float)(random.NextDouble() * 2 - 1) * halfH;
                var candidate = new Vector2(x, y);

                if (candidate.magnitude < SpawnSafeRadius) continue;

                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (Vector2.Distance(candidate, placed[i]) < EnvPropMinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Pick a prop that wasn't used nearby (avoid same type clustering)
                int idx = random.Next(entries.Count);
                // Check if same index was used within nearby radius
                bool duplicate = false;
                for (int retry = 0; retry < 5 && placed.Count > 0; retry++)
                {
                    duplicate = false;
                    for (int p = Mathf.Max(0, placed.Count - 6); p < placed.Count; p++)
                    {
                        if (Vector2.Distance(candidate, placed[p]) < EnvPropMinDistance * 1.5f &&
                            placedIndices.Count > p && placedIndices[p] == idx)
                        {
                            duplicate = true;
                            idx = random.Next(entries.Count);
                            break;
                        }
                    }
                    if (!duplicate) break;
                }

                placed.Add(candidate);
                placedIndices.Add(idx);
                var (sprite, baseScale) = entries[idx];
                float variation = 0.85f + (float)random.NextDouble() * 0.3f;
                float scale = baseScale * variation;

                // Y-sort based on object BOTTOM edge (y - scale/2)
                // Lower bottom = higher sorting order = rendered in front
                float bottomY = y - scale * 0.5f;
                int sortOrder = baseSortOrder + (int)((-bottomY) * 10);

                var pos = new Vector3(x, y, zDepth);
                CreateSprite(parent, $"Prop_{placed.Count}", sprite, pos,
                    new Vector2(scale, scale), sortOrder, 1f);
            }

            Debug.Log($"[NightDash] Placed {placed.Count} environment props.");
            return placed;
        }

        private static void CreateSeamlessGround(Transform parent, string texturePath, Vector2 fillSize, float zDepth, int sortingOrder, float alpha, Vector2 tiling)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[NightDash] Could not load texture at {texturePath} for seamless ground.");
                return;
            }

            texture.wrapMode = TextureWrapMode.Repeat;

            string name = System.IO.Path.GetFileNameWithoutExtension(texturePath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, zDepth);
            go.transform.localScale = new Vector3(fillSize.x, fillSize.y, 1f);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateQuadMesh();

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));

            meshRenderer.sharedMaterial = material;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "SeamlessGroundQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
