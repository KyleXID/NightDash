using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    // Per-archetype sprite animation set. id matches:
    //   - Player class: RunSelection.ClassId  (e.g. "class_warrior")
    //   - Enemy:        EnemyArchetypeData.Id (e.g. "ghoul_scout")
    //   - Boss:         hard-coded id (e.g. "boss_agron")
    //
    // Authoring workflow: drop frame PNGs into folders, then run
    // NightDash/Generate Animation SOs to create/refresh the asset.
    [CreateAssetMenu(menuName = "NightDash/Data/Sprite Animation Set", fileName = "anim_")]
    public sealed class SpriteAnimationSetSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;

        [Header("Display")]
        [Tooltip("Uniform render scale applied by the Bridge. 1 = no scale.")]
        public float renderScale = 1f;

        [Tooltip("Source art faces left when true; Bridge will flipX to face right when moving right.")]
        public bool sourceFacesLeft = true;

        [Header("Clips")]
        public List<AnimationClipDef> clips = new();

        public bool TryGetClip(string clipName, out AnimationClipDef clip)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null &&
                    string.Equals(clips[i].name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    clip = clips[i];
                    return true;
                }
            }

            clip = null;
            return false;
        }

        public AnimationClipDef GetClipOrFallback(string preferred, string fallback)
        {
            if (TryGetClip(preferred, out var primary)) return primary;
            if (TryGetClip(fallback, out var alt)) return alt;
            return clips.Count > 0 ? clips[0] : null;
        }
    }

    [System.Serializable]
    public sealed class AnimationClipDef
    {
        public string name = "Walk";
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;

        public int FrameCount => frames != null ? frames.Length : 0;

        // Pure frame-index math. Safe for t<0, fps<=0, empty frames.
        // Static so EditMode tests can verify without instantiating SO.
        public static int ComputeFrameIndex(float time, int frameCount, float fps, bool loop)
        {
            if (frameCount <= 0) return 0;
            if (fps <= 0f) return 0;

            int idx = Mathf.FloorToInt(time * fps);
            if (loop)
            {
                int mod = idx % frameCount;
                return mod < 0 ? mod + frameCount : mod;
            }

            return Mathf.Clamp(idx, 0, frameCount - 1);
        }

        public Sprite GetFrameAt(float time)
        {
            int count = FrameCount;
            if (count == 0) return null;
            int idx = ComputeFrameIndex(time, count, fps, loop);
            return frames[idx];
        }
    }
}
