namespace TsDssConverter.Tests;

/// <summary>Finds the files in the samples folder, wherever the tests are started from.</summary>
public static class TestPaths
{
    public static string SamplesFolder { get; } = FindSamplesFolder();

    public static string InfoFile => Path.Combine(SamplesFolder, "topsolid", "Verschuren-P-20-LI.xlsx");
    public static string PositionFile => Path.Combine(SamplesFolder, "topsolid", "Verschuren-P-20-LP.xlsx");
    public static string MaterialsFile => Path.Combine(SamplesFolder, "materials.sample.csv");
    public static string GoldenFolder => Path.Combine(SamplesFolder, "duivestein");

    /// <summary>The PlanDate in the golden XML. The real converter writes today's date; tests use this fixed one.</summary>
    public static DateTime GoldenPlanDate => new DateTime(2026, 9, 19);

    private static string FindSamplesFolder()
    {
        // Walk up from bin\Debug\net10.0\ until a folder contains "samples".
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            string candidate = Path.Combine(folder.FullName, "samples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            folder = folder.Parent;
        }

        throw new DirectoryNotFoundException("Folder 'samples' not found above " + AppContext.BaseDirectory);
    }
}

/// <summary>A new empty folder in the temp folder, deleted again at the end of the test.</summary>
public class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsDssTests_" + Guid.NewGuid().ToString("N"));

    public TempFolder()
    {
        Directory.CreateDirectory(Path);
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
