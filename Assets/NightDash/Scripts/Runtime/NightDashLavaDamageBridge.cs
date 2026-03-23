using System.Collections.Generic;
using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace NightDash.Runtime
{
    /// <summary>
    /// Deals damage to the player when standing on lava crack decorations.
    /// Scans the scene for GameObjects containing "deco_lava_crack" under Stage01Environment/Cracks.
    /// </summary>
    public sealed class NightDashLavaDamageBridge : MonoBehaviour
    {
        private const float LavaDamagePerSecond = 5f;
        private const float LavaRadius = 1.5f; // match deco tile visual radius

        private readonly List<Vector2> _lavaCracks = new();
        private World _world;
        private EntityQuery _playerQuery;
        private bool _scanned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("[NightDash] LavaDamageBridge");
            go.AddComponent<NightDashLavaDamageBridge>();
            DontDestroyOnLoad(go);
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsCreated)
            {
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world == null || !_world.IsCreated) return;

                var em = _world.EntityManager;
                _playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadWrite<LocalTransform>(),
                    ComponentType.ReadWrite<CombatStats>(),
                    ComponentType.Exclude<Prefab>());
            }

            if (!_scanned)
            {
                ScanLavaCracks();
                _scanned = true;
            }

            if (_lavaCracks.Count == 0 || _playerQuery.IsEmptyIgnoreFilter) return;

            var em2 = _world.EntityManager;
            using var players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0) return;

            var playerEntity = players[0];
            var transform = em2.GetComponentData<LocalTransform>(playerEntity);
            var stats = em2.GetComponentData<CombatStats>(playerEntity);
            var playerPos = new float2(transform.Position.x, transform.Position.y);

            float radiusSq = LavaRadius * LavaRadius;
            bool onLava = false;

            for (int i = 0; i < _lavaCracks.Count; i++)
            {
                float2 lavaPos = new float2(_lavaCracks[i].x, _lavaCracks[i].y);
                if (math.lengthsq(playerPos - lavaPos) < radiusSq)
                {
                    onLava = true;
                    break;
                }
            }

            if (onLava)
            {
                stats.CurrentHealth -= LavaDamagePerSecond * Time.deltaTime;
                if (stats.CurrentHealth < 0f) stats.CurrentHealth = 0f;
                em2.SetComponentData(playerEntity, stats);
            }
        }

        private void ScanLavaCracks()
        {
            _lavaCracks.Clear();
            var root = GameObject.Find("Stage01Environment");
            if (root == null) return;

            var cracksRoot = root.transform.Find("Cracks");
            if (cracksRoot == null) return;

            for (int i = 0; i < cracksRoot.childCount; i++)
            {
                var child = cracksRoot.GetChild(i);
                // Only lava crack decos, not embers/ash
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null && sr.sprite.name.Contains("lava_crack"))
                {
                    _lavaCracks.Add(new Vector2(child.position.x, child.position.y));
                }
            }

            NightDashLog.Info($"[LavaDamage] Scanned {_lavaCracks.Count} lava crack zones.");
        }
    }
}
