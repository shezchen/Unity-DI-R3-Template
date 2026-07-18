using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UI
{
    public sealed class UiPrefabLease : IDisposable
    {
        private AsyncOperationHandle<GameObject> _handle;
        private bool _isDisposed;

        internal UiPrefabLease(string key, AsyncOperationHandle<GameObject> handle)
        {
            Key = key;
            _handle = handle;
            Prefab = handle.Result;
        }

        public string Key { get; }
        public GameObject Prefab { get; }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }
        }
    }

    public sealed record UiPrefabLoadResult(UiPrefabLease Lease, string Error = null)
    {
        public bool IsSuccess => Lease != null;
    }

    public interface IUiPrefabProvider
    {
        UniTask<UiPrefabLoadResult> LoadAsync(
            string addressableKey,
            CancellationToken cancellationToken = default);
    }

    public sealed class AddressableUiPrefabProvider : IUiPrefabProvider
    {
        public async UniTask<UiPrefabLoadResult> LoadAsync(
            string addressableKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(addressableKey))
            {
                return new UiPrefabLoadResult(null, "Addressable key is empty.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);

            try
            {
                var prefab = await handle.ToUniTask(cancellationToken: cancellationToken);
                if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded || prefab == null)
                {
                    Release(handle);
                    return new UiPrefabLoadResult(null, $"Addressable '{addressableKey}' did not load a prefab.");
                }

                return new UiPrefabLoadResult(new UiPrefabLease(addressableKey, handle));
            }
            catch (OperationCanceledException)
            {
                Release(handle);
                throw;
            }
            catch (Exception exception)
            {
                Release(handle);
                return new UiPrefabLoadResult(null, exception.Message);
            }
        }

        private static void Release(AsyncOperationHandle<GameObject> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}
