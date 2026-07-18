using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Architecture.Audio.Internal
{
    internal sealed class MusicPlayer : IMusicPlayer, IDisposable
    {
        private readonly IAudioClipStore _clipStore;
        private readonly IAudioOutput _output;
        private readonly CancellationTokenSource _shutdownCancellation = new();

        private CancellationTokenSource _commandCancellation;
        private MusicCueId? _currentCue;
        private int _generation;
        private bool _isShuttingDown;

        public MusicPlayer(IAudioClipStore clipStore, IAudioOutput output)
        {
            _clipStore = clipStore;
            _output = output;
        }

        public async UniTask<AudioPlayResult> PlayAsync(
            MusicCueId cueId,
            MusicTransition transition,
            CancellationToken cancellationToken = default)
        {
            if (_isShuttingDown)
            {
                return AudioPlayResult.ShuttingDown;
            }

            if (_currentCue.HasValue && _currentCue.Value.Equals(cueId) && _output.IsMusicPlaying)
            {
                return AudioPlayResult.Started;
            }

            var command = BeginCommand(cancellationToken);
            try
            {
                var loadResult = await _clipStore.LoadAsync(
                    new AudioCueKey(AudioCueKind.Music, cueId.Value),
                    command.Token);

                if (loadResult.Status != AudioClipLoadStatus.Loaded)
                {
                    return MapLoadResult(loadResult.Status, command.Generation, cancellationToken);
                }

                EnsureCurrent(command.Generation, command.Token);

                var halfDuration = transition.DurationSeconds * 0.5f;
                if (_output.IsMusicPlaying)
                {
                    await _output.FadeMusicTransitionGainAsync(0f, halfDuration, command.Token);
                    EnsureCurrent(command.Generation, command.Token);
                    _output.StopMusic();
                }

                _output.SetMusicTransitionGain(transition.DurationSeconds > 0f ? 0f : 1f);
                _output.StartMusic(loadResult.Clip);
                _currentCue = cueId;

                await _output.FadeMusicTransitionGainAsync(
                    loadResult.DefaultGain,
                    halfDuration,
                    command.Token);
                EnsureCurrent(command.Generation, command.Token);
                return AudioPlayResult.Started;
            }
            catch (OperationCanceledException)
            {
                return MapCancellation(command.Generation, cancellationToken);
            }
            finally
            {
                command.Dispose();
            }
        }

        public async UniTask<AudioPlayResult> StopAsync(
            MusicTransition transition,
            CancellationToken cancellationToken = default)
        {
            if (_isShuttingDown)
            {
                return AudioPlayResult.ShuttingDown;
            }

            var command = BeginCommand(cancellationToken);
            _currentCue = null;

            try
            {
                if (!_output.IsMusicPlaying)
                {
                    _output.SetMusicTransitionGain(1f);
                    return AudioPlayResult.Started;
                }

                await _output.FadeMusicTransitionGainAsync(
                    0f,
                    transition.DurationSeconds,
                    command.Token);
                EnsureCurrent(command.Generation, command.Token);

                _output.StopMusic();
                _output.SetMusicTransitionGain(1f);
                return AudioPlayResult.Started;
            }
            catch (OperationCanceledException)
            {
                return MapCancellation(command.Generation, cancellationToken);
            }
            finally
            {
                command.Dispose();
            }
        }

        public void Dispose()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _generation++;
            _shutdownCancellation.Cancel();
            _commandCancellation?.Cancel();
            _commandCancellation?.Dispose();
            _commandCancellation = null;
            _currentCue = null;
            _output.StopMusic();
            _shutdownCancellation.Dispose();
        }

        private CommandScope BeginCommand(CancellationToken callerCancellation)
        {
            _commandCancellation?.Cancel();
            _commandCancellation?.Dispose();
            _commandCancellation = new CancellationTokenSource();

            var generation = ++_generation;
            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellation,
                _shutdownCancellation.Token,
                _commandCancellation.Token);

            return new CommandScope(generation, linkedCancellation);
        }

        private void EnsureCurrent(int generation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private AudioPlayResult MapLoadResult(
            AudioClipLoadStatus status,
            int generation,
            CancellationToken callerCancellation)
        {
            return status switch
            {
                AudioClipLoadStatus.UnknownCue => AudioPlayResult.UnknownCue,
                AudioClipLoadStatus.LoadFailed => AudioPlayResult.LoadFailed,
                AudioClipLoadStatus.ShuttingDown => AudioPlayResult.ShuttingDown,
                AudioClipLoadStatus.Cancelled => MapCancellation(generation, callerCancellation),
                _ => AudioPlayResult.LoadFailed
            };
        }

        private AudioPlayResult MapCancellation(int generation, CancellationToken callerCancellation)
        {
            if (_isShuttingDown)
            {
                return AudioPlayResult.ShuttingDown;
            }

            if (generation != _generation)
            {
                return AudioPlayResult.Superseded;
            }

            return callerCancellation.IsCancellationRequested
                ? AudioPlayResult.Cancelled
                : AudioPlayResult.Superseded;
        }

        private readonly struct CommandScope : IDisposable
        {
            private readonly CancellationTokenSource _cancellation;

            public int Generation { get; }
            public CancellationToken Token => _cancellation.Token;

            public CommandScope(int generation, CancellationTokenSource cancellation)
            {
                Generation = generation;
                _cancellation = cancellation;
            }

            public void Dispose() => _cancellation.Dispose();
        }
    }
}
