using System.Threading;
using Cysharp.Threading.Tasks;

namespace Architecture
{
    public enum LanguageOperationStatus
    {
        Success,
        NotInitialized,
        UnsupportedLanguage,
        LocaleMissing,
        InitializationFailed,
        ChangeFailed
    }

    public sealed record LanguageOperationResult(
        LanguageOperationStatus Status,
        string Error = null)
    {
        public bool IsSuccess => Status == LanguageOperationStatus.Success;

        public static LanguageOperationResult Success() => new(LanguageOperationStatus.Success);
        public static LanguageOperationResult Failure(LanguageOperationStatus status, string error) => new(status, error);
    }

    public interface ILanguageService
    {
        bool IsInitialized { get; }
        GameLanguageType CurrentLanguage { get; }

        UniTask<LanguageOperationResult> InitializeAsync(CancellationToken cancellationToken = default);
        UniTask<LanguageOperationResult> SetLanguageAsync(
            GameLanguageType language,
            CancellationToken cancellationToken = default);
    }
}
