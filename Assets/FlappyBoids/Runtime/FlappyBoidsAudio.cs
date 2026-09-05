using UnityEngine;

namespace FlappyBoids
{
    public sealed class FlappyBoidsAudio : MonoBehaviour
    {
        [Header("Scene-authored audio")]
        [SerializeField] private AudioClip _underwaterAmbience;
        [SerializeField] private AudioSource _effectsSource;
        [SerializeField] private AudioSource _ambientSource;

        [Header("IdleTogether feedback clips")]
        [SerializeField] private AudioClip _flapClip;
        [SerializeField] private AudioClip[] _gateClips;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _finishClip;

        private int _nextGateClip;

        public bool HasAuthoredFeedback =>
            _effectsSource != null && _ambientSource != null && _underwaterAmbience != null &&
            _flapClip != null && _hitClip != null && _finishClip != null &&
            _gateClips != null && _gateClips.Length > 0;

        public void ConfigureSceneAudio(
            AudioClip underwaterAmbience,
            AudioSource effectsSource,
            AudioSource ambientSource,
            AudioClip flapClip,
            AudioClip[] gateClips,
            AudioClip hitClip,
            AudioClip finishClip)
        {
            _underwaterAmbience = underwaterAmbience;
            _effectsSource = effectsSource;
            _ambientSource = ambientSource;
            _flapClip = flapClip;
            _gateClips = gateClips;
            _hitClip = hitClip;
            _finishClip = finishClip;
        }

        private void Awake()
        {
            if (!HasAuthoredFeedback)
            {
                Debug.LogError(
                    "FlappyBoids audio is incomplete. Rebuild the authored scene to assign AudioSources and clips.",
                    this);
                return;
            }

            _effectsSource.playOnAwake = false;
            _effectsSource.spatialBlend = 0f;
            _effectsSource.volume = 0.46f;
            _ambientSource.clip = _underwaterAmbience;
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = true;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.volume = 0.16f;
        }

        public void PlayFlap()
        {
            Play(_flapClip, 0.48f, 1f);
        }

        public void PlayGate()
        {
            if (_gateClips == null || _gateClips.Length == 0) return;
            int index = _nextGateClip++ % _gateClips.Length;
            float pitch = 0.97f + index * (0.06f / Mathf.Max(1, _gateClips.Length - 1));
            Play(_gateClips[index], 0.92f, pitch);
        }

        public void PlayHit(int removed)
        {
            Play(_hitClip, Mathf.Clamp01(0.42f + removed * 0.07f), 0.94f);
        }

        public void PlayFinish(bool won)
        {
            Play(won ? _finishClip : _hitClip, 0.9f, won ? 1f : 0.88f);
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (_effectsSource == null || clip == null) return;
            _effectsSource.pitch = pitch;
            _effectsSource.PlayOneShot(clip, volume);
        }
    }
}
