using UnityEngine;

namespace NightDash.Runtime
{
    // Simple camera follow for preview scenes (no ECS dependency).
    public class SimpleCameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;
        public Vector3 offset = new Vector3(0, 0, -10);

        Vector3 _velocity;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
