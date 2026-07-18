using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public enum NavigationStatus
    {
        Succeeded,
        AlreadyCurrent,
        Empty,
        LoadFailed,
        InvalidPage,
        LifecycleFailed,
        ShuttingDown
    }

    public sealed record NavigationResult(
        NavigationStatus Status,
        string Error = null)
    {
        public bool IsSuccess => Status is NavigationStatus.Succeeded or NavigationStatus.AlreadyCurrent;

        public static NavigationResult Success() => new(NavigationStatus.Succeeded);
        public static NavigationResult AlreadyCurrent() => new(NavigationStatus.AlreadyCurrent);
        public static NavigationResult Failure(NavigationStatus status, string error) => new(status, error);
    }

    public sealed record NavigationResult<TPage>(
        NavigationStatus Status,
        TPage Page = default,
        string Error = null) where TPage : MonoBehaviour, IBasePage
    {
        public bool IsSuccess => Status is NavigationStatus.Succeeded or NavigationStatus.AlreadyCurrent;
    }

    public interface IPageNavigator
    {
        int Count { get; }
        IBasePage Top { get; }

        UniTask<NavigationResult<TPage>> PushAsync<TPage>(
            string addressableKey,
            CancellationToken cancellationToken = default) where TPage : MonoBehaviour, IBasePage;

        UniTask<NavigationResult<TPage>> ReplaceAsync<TPage>(
            string addressableKey,
            CancellationToken cancellationToken = default) where TPage : MonoBehaviour, IBasePage;

        UniTask<NavigationResult> PopAsync(CancellationToken cancellationToken = default);
        UniTask<NavigationResult> ClearAsync(CancellationToken cancellationToken = default);
    }
}
