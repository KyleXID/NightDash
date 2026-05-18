// Sprint B / M2 Phase 1 — Canvas-based campfire lobby.
// Replaces RunSelectionLobbyUI's OnGUI flow with a uGUI screen that shows
// 7 character cards in a row, lit by a placeholder campfire (real campfire
// sprite + glow halo arrives in Phase 2). Arrow keys pick character, up/down
// pick stage, Enter starts the run, ESC returns to Title.
//
// ECS hand-off is preserved: stage/class selection still goes through
// RunSelectionLobbyWorldBridge.TryApplySelectionToCurrentWorld so the
// existing bootstrap/data systems keep working.

using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashLobbyScreenUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning
        private const int    CharacterCardCount = 7;
        private const float  CardHeight         = 240f;
        private const float  CardSelectedScale  = 1.08f;
        private const float  CardUnselectedScale = 1.0f;
        // Selected card boosts its base brightness so it reads brighter
        // even though both states share the same warmth wash.
        private const float  CardSelectedBrightness = 2.05f;
        private const float  IdleTimeScale      = 1.0f;

        // Campfire center. Cards split into left (4) and right (3) groups so
        // the fire stays unobstructed in the middle. Closer-to-fire cards sit
        // higher; outer cards descend along a soft curve.
        private static readonly Vector2 CampfireCenter = new Vector2(0f, -40f);
        private const int    LeftCardCount   = 4;
        private const int    RightCardCount  = 3;

        // Per-card offsets from campfire center. Hand-tuned so the layout
        // forms a soft "people gathered around a fire" shape:
        //   - Paladin / Priest (innermost, closest to fire) flat at fire level.
        //   - Warrior / Archer one step out, slightly lower.
        //   - Mage one more step out, lower still and closer to Warrior.
        //   - Astrologer / Gunslinger tucked inward + dropped further down,
        //     forming a back row near the outer edges.
        // Index order matches DataCatalog.classes:
        //   0 Astrologer, 1 Mage, 2 Warrior, 3 Paladin,
        //   4 Priest,     5 Archer, 6 Gunslinger
        private static readonly Vector2[] CardOffsets = new Vector2[]
        {
            new Vector2(-260f, -240f), // 0 Astrologer (back row, closer to fire)
            new Vector2(-170f,    0f), // 1 Mage (closest to fire on left)
            new Vector2(-340f,  -50f), // 2 Warrior (closer to fire than before)
            new Vector2(-480f, -180f), // 3 Paladin (outer-left, slightly above prev)
            new Vector2( 170f,    0f), // 4 Priest (closest to fire on right)
            new Vector2( 370f,  -50f), // 5 Archer
            new Vector2( 260f, -240f), // 6 Gunslinger (back row, mirror of Astrologer)
        };

        // Navigation sequence — visual left-to-right flow across cards.
        // Hand-authored so arrow keys traverse front row first, then back-row
        // dead-ends. No wrap-around at either end.
        //   <-: Warrior -> Paladin -> Astrologer | (cannot go further)
        //   ->: Warrior -> Mage -> Priest -> Archer -> Gunslinger | (cannot go further)
        // Numbers are DataCatalog.classes indices (0=Astrologer, 1=Mage,
        // 2=Warrior, 3=Paladin, 4=Priest, 5=Archer, 6=Gunslinger).
        private static readonly int[] NavOrder = new int[]
        {
            0, // Astrologer (leftmost end)
            3, // Paladin
            2, // Warrior (default start)
            1, // Mage
            4, // Priest
            5, // Archer
            6, // Gunslinger (rightmost end)
        };
        private const int DefaultNavIndex = 2; // -> Warrior

        private static readonly Color CardSelectedTint   = Color.white;
        private static readonly Color CardUnselectedTint = new Color(0.32f, 0.30f, 0.42f, 1f);

        // ------------------------------------------------------------------ state
        private RunSelectionLobbyUI _legacyLobbyUi; // adapter target
        private NightDashDebugVisualBridge _visualBridge;
        private Canvas _canvas;
        private GameObject _backgroundLayer;
        private RawImage _backgroundImage;
        private Material _warmthMaterial;
        private Text _stageLabel;
        private NightDashLobbyModifierPanel _modifierPanel;
        private readonly List<(string id, int level)> _modifierStageScratch = new();

        private Image _campfireImage;
        private Sprite[] _campfireFrames;
        private const float CampfireFps         = 8f;
        // Pinned at Y = -160 (canvas-center anchored). Do not edit unless you
        // also intend to move the campfire sprite itself — the environment
        // warmth center (_FireCenterUV) and the per-card distance falloff are
        // tuned independently and should not pull this value with them.
        private static readonly Vector2 CampfireSpriteCenter = new Vector2(25f, -135f);
        private static readonly Vector2 CampfireSpriteSize   = new Vector2(250f, 325f);

        // Tight halo right around the flames — small radius, soft pulse.
        // The wide ambient wash is handled by the warmth shader on the BG;
        // this halo just gives the fire itself a little bloom.
        private RectTransform _glowHaloRect;
        private Image _glowHaloImage;
        private const float GlowPulseHz   = 0.75f;
        private const float GlowAlphaMin  = 0.12f;
        private const float GlowAlphaMax  = 0.24f;
        private const float GlowScaleMin  = 0.95f;
        private const float GlowScaleMax  = 1.06f;
        private static readonly Vector2 GlowHaloSize = new Vector2(310f, 310f);

        // Per-card warmth wash. Both selected and unselected cards receive
        // the same warm add — only the base brightness differs. Gradient is
        // ease-out so cards close to the fire stay bright longer before
        // falling off.
        private const float CardLightFalloffPx    = 800f;
        private const float CardLightPulseHz      = 1.0f;
        private const float CardLightPulseMin     = 0.88f;
        private const float CardLightPulseMax     = 1.12f;
        private const float CardWarmGradientPower = 0.7f; // < 1 = ease-out
        private static readonly Color CardWarmAdd = new Color(0.65f, 0.32f, 0.08f, 0f);

        private readonly List<CharacterCard> _cards = new();
        private readonly List<string> _stageIds = new();
        private int _classIndex; // index into _cards / DataCatalog.classes
        private int _navIndex;   // index into NavOrder
        private int _stageIndex;
        private bool _initialized;
        private float _animTime;

        private struct CharacterCard
        {
            public string ClassId;
            public RectTransform Rect;
            public Image Image;
            public Text Label;
            public AnimationClipDef IdleClip;
            public bool FacesLeft; // mirrors sprite when card is right of center
        }

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashLobbyScreenUI");
            go.AddComponent<NightDashLobbyScreenUI>();
            // Hidden by default; Title.OnStartClicked enables this screen.
            go.SetActive(false);
        }

        private void Awake()
        {
            _legacyLobbyUi = FindFirstObjectByType<RunSelectionLobbyUI>(FindObjectsInactive.Include);
            // Stage ids must exist before BuildModifierPanel() runs — otherwise
            // the panel sees a null stageId on first entry and locks itself.
            CollectStageIds();
            BuildCanvas();
            BuildCards();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;

            NightDashInputContextStack.Push(NightDashInputContext.Lobby);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);

            // Keep gameplay frozen and views hidden — selection screen, not play.
            Time.timeScale = 0f;
            HideGameplayViews();

            // Belt-and-suspenders sweep: if any earlier flow left the ECS
            // world in RunStatus.Playing with stale enemy entities (or if
            // Bootstrap auto-set Playing before the player ever pressed
            // Start), the lobby would have monsters piling up at origin in
            // the background while the player browsed. Teardown is
            // idempotent — safe to call when nothing is alive.
            RunTeardownBridge.DestroyCurrentRun(clearNavigation: true);

            // Hard-disable the legacy OnGUI lobby so it cannot draw on top of us.
            if (_legacyLobbyUi != null)
            {
                _legacyLobbyUi.SetLobbyVisible(false);
                _legacyLobbyUi.enabled = false;
            }

            // Stage selection restores from PlayerPrefs as before, but
            // character always starts on Warrior per design — explicit
            // mid-row anchor for arrow-key navigation.
            RunSelectionSession.GetCurrent(out string savedStage, out _);
            _stageIndex = FindStageIndex(savedStage);
            _navIndex = DefaultNavIndex;
            _classIndex = NavOrder[_navIndex];
            ApplySelectionVisuals();
            UpdateStageLabel();
            _animTime = 0f;

            // Awake-time panel build saw _stageIndex == 0 (catalog order),
            // which can lock the panel if the first registered stage isn't
            // stage_01. Re-Configure with the real saved stage so the very
            // first lobby entry isn't stuck behind the clear-lock notice.
            if (_modifierPanel != null && _stageIds.Count > 0
                && _stageIndex >= 0 && _stageIndex < _stageIds.Count)
            {
                RunSelectionSession.GetCurrentModifierStages(_modifierStageScratch);
                _modifierPanel.Configure(_stageIds[_stageIndex], _modifierStageScratch);
            }
            // Seed the session with the lobby's initial selection so the
            // hold-Tab overlay sees the right class on the first frame.
            PushSelectionToSession();
        }

        private void OnDisable()
        {
            NightDashInputContextStack.Pop(NightDashInputContext.Lobby);
        }

        // ====================================================================
        // Canvas + visuals
        // ====================================================================
        private void BuildCanvas()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 4500; // below Title (5000)

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Wipe any previously-built children so re-Awake stays clean.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildBackground();
            BuildCampfire();
            BuildStageLabel();
            BuildHelpText();
            BuildModifierPanel();
        }

        private void BuildModifierPanel()
        {
            var rect = CreateRect("ModifierPanel", transform);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-40f, 0f);
            // Tight fit: header band (84px) + 5 chips at 118px + 4 spacings
            // at 10px + 12px bottom padding = 726px. No excess vertical space.
            rect.sizeDelta = new Vector2(380f, 726f);

            _modifierPanel = rect.gameObject.AddComponent<NightDashLobbyModifierPanel>();

            // Hydrate selected modifier stages from PlayerPrefs so the panel
            // restores the player's previous (id, level) state on lobby re-entry.
            RunSelectionSession.GetCurrentModifierStages(_modifierStageScratch);
            string currentStage = _stageIds.Count > 0 && _stageIndex >= 0 && _stageIndex < _stageIds.Count
                ? _stageIds[_stageIndex]
                : null;
            _modifierPanel.Configure(currentStage, _modifierStageScratch);
        }

        private void BuildStageLabel()
        {
            var rect = CreateRect("StageLabel", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -60f);
            rect.sizeDelta = new Vector2(1100f, 110f);

            var t = rect.gameObject.AddComponent<Text>();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 64;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            t.raycastTarget = false;
            AddTextOutline(t, 2.5f);
            _stageLabel = t;
        }

        private void UpdateStageLabel()
        {
            if (_stageLabel == null) return;
            if (_stageIndex < 0 || _stageIndex >= _stageIds.Count)
            {
                _stageLabel.text = "";
                return;
            }
            string id = _stageIds[_stageIndex];
            string display = id;
            var registry = DataRegistry.Instance;
            if (registry != null && registry.TryGetStage(id, out var stage) && stage != null
                && !string.IsNullOrEmpty(stage.displayName))
            {
                display = stage.displayName;
            }
            // Show prev/next hints only when there is somewhere to go.
            string left = _stageIndex > 0 ? "<  " : "    ";
            string right = _stageIndex < _stageIds.Count - 1 ? "  >" : "    ";
            _stageLabel.text = $"{left}{display}{right}";
        }

        private void BuildBackground()
        {
            var bgRect = CreateRect("Background", transform);
            StretchFull(bgRect);
            var bgImage = bgRect.gameObject.AddComponent<RawImage>();
            var tex = Resources.Load<Texture2D>("NightDash/UI/Lobby/lobby_campfire_background");
            if (tex != null)
            {
                bgImage.texture = tex;
                bgImage.color = Color.white;
                ApplyCoverFitUv(bgImage);
            }
            else
            {
                bgImage.color = new Color(0.06f, 0.04f, 0.12f, 1f);
            }
            bgImage.raycastTarget = false;

            // Apply warmth-overlay shader so background props (stones, logs,
            // trees) near the fire receive the same warmth wash as the cards.
            var shader = Shader.Find("NightDash/UI/WarmthOverlay");
            if (shader != null)
            {
                _warmthMaterial = new Material(shader);
                _warmthMaterial.SetVector("_FireCenterUV", new Vector4(0.5f, 0.45f, 0f, 0f));
                _warmthMaterial.SetFloat("_FireRadiusUV", 0.40f);
                _warmthMaterial.SetColor("_WarmthColor", new Color(0.50f, 0.24f, 0.06f, 1f));
                // Power 2.5 = sharp ease-in: light concentrates right next to
                // the fire and fades quickly with distance. Peak intensity is
                // bumped to 0.50 to make the close band genuinely dramatic;
                // the falloff curve keeps the screen edge as quiet as before.
                _warmthMaterial.SetFloat("_GradientPower", 2.5f);
                _warmthMaterial.SetFloat("_WarmthIntensity", 0.50f);
                bgImage.material = _warmthMaterial;
            }
            _backgroundImage = bgImage;
            _backgroundLayer = bgRect.gameObject;
        }

        // Same cover-fit logic as the Title: keep native aspect, trim along
        // the longer axis so the texture fills the screen without squashing.
        private static void ApplyCoverFitUv(RawImage img)
        {
            if (img == null || img.texture == null) return;
            float texAspect = (float)img.texture.width / Mathf.Max(1, img.texture.height);
            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (texAspect > screenAspect)
            {
                float fit = screenAspect / texAspect;
                float pad = (1f - fit) * 0.5f;
                img.uvRect = new Rect(pad, 0f, fit, 1f);
            }
            else
            {
                float fit = texAspect / screenAspect;
                float pad = (1f - fit) * 0.5f;
                img.uvRect = new Rect(0f, pad, 1f, fit);
            }
        }

        private void BuildCampfire()
        {
            // Build the flame sprite first so the glow halo (created next)
            // becomes a later sibling and renders ON TOP of the sprite.
            // The halo's additive-feeling alpha pulse then reads as light
            // spilling forward off the flames, not a disc behind them.
            var fireRect = CreateRect("CampfireSprite", transform);
            fireRect.anchorMin = fireRect.anchorMax = new Vector2(0.5f, 0.5f);
            fireRect.pivot = new Vector2(0.5f, 0.5f);
            fireRect.sizeDelta = CampfireSpriteSize;
            fireRect.anchoredPosition = CampfireSpriteCenter;

            var fireImage = fireRect.gameObject.AddComponent<Image>();
            fireImage.preserveAspect = true;
            fireImage.raycastTarget = false;
            _campfireFrames = LoadCampfireFrames();
            if (_campfireFrames != null && _campfireFrames.Length > 0)
            {
                fireImage.sprite = _campfireFrames[0];
                fireImage.color = Color.white;
            }
            else
            {
                fireImage.color = new Color(1f, 0.55f, 0.18f, 0.85f); // visual stub
            }
            _campfireImage = fireImage;

            // Glow halo lifted +60 over the sprite center so it hovers around
            // the flame tips rather than the log base, and rendered after the
            // sprite so it sits in front (light reads as foreground bloom).
            var glowRect = CreateRect("CampfireGlow", transform);
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = GlowHaloSize;
            glowRect.anchoredPosition = new Vector2(CampfireSpriteCenter.x,
                                                    CampfireSpriteCenter.y + 60f);

            var glowImage = glowRect.gameObject.AddComponent<Image>();
            glowImage.preserveAspect = true;
            glowImage.raycastTarget = false;
            var glowSprite = Resources.Load<Sprite>("NightDash/UI/Lobby/lobby_campfire_glow_halo");
            if (glowSprite != null)
            {
                glowImage.sprite = glowSprite;
                glowImage.color = new Color(1f, 0.85f, 0.55f, GlowAlphaMin);
            }
            else
            {
                glowImage.color = new Color(1f, 0.55f, 0.20f, 0.20f);
            }
            _glowHaloRect = glowRect;
            _glowHaloImage = glowImage;
        }

        private static Sprite[] LoadCampfireFrames()
        {
            var list = new List<Sprite>(8);
            for (int i = 0; i < 16; i++)
            {
                var s = Resources.Load<Sprite>($"NightDash/UI/Lobby/Campfire/frame_{i:000}");
                if (s == null) break;
                list.Add(s);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private void BuildCards()
        {
            _cards.Clear();

            var registry = DataRegistry.Instance;
            if (registry == null || registry.Catalog == null) return;
            // Snapshot the catalog into a *stable* local list ordered by the
            // canonical class id sequence below. DataCatalog rebuilds (e.g.
            // after Reimport / new .asset added) reorder Resources.LoadAll
            // results, which would otherwise shuffle the lobby cards.
            var classes = SortClassesByCanonicalOrder(registry.Catalog.classes);
            if (classes == null || classes.Count == 0) return;

            int cardCount = Mathf.Min(CharacterCardCount, classes.Count);

            // Per-card width follows each PNG's native aspect ratio so every
            // character renders at the same on-screen height regardless of
            // how wide their silhouette is (shields/bows etc.).
            float[] widths = new float[cardCount];
            Sprite[] firstFrames = new Sprite[cardCount];
            AnimationClipDef[] idleClips = new AnimationClipDef[cardCount];
            for (int i = 0; i < cardCount; i++)
            {
                var classData = classes[i];
                if (classData == null || string.IsNullOrEmpty(classData.id)) continue;
                if (registry.TryGetAnimationSet(classData.id, out var animSet) && animSet != null)
                {
                    idleClips[i] = animSet.GetClipOrFallback("Idle", "Walk");
                    if (idleClips[i] != null && idleClips[i].FrameCount > 0)
                    {
                        firstFrames[i] = idleClips[i].frames[0];
                    }
                }
                float aspect = 0.6f;
                if (firstFrames[i] != null && firstFrames[i].rect.height > 0f)
                {
                    aspect = firstFrames[i].rect.width / firstFrames[i].rect.height;
                }
                widths[i] = CardHeight * aspect;
            }

            int actualLeft = Mathf.Min(LeftCardCount, cardCount);

            for (int i = 0; i < cardCount; i++)
            {
                var classData = classes[i];
                if (classData == null || string.IsNullOrEmpty(classData.id)) continue;

                bool onLeft = i < actualLeft;

                // Pick offset from the hand-tuned table; fall back to a
                // simple linear layout if more than 7 classes ever appear.
                Vector2 offset;
                if (i < CardOffsets.Length)
                {
                    offset = CardOffsets[i];
                }
                else
                {
                    float fallbackX = onLeft ? -((i - actualLeft + 1) * 200f) : ((i - actualLeft + 1) * 200f);
                    offset = new Vector2(fallbackX, -100f);
                }

                float cardX = CampfireCenter.x + offset.x;
                float cardY = CampfireCenter.y + offset.y;

                var rect = CreateRect($"Card_{classData.id}", transform);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(widths[i], CardHeight);
                rect.anchoredPosition = new Vector2(cardX, cardY);

                var image = rect.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (firstFrames[i] != null) image.sprite = firstFrames[i];

                // Class name label as a sibling of the card (not a child) so
                // mirroring the card's localScale.x doesn't flip the text.
                var labelRect = CreateRect($"Label_{classData.id}", transform);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(cardX, cardY - CardHeight * 0.5f - 8f);
                // Height bumped to 80 so the 48pt label has vertical room
                // (Silver glyphs are tall — pixel ascent ≈ font size × 1.1).
                labelRect.sizeDelta = new Vector2(Mathf.Max(widths[i], 200f), 80f);

                var label = labelRect.gameObject.AddComponent<Text>();
                label.text = string.IsNullOrEmpty(classData.displayName) ? classData.id : classData.displayName;
                label.alignment = TextAnchor.UpperCenter;
                label.fontSize = 40;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.color = Color.white;
                label.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
                label.raycastTarget = false;
                AddTextOutline(label, 2f);

                _cards.Add(new CharacterCard
                {
                    ClassId = classData.id,
                    Rect = rect,
                    Image = image,
                    Label = label,
                    IdleClip = idleClips[i],
                    // Right-side cards mirror so they face inward toward fire.
                    FacesLeft = !onLeft,
                });
            }

            SortCardsByDepth();
        }

        // Reorders sibling indices to compose the lobby with proper depth.
        // Cards on the far side of the fire (anchoredY above the glow halo,
        // i.e. visually further from camera) sit BEHIND the halo so light
        // washes over them. Cards on the near side sit IN FRONT of the halo
        // so they read as foreground silhouettes the fire is back-lighting.
        // Final stack (back → front):
        //   bg → CampfireSprite → back cards (Y desc) → glow halo
        //   → front cards (Y desc) → stage label → help text
        private void SortCardsByDepth()
        {
            float glowY = _glowHaloRect != null
                ? _glowHaloRect.anchoredPosition.y
                : float.NegativeInfinity;

            var sortedIdx = new List<int>(_cards.Count);
            for (int i = 0; i < _cards.Count; i++) sortedIdx.Add(i);
            // Y descending = back-to-front (higher Y is further from camera).
            sortedIdx.Sort((a, b) =>
                _cards[b].Rect.anchoredPosition.y.CompareTo(_cards[a].Rect.anchoredPosition.y));

            // 1) Back cards (Y > glowY) become the next siblings in order.
            foreach (int i in sortedIdx)
            {
                if (_cards[i].Rect == null) continue;
                if (_cards[i].Rect.anchoredPosition.y <= glowY) continue;
                _cards[i].Rect.SetAsLastSibling();
                if (_cards[i].Label != null) _cards[i].Label.transform.SetAsLastSibling();
            }

            // 2) Halo on top of the back cards so the fire glow spills onto
            // characters standing on the far side of the campfire.
            if (_glowHaloRect != null) _glowHaloRect.SetAsLastSibling();

            // 3) Front cards (Y <= glowY) on top of the halo — they occlude
            // the bloom because they stand between the fire and the camera.
            foreach (int i in sortedIdx)
            {
                if (_cards[i].Rect == null) continue;
                if (_cards[i].Rect.anchoredPosition.y > glowY) continue;
                _cards[i].Rect.SetAsLastSibling();
                if (_cards[i].Label != null) _cards[i].Label.transform.SetAsLastSibling();
            }

            // UI labels on the very top so they stay readable through bloom.
            if (_stageLabel != null) _stageLabel.transform.SetAsLastSibling();
            var helpText = transform.Find("HelpText");
            if (helpText != null) helpText.SetAsLastSibling();
        }

        private void BuildHelpText()
        {
            var rect = CreateRect("HelpText", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(1400f, 80f);

            var t = rect.gameObject.AddComponent<Text>();
            t.text = "← →  CHARACTER     ↑ ↓  STAGE     [TAB]  INFO     ENTER  START     ESC  BACK";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 40;
            t.color = new Color(1f, 1f, 1f, 0.78f);
            t.font = NightDash.Runtime.UI.NightDashUIFonts.Arcade;
            t.raycastTarget = false;
            AddTextOutline(t, 1.5f);
        }

        // Lobby-wide text outline helper. All lobby glyphs sit on top of the
        // warm pulsing background, so a consistent dark outline keeps them
        // readable regardless of the warmth shader phase.
        private static void AddTextOutline(Text text, float distance)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(distance, -distance);
        }

        // ====================================================================
        // Per-frame animation + input
        // ====================================================================
        private void Update()
        {
            if (NightDashInputContextStack.Top != NightDashInputContext.Lobby) return;

            _animTime += Time.unscaledDeltaTime;
            TickCardIdle();
            TickCampfire();
            TickCardLighting();
            TickBackgroundWarmth();

            ReadInput(out bool left, out bool right, out bool up, out bool down, out bool confirm, out bool cancel);

            if (left)       { MoveClass(-1); }
            else if (right) { MoveClass(+1); }
            else if (up)    { MoveStage(-1); }
            else if (down)  { MoveStage(+1); }
            else if (confirm) { StartRun(); }
            else if (cancel)  { BackToTitle(); }
        }

        // Per-card warmth wash — simulates fire light on each character without
        // relying on a single halo sprite. Cards closer to the fire get more
        // orange added on top of their selection base color; the magnitude
        // pulses gently with the fire's rhythm.
        private void TickCardLighting()
        {
            float pulseT = 0.5f + 0.5f * Mathf.Sin(_animTime * CardLightPulseHz * Mathf.PI * 2f);
            float pulse = Mathf.Lerp(CardLightPulseMin, CardLightPulseMax, pulseT);

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.Image == null || card.Rect == null) continue;
                bool selected = i == _classIndex;

                // Distance-based proximity, then ease-out so close cards
                // hold near-1 longer before the gradient drops.
                float dist = Vector2.Distance(card.Rect.anchoredPosition, CampfireSpriteCenter);
                float proximity = Mathf.Clamp01(1f - dist / CardLightFalloffPx);
                float gradient = Mathf.Pow(proximity, CardWarmGradientPower);

                // Same warmth add for everyone; base brightness differentiates
                // the selected card.
                Color baseTint = selected
                    ? CardUnselectedTint * CardSelectedBrightness
                    : CardUnselectedTint;
                Color warmAdd = CardWarmAdd * (gradient * pulse);
                Color final = baseTint + warmAdd;
                final.a = 1f;
                card.Image.color = final;
            }
        }

        private void TickCampfire()
        {
            // Frame swap (uniform fps loop). Logs/embers are nearly static
            // across frames (PixelLab Custom Animation V3 honored the
            // reference); only the flame region animates.
            if (_campfireImage != null && _campfireFrames != null && _campfireFrames.Length > 0)
            {
                int idx = Mathf.FloorToInt(_animTime * CampfireFps) % _campfireFrames.Length;
                if (idx < 0) idx += _campfireFrames.Length;
                var s = _campfireFrames[idx];
                if (s != null && _campfireImage.sprite != s) _campfireImage.sprite = s;
            }

            // Soft halo right around the flames. SCALE pulse intentionally
            // disabled — halo sits in front of the campfire sprite (user
            // request: light reads as foreground bloom), and a scale pulse
            // there caused the halo's coverage of the sprite to oscillate,
            // making the campfire body look like it was beating like a
            // heart. Alpha pulse alone keeps the bloom alive without
            // changing how much of the sprite is occluded.
            if (_glowHaloImage != null && _glowHaloRect != null)
            {
                float u = (Mathf.Sin(_animTime * GlowPulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
                var c = _glowHaloImage.color;
                c.a = Mathf.Lerp(GlowAlphaMin, GlowAlphaMax, u);
                _glowHaloImage.color = c;
                _glowHaloRect.localScale = Vector3.one;
            }
        }

        private void TickBackgroundWarmth()
        {
            if (_warmthMaterial == null) return;
            // Drive the same ambient pulse the cards use so background props
            // and characters flicker in sync with the flames.
            float pulseT = 0.5f + 0.5f * Mathf.Sin(_animTime * CardLightPulseHz * Mathf.PI * 2f);
            float pulse = Mathf.Lerp(CardLightPulseMin, CardLightPulseMax, pulseT);
            // Base intensity 0.50 — paired with GradientPower 2.5 so peak
            // warmth right next to the fire is dramatic while distant edges
            // stay quiet (steep falloff).
            _warmthMaterial.SetFloat("_WarmthIntensity", 0.50f * pulse);
        }

        private void TickCardIdle()
        {
            float t = _animTime * IdleTimeScale;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IdleClip == null || card.IdleClip.FrameCount == 0 || card.Image == null) continue;

                // Only the selected card animates; everyone else holds frame 0.
                Sprite sprite = (i == _classIndex)
                    ? card.IdleClip.GetFrameAt(t)
                    : card.IdleClip.frames[0];
                if (sprite != null) card.Image.sprite = sprite;
            }
        }

        private static void ReadInput(out bool left, out bool right, out bool up, out bool down, out bool confirm, out bool cancel)
        {
            left = right = up = down = confirm = cancel = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
            cancel = kb.escapeKey.wasPressedThisFrame;
#else
            left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
            cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ====================================================================
        // Selection
        // ====================================================================
        private void MoveClass(int delta)
        {
            if (_cards.Count == 0 || NavOrder.Length == 0) return;
            int prevNav = _navIndex;
            // Hard clamp: no wrap-around at either end of NavOrder.
            _navIndex = Mathf.Clamp(_navIndex + delta, 0, NavOrder.Length - 1);
            if (_navIndex == prevNav) return;

            int classIdx = NavOrder[_navIndex];
            if (classIdx < 0 || classIdx >= _cards.Count) return;
            _classIndex = classIdx;
            _animTime = 0f; // restart idle from frame 0
            ApplySelectionVisuals();
            PushSelectionToSession();
        }

        private void MoveStage(int delta)
        {
            if (_stageIds.Count == 0) return;
            int prev = _stageIndex;
            // Hard clamp at the ends, matching character navigation.
            _stageIndex = Mathf.Clamp(_stageIndex + delta, 0, _stageIds.Count - 1);
            if (_stageIndex != prev)
            {
                UpdateStageLabel();
                // Stage changed: rebuild modifier panel so clear-locked stages
                // gray out the toggles and previously-saved selections still
                // pre-fill where applicable.
                if (_modifierPanel != null)
                {
                    RunSelectionSession.GetCurrentModifierStages(_modifierStageScratch);
                    _modifierPanel.Configure(_stageIds[_stageIndex], _modifierStageScratch);
                }
                PushSelectionToSession();
            }
        }

        // Mirror the lobby's current stage+class selection into
        // RunSelectionSession without disturbing the modifier stack — so the
        // hold-Tab class info overlay (and any other transient reader) can
        // see the live selection while the player is still browsing.
        private void PushSelectionToSession()
        {
            if (_cards.Count == 0 || _stageIds.Count == 0) return;
            int classIdx = Mathf.Clamp(_classIndex, 0, _cards.Count - 1);
            int stageIdx = Mathf.Clamp(_stageIndex, 0, _stageIds.Count - 1);
            string classId = _cards[classIdx].ClassId;
            string stageId = _stageIds[stageIdx];
            RunSelectionSession.SetCurrentStageAndClass(stageId, classId);
        }

        private void ApplySelectionVisuals()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.Image == null || card.Rect == null) continue;
                bool selected = i == _classIndex;
                // Image.color is updated every frame by TickCardLighting; we
                // intentionally don't set it here so the selection change
                // doesn't fight the warmth pulse.
                if (card.Label != null)
                {
                    var c = card.Label.color;
                    c.a = selected ? 1f : 0.5f;
                    card.Label.color = c;
                }
                float scale = selected ? CardSelectedScale : CardUnselectedScale;
                float xScale = card.FacesLeft ? -scale : scale;
                card.Rect.localScale = new Vector3(xScale, scale, 1f);
                // No sibling reordering — preserve back-row perspective
                // (Astrologer/Gunslinger stay behind their neighbours).
            }
        }

        // ====================================================================
        // Stage list
        // ====================================================================
        private void CollectStageIds()
        {
            _stageIds.Clear();
            var registry = DataRegistry.Instance;
            if (registry == null || registry.Catalog == null) return;
            var stages = registry.Catalog.stages;
            if (stages == null) return;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && !string.IsNullOrEmpty(stages[i].id))
                {
                    _stageIds.Add(stages[i].id);
                }
            }
            if (_stageIds.Count == 0) _stageIds.Add("stage_01");
        }

        private int FindClassIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].ClassId == id) return i;
            return 0;
        }

        private int FindStageIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < _stageIds.Count; i++)
                if (_stageIds[i] == id) return i;
            return 0;
        }

        // ====================================================================
        // Actions
        // ====================================================================
        private void StartRun()
        {
            if (_cards.Count == 0 || _stageIds.Count == 0) return;
            string classId = _cards[Mathf.Clamp(_classIndex, 0, _cards.Count - 1)].ClassId;
            string stageId = _stageIds[Mathf.Clamp(_stageIndex, 0, _stageIds.Count - 1)];

            // Capture modifier picks with stack levels (empty if stage is locked / nothing chosen).
            _modifierStageScratch.Clear();
            if (_modifierPanel != null) _modifierPanel.GetSelectedModifierStages(_modifierStageScratch);

            RunSelectionSession.SetCurrent(stageId, classId, _modifierStageScratch);
            bool applied = RunSelectionLobbyWorldBridge.TryApplySelectionToCurrentWorld(stageId, classId);
            NightDashLog.Info($"[NightDash] Lobby Start: stage='{stageId}', class='{classId}', modifiers={_modifierStageScratch.Count}, applied={applied}.");

            // Resume sim, restore gameplay views, hand off input context.
            Time.timeScale = 1f;
            RestoreGameplayViews();

            // Explicitly hand off the input context to Playing BEFORE we
            // disable. OnDisable's Pop(Lobby) below will silently no-op
            // because Top is now Playing — that's the intended invariant.
            // Without this Push, ESC during gameplay has nothing to bind
            // to and the Pause Menu would never trigger (silent failure).
            NightDashInputContextStack.Pop(NightDashInputContext.Lobby);
            NightDashInputContextStack.Push(NightDashInputContext.Playing);

            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Playing);
            gameObject.SetActive(false);
        }

        private void BackToTitle()
        {
            // Title screen handles its own pause + view-hide on enable.
            // Pop Lobby first so Title.OnEnable's Push(Title) lands on a
            // clean stack — otherwise OnDisable's Pop(Lobby) would silently
            // fail (Top would already be Title) and Lobby would leak.
            NightDashInputContextStack.Pop(NightDashInputContext.Lobby);

            var title = FindFirstObjectByType<NightDashTitleScreenUI>(FindObjectsInactive.Include);
            if (title != null) title.gameObject.SetActive(true);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Title);
            gameObject.SetActive(false);
        }

        // ====================================================================
        // Helpers
        // ====================================================================
        private void HideGameplayViews()
        {
            if (_visualBridge == null)
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (_visualBridge != null)
            {
                _visualBridge.DestroyAllViewsImmediate();
                _visualBridge.enabled = false;
            }
        }

        private void RestoreGameplayViews()
        {
            if (_visualBridge == null)
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>();
            if (_visualBridge != null) _visualBridge.enabled = true;
        }

        // Canonical class display order for the lobby cards. DataCatalog
        // sorts by GUID under the hood, which means adding any asset can
        // re-shuffle the roster — pinning the sequence here keeps Warrior
        // in slot 0 etc. regardless of catalog state.
        private static readonly string[] ClassOrder =
        {
            "class_warrior",
            "class_priest",
            "class_mage",
            "class_archer",
            "class_astrologer",
            "class_gunslinger",
            "class_paladin",
        };

        private static System.Collections.Generic.List<NightDash.Data.ClassData> SortClassesByCanonicalOrder(
            System.Collections.Generic.List<NightDash.Data.ClassData> input)
        {
            if (input == null) return null;
            var ordered = new System.Collections.Generic.List<NightDash.Data.ClassData>(input.Count);
            // Pass 1 — emit in the canonical order, skipping any id the
            // catalog doesn't actually have on disk.
            for (int i = 0; i < ClassOrder.Length; i++)
            {
                for (int j = 0; j < input.Count; j++)
                {
                    if (input[j] != null && input[j].id == ClassOrder[i])
                    {
                        ordered.Add(input[j]);
                        break;
                    }
                }
            }
            // Pass 2 — append anything the catalog has that wasn't in the
            // canonical list so new classes still show up even before
            // ClassOrder is updated.
            for (int j = 0; j < input.Count; j++)
            {
                if (input[j] == null) continue;
                bool alreadyAdded = false;
                for (int k = 0; k < ordered.Count; k++)
                {
                    if (ordered[k] == input[j]) { alreadyAdded = true; break; }
                }
                if (!alreadyAdded) ordered.Add(input[j]);
            }
            return ordered;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
