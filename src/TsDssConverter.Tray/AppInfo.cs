using System.Reflection;

namespace TsDssConverter.Tray;

/// <summary>Facts about the program itself.</summary>
internal static class AppInfo
{
    /// <summary>
    /// The version as "1.0.0". It comes from the &lt;Version&gt; line in TsDssConverter.Tray.csproj,
    /// so there is one place to change it.
    /// </summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
}
