using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Architecture.Audio.Internal
{
    internal sealed class AddressableAudioClipStore : IAudioClipStore
    {
        private sealed class CacheEntry
        {
            public AsyncOperationHandle<AudioClip> Handle { get; }
            public UniTask<AudioClipLoadResult> LoadTask { get; set; }
            public bool IsReleased { get; private set; }

            public CacheEntry(AsyncOperationHandle<AudioClip> handle)
            {
                Handle = handle;
            }

            public void Release()
            {
                if (IsReleased)
                {
                    return;
                }

                IsReleased = true;
                if (Handle.IsValid())
                {
                    Addressables.Release(Handle);
                }
            }
        }

        private readonly AudioCatalog _catalog;
        private readonly Dictionary<AudioCueKey, CacheEntry> _entries = new();
        private readonly CancellationTokenSource _shutdownCancellation = new();
        private bool _isShuttingDown;

        public AddressableAudioClipStore(AudioCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public async UniTask<AudioClipLoadResult> LoadAsync(
            AudioCueKey cueKey,
            CancellationToken cancellationToken)
        {
            if (_isShuttingDown)
            {
                return AudioClipLoadResult.FromStatus(AudioClipLoadStatus.ShuttingDown);
            }

            if (!TryGetDefinition(cueKey, out var definition))
            {
                return AudioClipLoadResult.FromStatus(AudioClipLoadStatus.UnknownCue);
            }

            if (definition.ClipReference == null || !definition.ClipReference.RuntimeKeyIsValid())
            {
                Debug.LogError($"[AudioClipStore] Cue {cueKey} has no valid AudioClip reference.");
                return AudioClipLoadResult.FromStatus(AudioClipLoadStatus.LoadFailed);
            }

            if (!_entries.TryGetValue(cueKey, out var entry))
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(definition.ClipReference.RuntimeKey);
                entry = new CacheEntry(handle);
                _entries.Add(cueKey, entry);
                entry.LoadTask = CompleteLoadAsync(cueKey, definition, entry).Preserve();
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdownCancellation.Token);

            try
            {
                return await entry.LoadTask.AttachExternalCancellation(linkedCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return AudioClipLoadResult.FromStatus(
                    _isShuttingDown ? AudioClipLoadStatus.ShuttingDown : AudioClipLoadStatus.Cancelled);
            }
        }

        public void Dispose()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _shutdownCancellation.Cancel();

            foreach (var entry in _entries.Values)
            {
                entry.Release();
            }

            _entries.Clear();
            _shutdownCancellation.Dispose();
        }

        private async UniTask<AudioClipLoadResult> CompleteLoadAsync(
            AudioCueKey cueKey,
            AudioCueDefinition definition,
            CacheEntry entry)
        {
            try
            {
                var operationTask = entry.Handle.Task;
                await operationTask.AsUniTask();
            }
            catch (Exception exception)
            {
                if (!_isShuttingDown)
                {
                    Debug.LogError(
                        $"[AudioClipStore] Load failed for {cueKey}: {exception.Message}");
                }

                RemoveFailedEntry(cueKey, entry);
                return AudioClipLoadResult.FromStatus(
                    _isShuttingDown ? AudioClipLoadStatus.ShuttingDown : AudioClipLoadStatus.LoadFailed);
            }

            if (_isShuttingDown || entry.IsReleased)
            {
                return AudioClipLoadResult.FromStatus(AudioClipLoadStatus.ShuttingDown);
            }

            if (!entry.Handle.IsValid() ||
                entry.Handle.Status != AsyncOperationStatus.Succeeded ||
                entry.Handle.Result == null)
            {
                Debug.LogError($"[AudioClipStore] Load failed for {cueKey}.");
                RemoveFailedEntry(cueKey, entry);
                return AudioClipLoadResult.FromStatus(AudioClipLoadStatus.LoadFailed);
            }

            return AudioClipLoadResult.Loaded(entry.Handle.Result, definition.DefaultGain);
        }

        private bool TryGetDefinition(AudioCueKey cueKey, out AudioCueDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(cueKey.Value))
            {
                definition = null;
                return false;
            }

            return cueKey.Kind == AudioCueKind.Music
                ? _catalog.TryGet(new MusicCueId(cueKey.Value), out definition)
                : _catalog.TryGet(new SfxCueId(cueKey.Value), out definition);
        }

        private void RemoveFailedEntry(AudioCueKey cueKey, CacheEntry entry)
        {
            if (_entries.TryGetValue(cueKey, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(cueKey);
            }

            entry.Release();
        }
    }
}
