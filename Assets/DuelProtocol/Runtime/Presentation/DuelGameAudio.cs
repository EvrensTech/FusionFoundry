using UnityEngine;

namespace DuelProtocol.Presentation
{
    public sealed class DuelGameAudio : MonoBehaviour
    {
        private static DuelGameAudio _instance;
        private AudioSource _source;
        private AudioClip _pulse;
        private AudioClip _hit;
        private AudioClip _core;
        private AudioClip _score;

        public static DuelGameAudio Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("Duel Gameplay Audio");
                _instance = go.AddComponent<DuelGameAudio>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = .18f;
            _pulse = Tone("Pulse", 115f, 520f, .26f, .46f, true);
            _hit = Tone("Impact", 180f, 55f, .2f, .62f, true);
            _core = Tone("Core", 440f, 880f, .38f, .35f, false);
            _score = Tone("Score", 392f, 1174f, .64f, .42f, false);
        }

        public void PlayPulse(Vector3 position) => Play(_pulse, position, .85f);
        public void PlayHit(Vector3 position) => Play(_hit, position, 1f);
        public void PlayCore(Vector3 position) => Play(_core, position, .8f);
        public void PlayScore() => Play(_score, Vector3.zero, .9f);

        private void Play(AudioClip clip, Vector3 position, float gain)
        {
            if (clip == null) return;
            transform.position = position;
            var settings = DuelPlayerPreferences.Current;
            _source.PlayOneShot(clip, settings.MasterVolume * settings.SfxVolume * gain);
        }

        private static AudioClip Tone(string name, float startHz, float endHz, float seconds, float gain, bool noise)
        {
            const int rate = 22050;
            var count = Mathf.CeilToInt(rate * seconds);
            var data = new float[count];
            var phase = 0f;
            uint seed = 0x9E3779B9u;
            for (var i = 0; i < count; i++)
            {
                var p = i / (float)count;
                var hz = Mathf.Lerp(startHz, endHz, p);
                phase += Mathf.PI * 2f * hz / rate;
                var envelope = Mathf.Pow(1f - p, 2.2f) * Mathf.Min(1f, p * 35f);
                seed = seed * 1664525u + 1013904223u;
                var n = ((seed >> 9) / 8388607f * 2f - 1f) * (noise ? .22f : .03f);
                data[i] = (Mathf.Sin(phase) * .78f + Mathf.Sin(phase * .5f) * .18f + n) * envelope * gain;
            }
            var clip = AudioClip.Create("Duel " + name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
