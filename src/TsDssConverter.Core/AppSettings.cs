using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// Everything that is stored in settings.json. The first block is what the settings window shows;
/// the "advanced" block can only be changed by editing settings.json (no window for it).
/// "Start with Windows" is NOT here: that checkbox shows the real state of the registry.
/// </summary>
public class AppSettings
{
    // ---- Settings window ----
    public string TopSolidExportPath { get; set; } = @"Z:\TopSolid\Export\";
    public string BatchFolder { get; set; } = @"Z:\Duivestein\Batch";
    public string LabelFolder { get; set; } = @"Z:\Duivestein\Label";
    public bool FlipX { get; set; } = false;
    public bool FlipY { get; set; } = false;

    /// <summary>Language of the whole interface: "nl" (Dutch, the default), "fr" or "en".</summary>
    public string Language { get; set; } = "nl";

    // ---- Advanced: only in settings.json ----
    public string CncExtension { get; set; } = ".xcs";
    public string CncPathPrefixInXml { get; set; } = "";
    public string CsvEncoding { get; set; } = "utf-8";
    public int RescanSeconds { get; set; } = 30;
    public bool RequireTriggerFile { get; set; } = true;

    private const int MinRescanSeconds = 5;
    private const int MaxRescanSeconds = 3600;

    /// <summary>A separate copy, so the settings window can change values without touching the ones in use.</summary>
    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone(); // all values are numbers, booleans or text: a shallow copy is a full copy
    }

    /// <summary>The part of the settings that the conversion itself needs.</summary>
    public ConverterSettings ToConverterSettings()
    {
        return new ConverterSettings
        {
            FlipX = FlipX,
            FlipY = FlipY,
            TopSolidExportPath = TopSolidExportPath,
            CncExtension = CncExtension,
            CncPathPrefixInXml = CncPathPrefixInXml,
            CsvEncoding = CsvEncoding,
        };
    }

    /// <summary>
    /// Repairs values that would break the program (empty folder, "xcs" without dot, unknown encoding, ...).
    /// Returns a note (in the selected language) for every repair, so the user can be told. Nothing is thrown.
    /// </summary>
    public List<string> Normalize()
    {
        var defaults = new AppSettings();
        var notes = new List<string>();

        // A hand-edited settings.json can contain null or "" where text is expected.
        TopSolidExportPath = FixEmpty(TopSolidExportPath, defaults.TopSolidExportPath, nameof(TopSolidExportPath), notes);
        BatchFolder = FixEmpty(BatchFolder, defaults.BatchFolder, nameof(BatchFolder), notes);
        LabelFolder = FixEmpty(LabelFolder, defaults.LabelFolder, nameof(LabelFolder), notes);
        CncExtension = FixEmpty(CncExtension, defaults.CncExtension, nameof(CncExtension), notes);
        CsvEncoding = FixEmpty(CsvEncoding, defaults.CsvEncoding, nameof(CsvEncoding), notes);
        CncPathPrefixInXml ??= ""; // empty is allowed here: "not used"

        CncExtension = CncExtension.Trim();
        if (!CncExtension.StartsWith('.'))
        {
            CncExtension = "." + CncExtension;
        }

        if (RescanSeconds < MinRescanSeconds || RescanSeconds > MaxRescanSeconds)
        {
            notes.Add(Messages.SettingReset(nameof(RescanSeconds), Messages.ReasonOutOfRange(RescanSeconds, MinRescanSeconds, MaxRescanSeconds)));
            RescanSeconds = defaults.RescanSeconds;
        }

        if (!IsKnownEncoding(CsvEncoding))
        {
            notes.Add(Messages.SettingReset(nameof(CsvEncoding), Messages.ReasonUnknownEncoding(CsvEncoding)));
            CsvEncoding = defaults.CsvEncoding;
        }

        // "FR " and "fr" are both fine; store the clean code. Anything unknown becomes Dutch.
        if (Localizer.TryParse(Language, out AppLanguage language))
        {
            Language = Localizer.ToCode(language);
        }
        else
        {
            notes.Add(Messages.SettingReset(nameof(Language), Messages.ReasonUnknownLanguage(Language ?? "")));
            Language = defaults.Language;
        }

        return notes;
    }

    private static string FixEmpty(string? value, string defaultValue, string settingName, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            notes.Add(Messages.SettingReset(settingName, Messages.ReasonEmpty));
            return defaultValue;
        }

        return value.Trim();
    }

    private static bool IsKnownEncoding(string name)
    {
        try
        {
            new ConverterSettings { CsvEncoding = name }.GetCsvEncoding();
            return true;
        }
        catch (ArgumentException)
        {
            return false; // Encoding.GetEncoding does not know this name
        }
    }
}
