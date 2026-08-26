using UnityEngine;

namespace FlappyBoids
{
    public sealed class FlappyBoidsAudio : MonoBehaviour
    {
        [Header("Scene-authored audio")]
        [SerializeField] private AudioClip _underwaterAmbience;
        [SerializeField] private AudioSource _effectsSource;
        [SerializeField] private AudioSource _ambientSource;

        private AudioClip _flap;
        private AudioClip _gate;
        private AudioClip _hit;
        private AudioClip _win;

        public void ConfigureSceneAudio(
            AudioClip underwaterAmbience, AudioSource effectsSource, AudioSource ambientSource)
        {
            _underwaterAmbience = underwaterAmbience;
            _effectsSource = effectsSource;
            _ambientSource = ambientSource;
        }

        private void Awake()
        {
            if (_effectsSource == null) _effectsSource = gameObject.AddComponent<AudioSource>();
            if (_ambientSource == null) _ambientSource = gameObject.AddComponent<AudioSource>();
            _effectsSource.playOnAwake = false;
            _effectsSource.volume = 0.32f;
            _flap = Tone("Flap", 430f, 680f, 0.10f);
            _gate = Tone("Gate", 620f, 900f, 0.12f);
            _hit = Tone("Hit", 170f, 75f, 0.13f);
            _win = Tone("Finish", 520f, 1050f, 0.34f);

            if (_underwaterAmbience != null)
            {
                _ambientSource.clip = _underwaterAmbience;
                _ambientSource.loop = true;
                _ambientSource.playOnAwake = true;
                _ambientSource.spatialBlend = 0f;
                _ambientSource.volume = 0.16f;
                _ambientSource.Play();
            }
        }

        public void PlayFlap() => _effectsSource.PlayOneShot(_flap, 0.7f);
        public void PlayGate() => _effectsSource.PlayOneShot(_gate, 0.8f);
        public void PlayHit(int removed) => _effectsSource.PlayOneShot(_hit, Mathf.Clamp01(0.4f + removed * 0.08f));
        public void PlayFinish(bool won) => _effectsSource.PlayOneShot(won ? _win : _hit, 1f);

        private static AudioClip Tone(string name, float startFrequency, float endFrequency, float duration)
        {
            const int sampleRate = 22050;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)length;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, time);
                phase += frequency * Mathf.PI * 2f / sampleRate;
                float envelope = Mathf.Sin(time * Mathf.PI) * (1f - time * 0.45f);
                samples[i] = Mathf.Sin(phase) * envelope * 0.34f;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
