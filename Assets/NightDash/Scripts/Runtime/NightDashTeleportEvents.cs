// Sprint B / M3 — One-shot teleport event hook.
// PlayerMovementSystem fires this when a caster (mage / astrologer) dashes
// because the burst happens in a single ECS frame and the regular dash
// trail (sample on a 0.04s cadence) only sees one position. The trail
// bridge subscribes here to spawn a fan of afterimages along the path.

using UnityEngine;

namespace NightDash.Runtime
{
    public static class NightDashTeleportEvents
    {
        // (startWorldPos, endWorldPos) — both float3 ECS world coordinates.
        // Subscribers run on the next MonoBehaviour Update so they can hit
        // the visual bridge for the player's current sprite.
        public static System.Action<Vector3, Vector3> OnTeleport;

        public static void Fire(Unity.Mathematics.float3 start, Unity.Mathematics.float3 end)
        {
            OnTeleport?.Invoke(new Vector3(start.x, start.y, start.z),
                               new Vector3(end.x, end.y, end.z));
        }
    }
}
