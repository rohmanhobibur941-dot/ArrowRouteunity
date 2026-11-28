using UnityEngine;

namespace ArrowRoute
{
    /// <summary>Sounds and vibration. The sounds are generated in code (simple tones), so no audio files are needed.</summary>
    public sealed class Fx : MonoBehaviour
    {
        AudioSource src;
        AudioClip pop, error, win;

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            pop = Tone("pop", new[] { 900f, 1200f }, 0.07f, 0.35f);
            error = Tone("error", new[] { 220f, 160f }, 0.18f, 0.40f);
            win = Tone("win", new[] { 523f, 659f, 784f, 1047f }, 0.45f, 0.35f);
        }

        static AudioClip Tone(string name, float[] freqs, float seconds, float volume)
        {
            const int rate = 44100;
            int n = (int)(rate * seconds);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                int step = Mathf.Min(freqs.Length - 1, (int)(t / seconds * freqs.Length));
                float env = Mathf.Clamp01(1f - t / seconds);
                data[i] = Mathf.Sin(2f * Mathf.PI * freqs[step] * t) * env * volume;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip c)
        {
            if (Prefs.Sound && c != null) src.PlayOneShot(c);
        }

        public void Pop() { Play(pop); }

        public void Error()
        {
            Play(error);
            Buzz();
        }

        public void Win()
        {
            Play(win);
            Buzz();
        }

        static void Buzz()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Prefs.Vibrate) Handheld.Vibrate();
#endif
        }
    }
}
