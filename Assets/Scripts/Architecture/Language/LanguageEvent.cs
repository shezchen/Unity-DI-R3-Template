namespace Architecture
{
    public record LanguageChangeEvent(GameLanguageType NewLanguage);
    public record LanguageConfirmEvent(GameLanguageType ConfirmedLanguage);
}