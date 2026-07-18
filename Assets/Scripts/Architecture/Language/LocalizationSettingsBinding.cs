using System;
using System.Threading;
using Architecture.Data.Settings;
using Cysharp.Threading.Tasks;

namespace Architecture
{
    public sealed class LocalizationSettingsBinding
    {
        private readonly ISettingsService _settings;
        private readonly ILanguageService _language;

        public LocalizationSettingsBinding(ISettingsService settings, ILanguageService language)
        {
            _settings = settings;
            _language = language;
        }

        public UniTask<LanguageOperationResult> ApplyCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "[LocalizationSettingsBinding] Settings must be initialized before applying locale.");
            }

            if (!_language.IsInitialized)
            {
                throw new InvalidOperationException(
                    "[LocalizationSettingsBinding] Language service must be initialized before applying locale.");
            }

            return _language.SetLanguageAsync(_settings.Current.Language, cancellationToken);
        }
    }
}
