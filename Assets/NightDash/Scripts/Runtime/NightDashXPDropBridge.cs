// ============================================================================
// NightDashXPDropBridge.cs
// Spawns visual XP gem sprites when enemies die. Gems wait briefly then
// accelerate toward the player (magnet effect).
// ============================================================================

using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashXPDropBridge : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning
        private const float GemScale       = 1.5f;
        private const int   GemSortOrder   = 90;
        private const float WaitTime       = 0.30f;  // sit still before moving
        private const float BaseSpeed      = 2.0f;
        private const float AccelFactor    = 8.0f;   // speed boost as distance shrinks
        private const float PickupRadius   = 0.30f;

        // ------------------------------------------------------------------ HP drop
        private const float HealthDropChance = 0.15f; // 15% chance per kill
        private const string PathHealthOrb = "NightDash/Art/Stage01/Items/spr_item_health_orb";
        private const float HealthRestoreAmount = 10f;

        // ------------------------------------------------------------------ sprite path
        private const string PathXPGem = "NightDash/Art/Stage01/Items/spr_item_xp_gem";

        // ------------------------------------------------------------------ state
        private World _world;
        private EntityQuery _playerQuery;
        private Sprite _gemSprite;
        private Sprite _healthSprite;

        private readonly List<XPGemAnimator> _activeGems = new();

        // ====================================================================
        // Bootstrap
        // ====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] XPDropBridge");
            go.AddComponent<NightDashXPDropBridge>();
            DontDestroyOnLoad(go);
            NightDashLog.Info("[XPDropBridge] Auto-created.");
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================
        private void OnEnable()
        {
            NightDashVFXBridge.OnEnemyDeath += HandleEnemyDeath;
        }

        private void OnDisable()
        {
            NightDashVFXBridge.OnEnemyDeath -= HandleEnemyDeath;
        }

        private void LateUpdate()
        {
            if (!TryGetWorld()) return;

            // Get current player position
            Vector3 playerPos = GetPlayerPosition();

            // Update all active gems
            for (int i = _activeGems.Count - 1; i >= 0; i--)
            {
                var gem = _activeGems[i];
                if (gem == null || gem.gameObject == null)
                {
                    _activeGems.RemoveAt(i);
                    continue;
                }

                gem.SetPlayerPosition(playerPos);

                // Check pickup
                float dist = Vector3.Distance(gem.transform.position, playerPos);
                if (dist < PickupRadius)
                {
                    if (gem.IsHealthOrb)
                    {
                        HealPlayer(HealthRestoreAmount);
                    }
                    Destroy(gem.gameObject);
                    _activeGems.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            NightDashVFXBridge.OnEnemyDeath -= HandleEnemyDeath;

            foreach (var gem in _activeGems)
            {
                if (gem != null && gem.gameObject != null)
                    Destroy(gem.gameObject);
            }
            _activeGems.Clear();
        }

        // ====================================================================
        // World init
        // ====================================================================
        private bool TryGetWorld()
        {
            if (_world != null && _world.IsCreated) return true;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return false;

            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Prefab>());

            _gemSprite = LoadGemSprite();
            _healthSprite = Resources.Load<Sprite>(PathHealthOrb);

            NightDashLog.Info("[XPDropBridge] Queries initialised.");
            return true;
        }

        // ====================================================================
        // Event handler
        // ====================================================================
        private void HandleEnemyDeath(Vector3 deathPos)
        {
            SpawnGem(deathPos);

            // 15% chance to drop a health orb
            if (Random.value < HealthDropChance)
            {
                SpawnHealthOrb(deathPos + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0f));
            }
        }

        // ====================================================================
        // Gem spawner
        // ====================================================================
        private void SpawnGem(Vector3 pos)
        {
            var go = new GameObject("[XP] Gem");
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * GemScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _gemSprite ?? CreatePlaceholder();
            sr.sortingOrder  = GemSortOrder;
            sr.color         = new Color(0.3f, 1f, 0.5f, 1f);

            var animator = go.AddComponent<XPGemAnimator>();
            animator.Init(WaitTime, BaseSpeed, AccelFactor, false);
            _activeGems.Add(animator);
        }

        private void SpawnHealthOrb(Vector3 pos)
        {
            var go = new GameObject("[HP] HealthOrb");
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * GemScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _healthSprite ?? CreatePlaceholder();
            sr.sortingOrder  = GemSortOrder;
            sr.color         = new Color(1f, 0.4f, 0.4f, 1f); // red tint

            var animator = go.AddComponent<XPGemAnimator>();
            animator.Init(WaitTime, BaseSpeed, AccelFactor, true);
            _activeGems.Add(animator);
        }

        // ====================================================================
        // Player position
        // ====================================================================
        private Vector3 GetPlayerPosition()
        {
            if (_playerQuery == null || !_playerQuery.IsEmptyIgnoreFilter == false)
            {
                // fallback if query is empty
            }

            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (transforms.Length > 0)
            {
                var p = transforms[0].Position;
                return new Vector3(p.x, p.y, 0f);
            }

            return Vector3.zero;
        }

        // ====================================================================
        // Sprite
        // ====================================================================
        private void HealPlayer(float amount)
        {
            if (_world == null || !_world.IsCreated) return;
            var em = _world.EntityManager;
            using var players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0) return;

            var stats = em.GetComponentData<CombatStats>(players[0]);
            stats.CurrentHealth = Mathf.Min(stats.CurrentHealth + amount, stats.MaxHealth);
            em.SetComponentData(players[0], stats);
        }

        private static Sprite LoadGemSprite()
        {
            var sprite = Resources.Load<Sprite>(PathXPGem);
            if (sprite == null)
            {
                NightDashLog.Info($"[XPDropBridge] Sprite not found: '{PathXPGem}', using placeholder.");
                return CreatePlaceholder();
            }
            return sprite;
        }

        private static Sprite CreatePlaceholder()
        {
            var tex = new Texture2D(8, 8);
            var px  = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.3f, 1f, 0.5f, 1f);
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
        }
    }

    // ========================================================================
    // XP Gem movement: wait → move toward player with increasing speed.
    // ========================================================================
    public sealed class XPGemAnimator : MonoBehaviour
    {
        private float _waitTime;
        private float _baseSpeed;
        private float _accelFactor;
        private float _elapsed;
        private Vector3 _playerPos;

        public bool IsHealthOrb { get; private set; }

        public void Init(float waitTime, float baseSpeed, float accelFactor, bool isHealthOrb = false)
        {
            _waitTime    = waitTime;
            _baseSpeed   = baseSpeed;
            _accelFactor = accelFactor;
            IsHealthOrb  = isHealthOrb;
        }

        public void SetPlayerPosition(Vector3 pos)
        {
            _playerPos = pos;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // Wait phase
            if (_elapsed < _waitTime) return;

            // Move toward player with accelerating speed
            float dist  = Vector3.Distance(transform.position, _playerPos);
            float speed = _baseSpeed + _accelFactor / Mathf.Max(dist, 0.1f);

            transform.position = Vector3.MoveTowards(
                transform.position, _playerPos, speed * Time.deltaTime);
        }
    }
}
