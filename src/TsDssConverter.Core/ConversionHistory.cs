namespace TsDssConverter.Core;

/// <summary>One line in the list "last conversions" of the settings window.</summary>
public class HistoryEntry
{
    public DateTime Time { get; set; }
    public string Project { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// The last 20 conversions, newest first. Filled by the conversion thread, shown by the window,
/// so it is protected with a lock.
/// </summary>
public class ConversionHistory
{
    public const int MaxEntries = 20;

    private readonly object _lock = new();
    private readonly List<HistoryEntry> _entries = new();

    /// <summary>Raised after an entry was added. It can come from another thread than the window's.</summary>
    public event Action? Changed;

    public void Add(HistoryEntry entry)
    {
        lock (_lock)
        {
            _entries.Insert(0, entry);

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>A copy of the entries, newest first. Safe to loop over while another thread adds.</summary>
    public List<HistoryEntry> GetEntries()
    {
        lock (_lock)
        {
            return new List<HistoryEntry>(_entries);
        }
    }

    /// <summary>The most recent entry, or null if there is none yet.</summary>
    public HistoryEntry? Latest
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count > 0 ? _entries[0] : null;
            }
        }
    }
}
