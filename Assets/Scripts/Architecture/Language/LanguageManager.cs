using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Architecture
{
    /// <summary>
    /// Unity Localization adapter. Owns only the currently applied locale state.
    /// </summary>
    public sealed class LanguageManager : ILanguageService
    {
        private const string CodeEn = "en";
        private const string CodeZh = "zh-Hans";
        private const string CodeJa = "ja";

        public bool IsInitialized { get; private set; }
        public GameLanguageType CurrentLanguage { get; private set; } = GameLanguageType.English;

        public async UniTask<LanguageOperationResult> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
            {
                return LanguageOperationResult.Success();
            }

            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(
                    cancellationToken: cancellationToken);
                var selectedLocale = LocalizationSettings.SelectedLocale;
                if (selectedLocale == null || !TryGetLanguage(selectedLocale.Identifier.Code, out var language))
                {
                    return LanguageOperationResult.Failure(
                        LanguageOperationStatus.InitializationFailed,
                        $"Selected locale '{selectedLocale?.Identifier.Code ?? "null"}' is unsupported.");
                }

                CurrentLanguage = language;
                IsInitialized = true;
                return LanguageOperationResult.Success();
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    throw;
                }

                return LanguageOperationResult.Failure(
                    LanguageOperationStatus.InitializationFailed,
                    exception.Message);
            }
        }

        public async UniTask<LanguageOperationResult> SetLanguageAsync(
            GameLanguageType language,
            CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                return LanguageOperationResult.Failure(
                    LanguageOperationStatus.NotInitialized,
                    "Language service has not been initialized.");
            }

            if (!TryGetLocaleCode(language, out var code))
            {
                return LanguageOperationResult.Failure(
                    LanguageOperationStatus.UnsupportedLanguage,
                    $"Language '{language}' is unsupported.");
            }

            if (CurrentLanguage == language)
            {
                return LanguageOperationResult.Success();
            }

            try
            {
                Locale targetLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (targetLocale == null)
                {
                    return LanguageOperationResult.Failure(
                        LanguageOperationStatus.LocaleMissing,
                        $"Locale '{code}' is not configured.");
                }

                LocalizationSettings.SelectedLocale = targetLocale;
                await UniTask.Yield(cancellationToken);
                CurrentLanguage = language;
                return LanguageOperationResult.Success();
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    throw;
                }

                return LanguageOperationResult.Failure(
                    LanguageOperationStatus.ChangeFailed,
                    exception.Message);
            }
        }

        private static bool TryGetLocaleCode(GameLanguageType language, out string code)
        {
            code = language switch
            {
                GameLanguageType.Chinese => CodeZh,
                GameLanguageType.English => CodeEn,
                GameLanguageType.Japanese => CodeJa,
                _ => null
            };
            return code != null;
        }

        private static bool TryGetLanguage(string code, out GameLanguageType language)
        {
            switch (code)
            {
                case CodeZh:
                    language = GameLanguageType.Chinese;
                    return true;
                case CodeEn:
                    language = GameLanguageType.English;
                    return true;
                case CodeJa:
                    language = GameLanguageType.Japanese;
                    return true;
                default:
                    language = default;
                    return false;
            }
        }
    }
}
