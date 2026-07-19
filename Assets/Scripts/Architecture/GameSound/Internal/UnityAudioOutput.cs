using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace Architecture.Audio.Internal
{
    internal sealed class AudioOutputConfiguration
    {
        public AudioSource MusicSource { get; }
        public AudioSource SfxSource { get; }
        public AudioMixer Mixer { get; }
        public string MusicVolumeParameter { get; }
        public string SfxVolumeParameter { get; }

        public AudioOutputConfiguration(
            AudioSource musicSource,
            AudioSource sfxSource,
            AudioMixer mixer,
            string musicVolumeParameter,
            string sfxVolumeParameter)
        {
            MusicSource = musicSource;
            SfxSource = sfxSource;
            Mixer = mixer;
            MusicVolumeParameter = musicVolumeParameter;
            SfxVolumeParameter = sfxVolumeParameter;
        }
    }

    internal interface IAudioOutput
    {
        bool IsMusicPlaying { get; }

        void SetMusicTransitionGain(float gain);
        UniTask FadeMusicTransitionGainAsync(float gain, float duration, CancellationToken cancellationToken);
        void StartMusic(AudioClip clip);
        void StopMusic();
        void PlaySfx(AudioClip clip, float gain);
    }

    internal sealed class UnityAudioOutput :
        IAudioOutput,
        IAudioLevelsControl,
        IDisposable
    {
        private const float MinimumDecibels = -80f;

        private readonly AudioSource _musicSource;
        private readonly AudioSource _sfxSource;
        private readonly AudioMixer _mixer;
        private readonly string _musicVolumeParameter;
        private readonly string _sfxVolumeParameter;

        private Tween _musicFade;
        private float _musicTransitionGain = 1f;
        private AudioLevels _levels = new AudioLevels(1f, 1f);
        private bool _isDisposed;

        public bool IsMusicPlaying => !_isDisposed && _musicSource != null && _musicSource.isPlaying;

        public UnityAudioOutput(AudioOutputConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _musicSource = configuration.MusicSource != null
                ? configuration.MusicSource
                : throw new ArgumentNullException(nameof(configuration.MusicSource));
            _sfxSource = configuration.SfxSource != null
                ? configuration.SfxSource
                : throw new ArgumentNullException(nameof(configuration.SfxSource));
            _mixer = configuration.Mixer;
            _musicVolumeParameter = configuration.MusicVolumeParameter;
            _sfxVolumeParameter = configuration.SfxVolumeParameter;

            _musicSource.loop = true;
            ApplySourceGains();
        }

        public void Apply(AudioLevels levels)
        {
            ThrowIfDisposed();
            _levels = levels;

            if (_mixer != null)
            {
                SetMixerVolume(_musicVolumeParameter, levels.Music);
                SetMixerVolume(_sfxVolumeParameter, levels.Sfx);
            }

            ApplySourceGains();
        }

        public void SetMusicTransitionGain(float gain)
        {
            ThrowIfDisposed();
            _musicFade?.Kill();
            _musicFade = null;
            _musicTransitionGain = Mathf.Clamp01(gain);
            ApplySourceGains();
        }

        public async UniTask FadeMusicTransitionGainAsync(
            float gain,
            float duration,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            _musicFade?.Kill();
            _musicFade = null;

            var targetGain = Mathf.Clamp01(gain);
            if (duration <= 0f)
            {
                _musicTransitionGain = targetGain;
                ApplySourceGains();
                return;
            }

            var tween = DOTween.To(
                    () => _musicTransitionGain,
                    value =>
                    {
                        _musicTransitionGain = value;
                        ApplySourceGains();
                    },
                    targetGain,
                    duration)
                .SetUpdate(true);

            _musicFade = tween;
            await using var registration = cancellationToken.Register(() => tween.Kill(false));

            try
            {
                await tween.AsyncWaitForCompletion();
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                if (ReferenceEquals(_musicFade, tween))
                {
                    _musicFade = null;
                }
            }
        }

        public void StartMusic(AudioClip clip)
        {
            ThrowIfDisposed();
            _musicSource.clip = clip != null ? clip : throw new ArgumentNullException(nameof(clip));
            _musicSource.loop = true;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_isDisposed)
            {
                return;
            }

            _musicFade?.Kill();
            _musicFade = null;
            _musicSource.Stop();
            _musicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip, float gain)
        {
            ThrowIfDisposed();
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            _sfxSource.PlayOneShot(clip, Mathf.Max(0f, gain));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            StopMusic();
            _sfxSource.Stop();
            _isDisposed = true;
        }

        private void ApplySourceGains()
        {
            var hasMixer = _mixer != null;
            _musicSource.volume = _musicTransitionGain * (hasMixer ? 1f : _levels.Music);
            _sfxSource.volume = hasMixer ? 1f : _levels.Sfx;
        }

        private void SetMixerVolume(string parameter, float normalizedVolume)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                Debug.LogError("[AudioOutput] AudioMixer volume parameter is empty.");
                return;
            }

            var decibels = normalizedVolume <= 0f
                ? MinimumDecibels
                : Mathf.Log10(normalizedVolume) * 20f;

            if (!_mixer.SetFloat(parameter, decibels))
            {
                Debug.LogError($"[AudioOutput] AudioMixer parameter '{parameter}' was not found or exposed.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(UnityAudioOutput));
            }
        }
    }
}
