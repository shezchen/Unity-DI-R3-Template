using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI
{
    public sealed class PageStack
    {
        private sealed class PageEntry
        {
            private readonly CancellationTokenSource _lifetime = new();
            private bool _isDisposed;

            public PageEntry(GameObject gameObject, IBasePage page, UiPrefabLease prefabLease)
            {
                GameObject = gameObject;
                Page = page;
                PrefabLease = prefabLease;
            }

            public GameObject GameObject { get; }
            public IBasePage Page { get; }
            public UiPrefabLease PrefabLease { get; }
            public string Key => PrefabLease.Key;
            public CancellationToken LifetimeToken => _lifetime.Token;

            public void DisposeImmediately()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _lifetime.Cancel();

                if (GameObject != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(GameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(GameObject);
                    }
                }

                PrefabLease.Dispose();
                _lifetime.Dispose();
            }
        }

        private readonly List<PageEntry> _entries = new();
        private readonly IObjectResolver _resolver;
        private readonly Transform _uiRoot;

        public PageStack(IObjectResolver resolver, UIRoot uiRoot)
        {
            _resolver = resolver;
            _uiRoot = uiRoot.transform;
        }

        public int Count => _entries.Count;
        public IBasePage Top => Count == 0 ? null : _entries[Count - 1].Page;

        public bool IsTop<TPage>(string key, out TPage page) where TPage : MonoBehaviour, IBasePage
        {
            if (Count > 0 &&
                _entries[Count - 1].Key == key &&
                _entries[Count - 1].Page is TPage typedPage)
            {
                page = typedPage;
                return true;
            }

            page = null;
            return false;
        }

        public async UniTask<NavigationResult<TPage>> PushAsync<TPage>(UiPrefabLease prefabLease)
            where TPage : MonoBehaviour, IBasePage
        {
            var creation = CreateEntry<TPage>(prefabLease);
            if (creation.Entry == null)
            {
                return new NavigationResult<TPage>(creation.Status, null, creation.Error);
            }

            var entry = creation.Entry;
            var previous = Count > 0 ? _entries[Count - 1] : null;

            if (previous != null)
            {
                var pauseError = await InvokeLifecycleAsync(
                    () => previous.Page.OnPause(previous.LifetimeToken),
                    "pause current page");
                if (pauseError != null)
                {
                    entry.DisposeImmediately();
                    await TryResumeAsync(previous);
                    return new NavigationResult<TPage>(NavigationStatus.LifecycleFailed, null, pauseError);
                }
            }

            _entries.Add(entry);
            entry.GameObject.SetActive(true);

            var enterError = await InvokeLifecycleAsync(
                () => entry.Page.OnEnter(entry.LifetimeToken),
                "enter new page");
            if (enterError != null)
            {
                _entries.Remove(entry);
                entry.DisposeImmediately();
                var resumeError = await TryResumeAsync(previous);
                return new NavigationResult<TPage>(
                    NavigationStatus.LifecycleFailed,
                    null,
                    CombineErrors(enterError, resumeError));
            }

            return new NavigationResult<TPage>(NavigationStatus.Succeeded, (TPage)entry.Page);
        }

        public async UniTask<NavigationResult<TPage>> ReplaceAsync<TPage>(UiPrefabLease prefabLease)
            where TPage : MonoBehaviour, IBasePage
        {
            if (Count == 0)
            {
                return await PushAsync<TPage>(prefabLease);
            }

            var creation = CreateEntry<TPage>(prefabLease);
            if (creation.Entry == null)
            {
                return new NavigationResult<TPage>(creation.Status, null, creation.Error);
            }

            var entry = creation.Entry;
            var previous = _entries[Count - 1];
            var pauseError = await InvokeLifecycleAsync(
                () => previous.Page.OnPause(previous.LifetimeToken),
                "pause replaced page");
            if (pauseError != null)
            {
                entry.DisposeImmediately();
                await TryResumeAsync(previous);
                return new NavigationResult<TPage>(NavigationStatus.LifecycleFailed, null, pauseError);
            }

            _entries.Add(entry);
            entry.GameObject.SetActive(true);
            var enterError = await InvokeLifecycleAsync(
                () => entry.Page.OnEnter(entry.LifetimeToken),
                "enter replacement page");
            if (enterError != null)
            {
                _entries.Remove(entry);
                entry.DisposeImmediately();
                var resumeError = await TryResumeAsync(previous);
                return new NavigationResult<TPage>(
                    NavigationStatus.LifecycleFailed,
                    null,
                    CombineErrors(enterError, resumeError));
            }

            _entries.Remove(previous);
            var exitError = await InvokeLifecycleAsync(
                () => previous.Page.OnExit(previous.LifetimeToken),
                "exit replaced page");
            previous.DisposeImmediately();
            if (exitError != null)
            {
                Debug.LogError($"[PageStack] Replacement succeeded, but old page cleanup failed: {exitError}");
            }

            return new NavigationResult<TPage>(NavigationStatus.Succeeded, (TPage)entry.Page);
        }

        public async UniTask<NavigationResult> PopAsync()
        {
            if (Count == 0)
            {
                return NavigationResult.Failure(NavigationStatus.Empty, "Page stack is empty.");
            }

            var top = _entries[Count - 1];
            _entries.RemoveAt(Count - 1);
            var exitError = await InvokeLifecycleAsync(
                () => top.Page.OnExit(top.LifetimeToken),
                "exit top page");
            top.DisposeImmediately();

            var resumeError = Count > 0 ? await TryResumeAsync(_entries[Count - 1]) : null;
            var error = CombineErrors(exitError, resumeError);
            return error == null
                ? NavigationResult.Success()
                : NavigationResult.Failure(NavigationStatus.LifecycleFailed, error);
        }

        public async UniTask<NavigationResult> ClearAsync()
        {
            string error = null;
            while (Count > 0)
            {
                var top = _entries[Count - 1];
                _entries.RemoveAt(Count - 1);
                error = CombineErrors(
                    error,
                    await InvokeLifecycleAsync(
                        () => top.Page.OnExit(top.LifetimeToken),
                        "exit page during clear"));
                top.DisposeImmediately();
            }

            return error == null
                ? NavigationResult.Success()
                : NavigationResult.Failure(NavigationStatus.LifecycleFailed, error);
        }

        public void ClearImmediately()
        {
            for (var index = _entries.Count - 1; index >= 0; index--)
            {
                _entries[index].DisposeImmediately();
            }

            _entries.Clear();
        }

        private (PageEntry Entry, NavigationStatus Status, string Error) CreateEntry<TPage>(
            UiPrefabLease prefabLease) where TPage : MonoBehaviour, IBasePage
        {
            GameObject instance = null;
            try
            {
                instance = _resolver.Instantiate(prefabLease.Prefab, _uiRoot);
                instance.SetActive(false);
                var page = instance.GetComponent<TPage>();
                if (page == null)
                {
                    DestroyUnownedInstance(instance);
                    prefabLease.Dispose();
                    return (null, NavigationStatus.InvalidPage,
                        $"Prefab '{prefabLease.Key}' does not contain {typeof(TPage).Name}.");
                }

                return (new PageEntry(instance, page, prefabLease), NavigationStatus.Succeeded, null);
            }
            catch (Exception exception)
            {
                DestroyUnownedInstance(instance);
                prefabLease.Dispose();
                return (null, NavigationStatus.LifecycleFailed, exception.Message);
            }
        }

        private static async UniTask<string> TryResumeAsync(PageEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return await InvokeLifecycleAsync(
                () => entry.Page.OnResume(entry.LifetimeToken),
                "resume previous page");
        }

        private static async UniTask<string> InvokeLifecycleAsync(
            Func<UniTask> operation,
            string operationName)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception exception)
            {
                return $"Failed to {operationName}: {exception.Message}";
            }
        }

        private static string CombineErrors(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return first + " | " + second;
        }

        private static void DestroyUnownedInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
