using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;

namespace NightDash.Runtime
{
    public sealed class NightDashCameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.08f;
        [SerializeField] private bool lockZ = true;
        [SerializeField] private float fixedZ = -10f;

        private Camera _camera;
        private Vector3 _velocity;
        private EntityQuery _playerQuery;
        private EntityQuery _stageQuery;
        private EntityManager _entityManager;
        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashCameraFollow>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            cam.gameObject.AddComponent<NightDashCameraFollow>();
        }

        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized())
            {
                return;
            }
            if (_camera == null)
            {
                return;
            }

            using var players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0)
            {
                return;
            }

            LocalTransform player = _entityManager.GetComponentData<LocalTransform>(players[0]);
            Vector3 current = _camera.transform.position;
            float targetZ = lockZ ? fixedZ : current.z;
            float targetX = player.Position.x;
            float targetY = player.Position.y;

            if (!_stageQuery.IsEmptyIgnoreFilter)
            {
                StageRuntimeConfig stage = _stageQuery.GetSingleton<StageRuntimeConfig>();
                if (stage.UseBounds == 1)
                {
                    float halfHeight = _camera.orthographic ? _camera.orthographicSize : 5f;
                    float halfWidth = halfHeight * _camera.aspect;
                    float minX = stage.BoundsMin.x + halfWidth;
                    float maxX = stage.BoundsMax.x - halfWidth;
                    float minY = stage.BoundsMin.y + halfHeight;
                    float maxY = stage.BoundsMax.y - halfHeight;

                    if (minX > maxX)
                    {
                        targetX = (stage.BoundsMin.x + stage.BoundsMax.x) * 0.5f;
                    }
                    else
                    {
                        targetX = math.clamp(targetX, minX, maxX);
                    }

                    if (minY > maxY)
                    {
                        targetY = (stage.BoundsMin.y + stage.BoundsMax.y) * 0.5f;
                    }
                    else
                    {
                        targetY = math.clamp(targetY, minY, maxY);
                    }
                }
            }

            Vector3 target = new Vector3(targetX, targetY, targetZ);
            _camera.transform.position = Vector3.SmoothDamp(current, target, ref _velocity, smoothTime);
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return true;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            _entityManager = world.EntityManager;
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _stageQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StageRuntimeConfig>());
            _initialized = true;
            return true;
        }
    }
}
