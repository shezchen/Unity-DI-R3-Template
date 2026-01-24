using UnityEngine;

namespace Architecture.AudioProvider
{
    public interface ISfxAudioSourceProvider
    {
        AudioSource Source { get; }
    }

    public sealed class SfxAudioSourceProvider : ISfxAudioSourceProvider
    {
        public AudioSource Source { get; }

        public SfxAudioSourceProvider(AudioSource source)
        {
            Source = source;
        }
    }
}
