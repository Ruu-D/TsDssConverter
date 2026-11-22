using System.Text;
using System.Text.Json;

namespace TsDssConverter.Core;

/// <summary>The settings that were loaded, plus everything the user should be told about.</summary>
public class SettingsLoadResult
{
    public AppSettings Settings { get; set; } = new();

    /// <summary>False if settings.json did not exist yet (the defaults are used).</summary>
    public bool FileExisted { get; set; }

    /// <summary>Notes in the selected language: an unreadable file, or values that were repaired. Empty = all fine.</summary>
    public List<string> Problems { get; } = new();

    /// <summary>
    /// Not empty when the file exists but could not be used: its text has a mistake, or it could not be opened. The
    /// <see cref="Settings"/> are then the defaults, NOT what is in the file.
    /// </summary>
    public string ReadError { get; set; } = "";

    /// <summary>
    /// True when the file could not be opened (in use by another program, no permission): reading it again a moment later
    /// may work. False for a mistake in the text, which stays until someone fixes the file.
    /// </summary>
    public bool ReadErrorIsTemporary { get; set; }
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
            result.ReadError = error.Message;
            result.ReadErrorIsTemporary = error is IOException || error is UnauthorizedAccessException;
        }

        result.Problems.AddRange(result.Settings.Normalize());
        return result;
    }

    /// <summary>
    /// Saves what the settings window shows WITHOUT losing what someone edited by hand in settings.json meanwhile (in
    /// Notepad, Shift+click on the logo): the file is read again first, only the window's own settings are replaced
    /// (<see cref="AppSettings.CopyWindowSettingsFrom"/>), and the advanced settings stay as they are in the file now.
    /// A file with a mistake is never overwritten: then an IOException is thrown with a message for the user.
    /// </summary>
    /// <param name="fromWindow">The settings in use, with the values of the window put in.</param>
    /// <returns>What was saved (= the settings to use from now on) and the notes about repaired values.</returns>
    public static SettingsLoadResult SaveWindowSettings(string path, AppSettings fromWindow)
    {
        SettingsLoadResult onDisk = Load(path);

        if (onDisk.ReadError != "")
        {
            throw new IOException(Messages.SettingsFileNotOverwritten(path, onDisk.ReadError));
        }

        if (!onDisk.FileExisted)
        {
            // Someone deleted the file: there is nothing to keep, write the settings in use as they are.
            onDisk.Settings = fromWindow.Clone();
        }
        else
        {
            onDisk.Settings.CopyWindowSettingsFrom(fromWindow);
        }

        Save(path, onDisk.Settings);
        return onDisk;
    }

    /// <summary>
    /// Saves ONE change at once, without the Save button (2.0.0: folding the folders panel, choosing another language):
    /// like <see cref="SaveWindowSettings"/> the file is read again first, so everything else in it stays exactly as it
    /// is (also values typed in the window but not saved yet are NOT written). A file with a mistake is never overwritten:
    /// then an IOException is thrown with a message for the user.
    /// </summary>
    /// <param name="inUse">The settings in use: written as they are when the file was deleted.</param>
    /// <param name="change">The one change, e.g. <c>settings => settings.FoldersFolded = true</c>.</param>
    public static SettingsLoadResult SaveChange(string path, AppSettings inUse, Action<AppSettings> change)
    {
        SettingsLoadResult onDisk = Load(path);

        if (onDisk.ReadError != "")
        {
            throw new IOException(Messages.SettingsFileNotOverwritten(path, onDisk.ReadError));
        }

        if (!onDisk.FileExisted)
        {
            onDisk.Settings = inUse.Clone();
        }

        change(onDisk.Settings);
        Save(path, onDisk.Settings);
        return onDisk;
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
