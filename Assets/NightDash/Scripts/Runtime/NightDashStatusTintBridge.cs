// Sprite-tint feedback for active status effects.
//
// Watches every entity carrying a StatusEffectState and re-colors the
// matching SpriteRenderer through NightDashDebugVisualBridge. When all
// effects clear we restore the renderer's original Color (cached on first
// touch). Priority order — stun > freeze > burn > poison — picks the
// dominant tint when an entity has multiple effects at once, with a small
// pulse so the visual reads as "active" instead of "skinned".

using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashStatusTintBridge : MonoBehaviour
    {
        // Tint colours per effect. Alpha stays 1 — we lerp between this and
        // the renderer's base color, so the value drives saturation/hue.
        private static readonly Color StunTint   = new(1.00f, 0.90f, 0.35f);
        private static readonly Color FreezeTint = new(0.55f, 0.80f, 1.00f);
        private static readonly Color BurnTint   = new(1.00f, 0.55f, 0.25f);
        private static readonly Color PoisonTint = new(0.50f, 0.95f, 0.45f);

        // Strength of the tint blend (0=no tint, 1=fully overwrites base).
        // Lower for the player so the class silhouette stays readable.
        private const float TintStrengthEnemy  = 0.65f;
        private const float TintStrengthPlayer = 0.45f;
        // Pulse modulation so even a steady status feels "active" — 4 Hz
        // sine ±10% on the tint strength.
        private const float PulseHz = 4f;
        private const float PulseAmplitude = 0.10f;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashStatusTintBridge>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashStatusTintBridge");
            go.AddComponent<NightDashStatusTintBridge>();
        }

        private NightDashDebugVisualBridge _visualBridge;
        private World _queryWorld;
        private EntityQuery _statusQuery;

        // Per-renderer cache: the renderer's original color the first time
        // we touched it, plus the entity that owns the active effect. When
        // the effect clears (or the entity vanishes) we restore from here.
        private readonly Dictionary<SpriteRenderer, Color> _baseColorCache = new();
        private readonly Dictionary<Entity, SpriteRenderer> _entityToRenderer = new();
        private readonly HashSet<Entity> _aliveThisFrame = new();
        private readonly List<Entity> _toRemove = new();

        private void LateUpdate()
        {
            if (_visualBridge == null)
            {
                _visualBridge = FindFirstObjectByType<NightDashDebugVisualBridge>(FindObjectsInactive.Include);
                if (_visualBridge == null) return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { RestoreAll(); return; }
            EntityManager em = world.EntityManager;
            EnsureQueriesFor(world, em);

            _aliveThisFrame.Clear();
            using var entities = _statusQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var states = _statusQuery.ToComponentDataArray<StatusEffectState>(Unity.Collections.Allocator.Temp);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * PulseHz * 2f * Mathf.PI) * PulseAmplitude;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!em.Exists(entity)) continue;
                StatusEffectState s = states[i];
                if (s.ActiveMask == 0) continue;

                if (!_visualBridge.TryGetRenderer(entity, out SpriteRenderer renderer)) continue;
                if (renderer == null) continue;

                _aliveThisFrame.Add(entity);

                // Cache the renderer's original color on first touch so we
                // can restore it cleanly when the effect ends.
                if (!_baseColorCache.ContainsKey(renderer))
                {
                    _baseColorCache[renderer] = renderer.color;
                }
                _entityToRenderer[entity] = renderer;

                Color tint = ResolveDominantTint(s.ActiveMask);
                bool isPlayer = em.HasComponent<PlayerTag>(entity);
                float strength = (isPlayer ? TintStrengthPlayer : TintStrengthEnemy) * pulse;
                strength = Mathf.Clamp01(strength);

                Color baseColor = _baseColorCache[renderer];
                Color result = Color.Lerp(baseColor, tint, strength);
                // Preserve the renderer's existing alpha — entities that
                // fade out (death VFX) shouldn't pop opaque due to tint.
                result.a = baseColor.a;
                renderer.color = result;
            }

            // Restore renderers whose entity dropped out of the query.
            _toRemove.Clear();
            foreach (var kv in _entityToRenderer)
            {
                if (!_aliveThisFrame.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                Entity stale = _toRemove[i];
                if (_entityToRenderer.TryGetValue(stale, out SpriteRenderer r) && r != null)
                {
                    if (_baseColorCache.TryGetValue(r, out Color baseColor))
                    {
                        r.color = baseColor;
                    }
                }
                _entityToRenderer.Remove(stale);
            }
        }

        // Pick the strongest visual effect when several are active. Stun is
        // most disruptive → wins the slot; poison is the gentlest fallback.
        private static Color ResolveDominantTint(byte mask)
        {
            if ((mask & StatusEffectBits.Stun)   != 0) return StunTint;
            if ((mask & StatusEffectBits.Freeze) != 0) return FreezeTint;
            if ((mask & StatusEffectBits.Burn)   != 0) return BurnTint;
            if ((mask & StatusEffectBits.Poison) != 0) return PoisonTint;
            return Color.white;
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _statusQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StatusEffectState>());
        }

        private void OnDestroy()
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _statusQuery.Dispose();
            }
            RestoreAll();
        }

        private void RestoreAll()
        {
            foreach (var kv in _entityToRenderer)
            {
                if (kv.Value == null) continue;
                if (_baseColorCache.TryGetValue(kv.Value, out Color baseColor))
                {
                    kv.Value.color = baseColor;
                }
            }
            _entityToRenderer.Clear();
            _aliveThisFrame.Clear();
        }
    }
}
