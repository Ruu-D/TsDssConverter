namespace TsDssConverter.Core;

/// <summary>The three languages of the interface.</summary>
public enum AppLanguage
{
    Dutch,
    French,
    English,
}

/// <summary>
/// The language switch. Every text that a user can see is written in three languages next to each other,
/// for example  Localizer.T("Opslaan", "Enregistrer", "Save"),  and this class picks the one that is selected.
/// Dutch is the default (the customer is Flemish). The language is chosen in the settings window and stored
/// in settings.json as "nl", "fr" or "en".
/// </summary>
public static class Localizer
{
    // volatile: the tray thread changes it, the conversion thread reads it.
    private static volatile AppLanguage _current = AppLanguage.Dutch;

    public static AppLanguage Current
    {
        get => _current;
        set => _current = value;
    }

    /// <summary>The text in the selected language.</summary>
    public static string T(string dutch, string french, string english)
    {
        return _current switch
        {
            AppLanguage.French => french,
            AppLanguage.English => english,
            _ => dutch,
        };
    }

    /// <summary>"nl", "fr" or "en": how the language is written in settings.json.</summary>
    public static string ToCode(AppLanguage language)
    {
        return language switch
        {
            AppLanguage.French => "fr",
            AppLanguage.English => "en",
            _ => "nl",
        };
    }

    /// <summary>Reads "nl", "fr" or "en" (any case, spaces allowed). Returns false for anything else.</summary>
    public static bool TryParse(string? code, out AppLanguage language)
    {
        switch (code?.Trim().ToLowerInvariant())
        {
            case "nl":
                language = AppLanguage.Dutch;
                return true;
            case "fr":
                language = AppLanguage.French;
                return true;
            case "en":
                language = AppLanguage.English;
                return true;
            default:
                language = AppLanguage.Dutch;
                return false;
        }
    }

    /// <summary>Like TryParse, but gives Dutch for an unknown code.</summary>
    public static AppLanguage FromCode(string? code)
    {
        TryParse(code, out AppLanguage language);
        return language;
    }
}
