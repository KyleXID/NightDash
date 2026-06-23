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
        // When looping, the frame to jump back to after the FIRST full play-through.
        // 0 = loop the whole clip (default). >0 = play 0..end once as an intro, then
        // loop loopStartFrame..end (e.g. an eruption that settles into a writhe).
        public int loopStartFrame = 0;

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
                    if (loop) currentFrame = Mathf.Clamp(loopStartFrame, 0, frames.Length - 1); // intro plays once, then loop the tail
                    else { currentFrame = frames.Length - 1; isPlaying = false; }
                }
                spriteRenderer.sprite = frames[currentFrame];
            }
        }
    }
}
