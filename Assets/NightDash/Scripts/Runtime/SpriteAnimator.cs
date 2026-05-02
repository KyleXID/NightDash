using UnityEngine;

namespace NightDash.Runtime
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;
        public bool playOnStart = true;

        SpriteRenderer spriteRenderer;
        int currentFrame;
        float timer;
        bool isPlaying;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            isPlaying = true;
            currentFrame = 0;
            timer = 0f;
            if (frames != null && frames.Length > 0 && spriteRenderer != null)
                spriteRenderer.sprite = frames[0];
        }

        public void Stop() { isPlaying = false; }

        void Update()
        {
            if (!isPlaying || frames == null || frames.Length == 0) return;

            timer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(0.0001f, fps);
            while (timer >= frameDuration)
            {
                timer -= frameDuration;
                currentFrame++;
                if (currentFrame >= frames.Length)
                {
                    if (loop) currentFrame = 0;
                    else { currentFrame = frames.Length - 1; isPlaying = false; }
                }
                spriteRenderer.sprite = frames[currentFrame];
            }
        }
    }
}
