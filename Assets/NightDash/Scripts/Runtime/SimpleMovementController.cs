using UnityEngine;
using UnityEngine.InputSystem;

namespace NightDash.Runtime
{
    // Simple WASD/arrow movement for preview scenes.
    // Uses New Input System (project default).
    [RequireComponent(typeof(SpriteRenderer))]
    public class SimpleMovementController : MonoBehaviour
    {
        public float moveSpeed = 4f;
        public Sprite[] walkFrames;
        public Sprite[] idleFrames;
        public float walkFps = 12f;
        public float idleFps = 6f;

        SpriteRenderer spriteRenderer;
        SpriteAnimator animator;
        bool wasMoving;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<SpriteAnimator>();
            if (animator == null) animator = gameObject.AddComponent<SpriteAnimator>();
        }

        void Start()
        {
            PlayIdle();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = 0f, v = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;

            Vector2 input = new Vector2(h, v);
            bool isMoving = input.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                Vector2 dir = input.normalized;
                transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);

                if (h > 0.01f) spriteRenderer.flipX = false;
                else if (h < -0.01f) spriteRenderer.flipX = true;

                if (!wasMoving) PlayWalk();
            }
            else
            {
                if (wasMoving) PlayIdle();
            }

            wasMoving = isMoving;
        }

        void PlayWalk()
        {
            if (walkFrames == null || walkFrames.Length == 0) return;
            animator.frames = walkFrames;
            animator.fps = walkFps;
            animator.loop = true;
            animator.Play();
        }

        void PlayIdle()
        {
            if (idleFrames == null || idleFrames.Length == 0) return;
            animator.frames = idleFrames;
            animator.fps = idleFps;
            animator.loop = true;
            animator.Play();
        }
    }
}
