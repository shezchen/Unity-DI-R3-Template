using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Architecture.Audio
{
    public readonly struct MusicCueId : IEquatable<MusicCueId>
    {
        public string Value { get; }

        public MusicCueId(string value) => Value = value;

        public bool Equals(MusicCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MusicCueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct SfxCueId : IEquatable<SfxCueId>
    {
        public string Value { get; }

        public SfxCueId(string value) => Value = value;

        public bool Equals(SfxCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SfxCueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct MusicTransition
    {
        public static MusicTransition Immediate => new MusicTransition(0f);
        public static MusicTransition Default => new MusicTransition(0.5f);

        public float DurationSeconds { get; }

        public MusicTransition(float durationSeconds)
        {
            DurationSeconds = Math.Max(0f, durationSeconds);
        }
    }

    public readonly struct AudioLevels
    {
        public float Music { get; }
        public float Sfx { get; }

        public AudioLevels(float music, float sfx)
        {
            Music = Clamp01(music);
            Sfx = Clamp01(sfx);
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public enum AudioPlayResult
    {
        Started,
        UnknownCue,
        LoadFailed,
        Superseded,
        Cancelled,
        ShuttingDown
    }

    public interface IMusicPlayer
    {
        UniTask<AudioPlayResult> PlayAsync(
            MusicCueId cueId,
            MusicTransition transition,
            CancellationToken cancellationToken = default);

        UniTask<AudioPlayResult> StopAsync(
            MusicTransition transition,
            CancellationToken cancellationToken = default);
    }

    public interface ISfxPlayer
    {
        UniTask<AudioPlayResult> PlayAsync(
            SfxCueId cueId,
            float gain = 1f,
            CancellationToken cancellationToken = default);
    }

    public interface IAudioLevelsControl
    {
        void Apply(AudioLevels levels);
    }
}
