using System.Security.Cryptography;
using System.Text;
using TsDssConverter.Core;

namespace TsDssConverter.Tray;

/// <summary>The command line switches. Only for development; the customer just starts the exe.</summary>
internal class StartupOptions
{
    /// <summary>--data &lt;folder&gt;: use another data folder than C:\TsDssConverter (so a test never touches the real one).</summary>
    public string? DataFolder { get; private set; }

    /// <summary>--demo: adds menu items to try out the looks of the icon.</summary>
    public bool Demo { get; private set; }

    /// <summary>--show: opens the settings window right at the start (handy to look at the window).</summary>
    public bool ShowSettings { get; private set; }

    public static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--demo")
            {
                options.Demo = true;
            }
            else if (args[i] == "--show")
            {
                options.ShowSettings = true;
            }
            else if (args[i] == "--data" && i + 1 < args.Length)
            {
                i++;
                options.DataFolder = args[i];
            }
        }

        return options;
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = StartupOptions.Parse(args);
        var dataFolder = options.DataFolder == null ? AppDataFolder.Default() : new AppDataFolder(options.DataFolder);

        // Only one instance per data folder, for the whole PC ("Global\"), also when two Windows users are
        // logged in: two programs converting the same files would do every job twice.
        // A second start ends quietly, without a message.
        using var mutex = new Mutex(initiallyOwned: true, MutexName(dataFolder.Root), out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();

        try
        {
            dataFolder.EnsureCreated();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            MessageBox.Show(Strings.DataFolderFailed(dataFolder.Root, error.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using var app = new TrayApp(dataFolder, options);
        Application.Run(app);
        return 0;
    }

    /// <summary>A mutex name cannot contain a backslash, so the folder path becomes a short hash.</summary>
    private static string MutexName(string dataFolderPath)
    {
        string normalised = Path.GetFullPath(dataFolderPath).TrimEnd('\\').ToLowerInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        return @"Global\TsDssConverter." + Convert.ToHexString(hash, 0, 8);
    }
}
