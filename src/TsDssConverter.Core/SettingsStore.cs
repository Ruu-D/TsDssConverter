using System.Text;
using System.Text.Json;

namespace TsDssConverter.Core;

/// <summary>The settings that were loaded, plus everything the user should be told about.</summary>
public class SettingsLoadResult
{
    public AppSettings Settings { get; set; } = new();

    /// <summary>False if settings.json did not exist yet (the defaults are used).</summary>
    public bool FileExisted { get; set; }

    /// <summary>Dutch notes: an unreadable file, or values that were repaired. Empty = all fine.</summary>
    public List<string> Problems { get; } = new();
}

/// <summary>Reads and writes settings.json. Never throws for a bad file: the program must keep running.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // settings.json is edited by hand for the advanced settings: be forgiving.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static SettingsLoadResult Load(string path)
    {
        var result = new SettingsLoadResult();

        if (!File.Exists(path))
        {
            return result; // first start: defaults
        }

        result.FileExisted = true;

        try
        {
            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (settings == null)
            {
                throw new JsonException(Messages.ReasonEmptyFile);
            }

            result.Settings = settings;
        }
        catch (Exception error) when (error is JsonException || error is IOException || error is UnauthorizedAccessException)
        {
            // Keep the broken file as it is (the user may want to fix it); start with the defaults.
            result.Settings = new AppSettings();
            result.Problems.Add(Messages.SettingsUnreadable(path, error.Message));
        }

        result.Problems.AddRange(result.Settings.Normalize());
        return result;
    }

    /// <summary>Writes settings.json. First as .tmp, then renamed, so a crash never leaves half a file.</summary>
    public static void Save(string path, AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, Options);
        string tempPath = path + ".tmp";

        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, path, overwrite: true);
    }
}
