using System.Globalization;
using System.Text;

namespace TsDssConverter.Core;

/// <summary>
/// A simple log file per day: logs\yyyy-MM-dd.log, one line per message, old files are deleted.
/// Writing a log must NEVER crash the program, so every problem with the file is swallowed.
/// </summary>
public class LogWriter
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _folder;
    private readonly Func<DateTime> _now;
    private readonly int _keepDays;
    private readonly object _lock = new(); // the tray thread and the conversion thread both write

    /// <param name="now">Only for tests: gives the "current" time.</param>
    public LogWriter(string folder, int keepDays = 30, Func<DateTime>? now = null)
    {
        _folder = folder;
        _keepDays = keepDays;
        _now = now ?? (() => DateTime.Now);
    }

    public string CurrentLogFile => Path.Combine(_folder, _now().ToString(DateFormat, CultureInfo.InvariantCulture) + ".log");

    public void Info(string message) => Write("INFO ", message);

    public void Warning(string message) => Write("WARN ", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_folder);

                // A message with several lines (an error report): indent the next lines, so each entry stays readable.
                string text = message.Replace("\r\n", "\n").Replace("\n", "\r\n    ");
                string time = _now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                string line = $"{time} {level} {text}\r\n";

                File.AppendAllText(CurrentLogFile, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // Disk full, file locked, folder gone: nothing sensible to do. Keep the program running.
        }
    }

    /// <summary>Deletes log files older than the number of days to keep. The date comes from the file NAME.</summary>
    public void DeleteOldLogs()
    {
        try
        {
            if (!Directory.Exists(_folder))
            {
                return;
            }

            DateTime oldestToKeep = _now().Date.AddDays(-_keepDays);

            foreach (string file in Directory.GetFiles(_folder, "*.log"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                bool isDated = DateTime.TryParseExact(name, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date);

                if (isDated && date < oldestToKeep)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // Same as above: cleaning up is not important enough to stop the program.
        }
    }
}
