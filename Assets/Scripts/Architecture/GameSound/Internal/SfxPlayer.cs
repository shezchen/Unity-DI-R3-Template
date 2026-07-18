using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Architecture.Audio.Internal
{
    internal sealed class SfxPlayer : ISfxPlayer, IDisposable
    {
        private readonly IAudioClipStore _clipStore;
        private readonly IAudioOutput _output;
        private readonly CancellationTokenSource _shutdownCancellation = new();
        private bool _isShuttingDown;

        public SfxPlayer(IAudioClipStore clipStore, IAudioOutput output)
        {
            _clipStore = clipStore;
            _output = output;
        }

        public async UniTask<AudioPlayResult> PlayAsync(
            SfxCueId cueId,
            float gain = 1f,
            CancellationToken cancellationToken = default)
        {
            if (_isShuttingDown)
            {
                return AudioPlayResult.ShuttingDown;
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdownCancellation.Token);

            var loadResult = await _clipStore.LoadAsync(
                new AudioCueKey(AudioCueKind.Sfx, cueId.Value),
                linkedCancellation.Token);

            if (loadResult.Status != AudioClipLoadStatus.Loaded)
            {
                return MapLoadResult(loadResult.Status, cancellationToken);
            }

            if (_isShuttingDown)
            {
                return AudioPlayResult.ShuttingDown;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return AudioPlayResult.Cancelled;
            }

            _output.PlaySfx(loadResult.Clip, gain * loadResult.DefaultGain);
            return AudioPlayResult.Started;
        }

        public void Dispose()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _shutdownCancellation.Cancel();
            _shutdownCancellation.Dispose();
        }

        private AudioPlayResult MapLoadResult(
            AudioClipLoadStatus status,
            CancellationToken callerCancellation)
        {
            return status switch
            {
                AudioClipLoadStatus.UnknownCue => AudioPlayResult.UnknownCue,
                AudioClipLoadStatus.LoadFailed => AudioPlayResult.LoadFailed,
                AudioClipLoadStatus.ShuttingDown => AudioPlayResult.ShuttingDown,
                AudioClipLoadStatus.Cancelled when callerCancellation.IsCancellationRequested =>
                    AudioPlayResult.Cancelled,
                AudioClipLoadStatus.Cancelled => AudioPlayResult.ShuttingDown,
                _ => AudioPlayResult.LoadFailed
            };
        }
    }
}
