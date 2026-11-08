namespace TsDssConverter.Core;

/// <summary>
/// Notices that settings.json was changed outside the program (edited by hand in Notepad), so the new values can be used
/// without a restart. It only looks at the date of the file: the tray asks every few seconds, which is cheap.
/// </summary>
public class SettingsFileChecker
{
    private readonly string _path;
    private DateTime _knownTime;

    public SettingsFileChecker(string path)
    {
        _path = path;
        Remember();
    }

    /// <summary>Call after the program itself has read or written the file: that is not a change from outside.</summary>
    public void Remember()
    {
        _knownTime = ReadTime();
    }

    /// <summary>
    /// The file read again when it changed since the last time, otherwise null. Also null when the file is gone (the
    /// settings in use stay) or cannot be opened right now (in use while Notepad saves it): then it is tried again at the
    /// next call. A file with a mistake IS returned (see <see cref="SettingsLoadResult.ReadError"/>), once per change.
    /// </summary>
    public SettingsLoadResult? ReadIfChanged()
    {
        DateTime time = ReadTime();
        if (time == _knownTime)
        {
            return null;
        }

        SettingsLoadResult loaded = SettingsStore.Load(_path);

        if (loaded.ReadErrorIsTemporary)
        {
            return null; // not remembered: try again next time
        }

        _knownTime = time;
        return loaded.FileExisted ? loaded : null;
    }

    private DateTime ReadTime()
    {
        try
        {
            return File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            return _knownTime; // cannot look now (network, permission): act as if nothing changed
        }
    }
}
