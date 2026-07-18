using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class PageNavigator : IPageNavigator, IDisposable
    {
        private readonly IUiPrefabProvider _prefabProvider;
        private readonly PageStack _stack;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _shutdown = new();
        private bool _isShuttingDown;

        public PageNavigator(IUiPrefabProvider prefabProvider, PageStack stack)
        {
            _prefabProvider = prefabProvider;
            _stack = stack;
        }

        public int Count => _stack.Count;
        public IBasePage Top => _stack.Top;

        public async UniTask<NavigationResult<TPage>> PushAsync<TPage>(
            string addressableKey,
            CancellationToken cancellationToken = default) where TPage : MonoBehaviour, IBasePage
        {
            using var command = CreateCommandCancellation(cancellationToken);
            if (!await EnterGateAsync(command.Token))
            {
                return ShuttingDown<TPage>();
            }

            try
            {
                if (_stack.IsTop<TPage>(addressableKey, out var current))
                {
                    return new NavigationResult<TPage>(NavigationStatus.AlreadyCurrent, current);
                }

                var load = await _prefabProvider.LoadAsync(addressableKey, command.Token);
                if (!load.IsSuccess)
                {
                    return new NavigationResult<TPage>(NavigationStatus.LoadFailed, null, load.Error);
                }

                if (_isShuttingDown)
                {
                    load.Lease.Dispose();
                    return ShuttingDown<TPage>();
                }

                return await _stack.PushAsync<TPage>(load.Lease);
            }
            catch (OperationCanceledException) when (_isShuttingDown)
            {
                return ShuttingDown<TPage>();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask<NavigationResult<TPage>> ReplaceAsync<TPage>(
            string addressableKey,
            CancellationToken cancellationToken = default) where TPage : MonoBehaviour, IBasePage
        {
            using var command = CreateCommandCancellation(cancellationToken);
            if (!await EnterGateAsync(command.Token))
            {
                return ShuttingDown<TPage>();
            }

            try
            {
                if (_stack.IsTop<TPage>(addressableKey, out var current))
                {
                    return new NavigationResult<TPage>(NavigationStatus.AlreadyCurrent, current);
                }

                var load = await _prefabProvider.LoadAsync(addressableKey, command.Token);
                if (!load.IsSuccess)
                {
                    return new NavigationResult<TPage>(NavigationStatus.LoadFailed, null, load.Error);
                }

                if (_isShuttingDown)
                {
                    load.Lease.Dispose();
                    return ShuttingDown<TPage>();
                }

                return await _stack.ReplaceAsync<TPage>(load.Lease);
            }
            catch (OperationCanceledException) when (_isShuttingDown)
            {
                return ShuttingDown<TPage>();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask<NavigationResult> PopAsync(CancellationToken cancellationToken = default)
        {
            using var command = CreateCommandCancellation(cancellationToken);
            if (!await EnterGateAsync(command.Token))
            {
                return NavigationResult.Failure(NavigationStatus.ShuttingDown, "Navigator is shutting down.");
            }

            try
            {
                return _isShuttingDown
                    ? NavigationResult.Failure(NavigationStatus.ShuttingDown, "Navigator is shutting down.")
                    : await _stack.PopAsync();
            }
            catch (OperationCanceledException) when (_isShuttingDown)
            {
                return NavigationResult.Failure(NavigationStatus.ShuttingDown, "Navigator is shutting down.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async UniTask<NavigationResult> ClearAsync(CancellationToken cancellationToken = default)
        {
            using var command = CreateCommandCancellation(cancellationToken);
            if (!await EnterGateAsync(command.Token))
            {
                return NavigationResult.Failure(NavigationStatus.ShuttingDown, "Navigator is shutting down.");
            }

            try
            {
                return await _stack.ClearAsync();
            }
            catch (OperationCanceledException) when (_isShuttingDown)
            {
                return NavigationResult.Failure(NavigationStatus.ShuttingDown, "Navigator is shutting down.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _shutdown.Cancel();
            _stack.ClearImmediately();
        }

        private CancellationTokenSource CreateCommandCancellation(CancellationToken callerToken) =>
            CancellationTokenSource.CreateLinkedTokenSource(callerToken, _shutdown.Token);

        private async UniTask<bool> EnterGateAsync(CancellationToken cancellationToken)
        {
            if (_isShuttingDown)
            {
                return false;
            }

            try
            {
                await _gate.WaitAsync(cancellationToken);
                if (_isShuttingDown)
                {
                    _gate.Release();
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (_isShuttingDown)
            {
                return false;
            }
        }

        private static NavigationResult<TPage> ShuttingDown<TPage>()
            where TPage : MonoBehaviour, IBasePage =>
            new(NavigationStatus.ShuttingDown, null, "Navigator is shutting down.");
    }
}
