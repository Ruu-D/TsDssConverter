using System.Reflection;

namespace TsDssConverter.Tray;

/// <summary>Facts about the program itself.</summary>
internal static class AppInfo
{
    /// <summary>
    /// The version as "2.0.0", or with a suffix such as "2.0.0-alpha" for a test build. It comes from the &lt;Version&gt;
    /// line in TsDssConverter.Tray.csproj, so there is one place to change it. It is read from the "informational
    /// version" (the only one that keeps the suffix); the "+commit" the build adds to it is left out.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        Assembly program = Assembly.GetExecutingAssembly();
        string? informational = program.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            int plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational.Substring(0, plus);
        }

        return program.GetName().Version?.ToString(3) ?? "?";
    }
}
