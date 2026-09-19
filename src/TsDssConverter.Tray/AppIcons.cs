namespace TsDssConverter.Tray;

/// <summary>Loads the icons and the banner that are built into the exe (from the media folder).</summary>
internal static class AppIcons
{
    /// <summary>The icon for the tray, in the size that fits the current screen scaling.</summary>
    public static Icon ForState(TrayState state)
    {
        string name = state switch
        {
            TrayState.Busy => "app-busy.ico",
            TrayState.Error => "app-error.ico",
            TrayState.Paused => "app-paused.ico",
            _ => "app.ico",
        };

        using var stream = OpenResource(name);
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    /// <summary>The icon for the window (title bar, taskbar): all sizes, Windows picks the right one.</summary>
    public static Icon ForWindow()
    {
        using var stream = OpenResource("app.ico");
        return new Icon(stream);
    }

    /// <summary>The banner with the name of the program (2x version, scaled down: sharp on high-DPI screens).</summary>
    public static Image Banner()
    {
        using var stream = OpenResource("header.png");
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return Image.FromStream(copy); // the MemoryStream must stay open as long as the image is used
    }

    private static Stream OpenResource(string name)
    {
        return typeof(AppIcons).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Ingebouwd bestand niet gevonden: " + name);
    }
}
