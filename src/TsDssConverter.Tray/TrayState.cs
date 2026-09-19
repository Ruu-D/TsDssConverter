namespace TsDssConverter.Tray;

/// <summary>The four looks of the tray icon.</summary>
public enum TrayState
{
    /// <summary>Everything fine (the base icon).</summary>
    Ok,

    /// <summary>A conversion is running.</summary>
    Busy,

    /// <summary>The last conversion (or the start-up) failed. Stays until the next success or until the window is opened.</summary>
    Error,

    /// <summary>The user paused the program (pause symbol at the bottom right, like OneDrive).
    /// Only used to choose the icon: the tray decides this by itself, nobody has to set it.</summary>
    Paused,
}

/// <summary>Decides which icon is shown when several things are true at the same time.</summary>
internal static class TrayStateRules
{
    /// <summary>
    /// Priority: Error, then Busy, then Paused, then Ok.
    ///  - An error needs attention, so it is always visible (it disappears when the window is opened).
    ///  - A running conversion is really happening, so "busy" is honest even if the user just paused
    ///    (the pause takes effect when that conversion is finished).
    ///  - Paused is shown while nothing else is going on.
    /// </summary>
    /// <param name="state">What the program is doing: Ok, Busy or Error.</param>
    /// <param name="paused">Whether the user paused the program.</param>
    public static TrayState Displayed(TrayState state, bool paused)
    {
        if (state == TrayState.Error || state == TrayState.Busy)
        {
            return state;
        }

        return paused ? TrayState.Paused : TrayState.Ok;
    }
}
