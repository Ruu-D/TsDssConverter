using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>Tests for the data folder, the log writer and the conversion history.</summary>
public class AppFilesTests
{
    // ---------------------------------------------------------------- data folder

    [Fact]
    public void DataFolder_IsCTsDssConverter()
    {
        var folder = AppDataFolder.Default();

        Assert.Equal(@"C:\TsDssConverter", folder.Root);
        Assert.Equal(@"C:\TsDssConverter\settings.json", folder.SettingsFile);
        Assert.Equal(@"C:\TsDssConverter\materials.csv", folder.MaterialsFile);
        Assert.Equal(@"C:\TsDssConverter\columns-li.txt", folder.InfoColumnsFile);
        Assert.Equal(@"C:\TsDssConverter\columns-lp.txt", folder.PositionColumnsFile);
        Assert.Equal(@"C:\TsDssConverter\logs", folder.LogFolder);
    }

    [Fact]
    public void EnsureCreated_MakesTheTwoColumnFiles_WithTheDefaultNames_ThatTheProgramCanRead()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.File("TsDssConverter"));

        folder.EnsureCreated();

        var info = ColumnMap.LoadLabelInfo(folder.InfoColumnsFile);
        var position = ColumnMap.LoadLabelPosition(folder.PositionColumnsFile);
        Assert.Equal(new[] { "Test" }, info.HeadersFor(ColumnKeys.Info.Project));
        Assert.Equal(new[] { "LABEL_X" }, position.HeadersFor(ColumnKeys.Position.LabelX));
        Assert.Contains("SheetName", File.ReadAllText(folder.InfoColumnsFile));
        Assert.Contains("SUP_DESIGNATION", File.ReadAllText(folder.PositionColumnsFile));
    }

    [Fact]
    public void EnsureCreated_NeverTouchesAColumnFileThatWasChanged_ButMakesADeletedOneAgain()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.Path);
        folder.EnsureCreated();

        File.WriteAllText(folder.InfoColumnsFile, "Project = Nummer\r\n");   // the customer changed this one
        File.Delete(folder.PositionColumnsFile);                            // and deleted this one

        folder.EnsureCreated();

        Assert.Equal("Project = Nummer\r\n", File.ReadAllText(folder.InfoColumnsFile));
        Assert.True(File.Exists(folder.PositionColumnsFile));
    }

    [Fact]
    public void EnsureCreated_MakesTheFolders_AndAnEmptyMaterialsFileThatTheProgramCanRead()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.File("TsDssConverter"));

        folder.EnsureCreated();

        Assert.True(Directory.Exists(folder.LogFolder));
        Assert.Equal("TopSolidMaterial;DssMaterial;Thickness;Grain\r\n", File.ReadAllText(folder.MaterialsFile));
        Assert.Throws<ConversionException>(() => MaterialTable.Load(folder.MaterialsFile).Find("White_18")); // valid file, no materials yet
    }

    [Fact]
    public void EnsureCreated_NeverTouchesAnExistingMaterialsFile()
    {
        using var temp = new TempFolder();
        var folder = new AppDataFolder(temp.Path);
        File.WriteAllText(folder.MaterialsFile, "TopSolidMaterial;DssMaterial;Thickness;Grain\nWhite_18;White_18;18;0\n");

        folder.EnsureCreated();
        folder.EnsureCreated(); // twice: still fine

        Assert.Equal("White_18", MaterialTable.Load(folder.MaterialsFile).Find("White_18").DssName);
    }

    // ---------------------------------------------------------------- log writer

    [Fact]
    public void Log_WritesOneLinePerMessage_InTheFileOfTheDay()
    {
        using var temp = new TempFolder();
        var now = new DateTime(2026, 9, 19, 14, 32, 5);
        var log = new LogWriter(temp.File("logs"), now: () => now);

        log.Info("Gestart");
        log.Warning("CNC niet gevonden");
        log.Error("Batch bestaat al");

        string[] lines = File.ReadAllLines(temp.File(@"logs\2026-09-19.log"));
        Assert.Equal(new[]
        {
            "2026-09-19 14:32:05 INFO  Gestart",
            "2026-09-19 14:32:05 WARN  CNC niet gevonden",
            "2026-09-19 14:32:05 ERROR Batch bestaat al",
        }, lines);
    }

    [Fact]
    public void Log_StartsANewFileEachDay()
    {
        using var temp = new TempFolder();
        var now = new DateTime(2026, 9, 19, 23, 59, 0);
        var log = new LogWriter(temp.Path, now: () => now);

        log.Info("laat");
        now = now.AddMinutes(2);
        log.Info("vroeg");

        Assert.True(File.Exists(temp.File("2026-09-19.log")));
        Assert.True(File.Exists(temp.File("2026-09-20.log")));
    }

    [Fact]
    public void Log_MessageWithSeveralLines_IsIndentedSoTheEntryStaysReadable()
    {
        using var temp = new TempFolder();
        var log = new LogWriter(temp.Path, now: () => new DateTime(2026, 9, 19, 8, 0, 0));

        log.Error("Materiaal 'A' staat niet in materials.csv.\r\nMateriaal 'B' staat niet in materials.csv.");

        string[] lines = File.ReadAllLines(temp.File("2026-09-19.log"));
        Assert.Equal(2, lines.Length);
        Assert.Equal("    Materiaal 'B' staat niet in materials.csv.", lines[1]);
    }

    [Fact]
    public void Log_KeepsThirtyDays_AndOnlyDeletesOldLogFiles()
    {
        using var temp = new TempFolder();
        var today = new DateTime(2026, 9, 19, 10, 0, 0);
        var log = new LogWriter(temp.Path, keepDays: 30, now: () => today);

        File.WriteAllText(temp.File("2026-08-19.log"), "exactly 31 days old: deleted");
        File.WriteAllText(temp.File("2026-08-20.log"), "30 days old: kept");
        File.WriteAllText(temp.File("2026-01-01.log"), "very old: deleted");
        File.WriteAllText(temp.File("2026-09-19.log"), "today: kept");
        File.WriteAllText(temp.File("notes.log"), "not a dated log: never touched");
        File.WriteAllText(temp.File("2026-01-01.txt"), "not a .log file: never touched");

        log.DeleteOldLogs();

        Assert.False(File.Exists(temp.File("2026-08-19.log")));
        Assert.False(File.Exists(temp.File("2026-01-01.log")));
        Assert.True(File.Exists(temp.File("2026-08-20.log")));
        Assert.True(File.Exists(temp.File("2026-09-19.log")));
        Assert.True(File.Exists(temp.File("notes.log")));
        Assert.True(File.Exists(temp.File("2026-01-01.txt")));
    }

    [Fact]
    public void Log_NeverThrows_WhenTheFileCannotBeWritten()
    {
        using var temp = new TempFolder();
        var now = new DateTime(2026, 9, 19, 8, 0, 0);
        var log = new LogWriter(temp.Path, now: () => now);

        // A folder with the name of today's log file: the file cannot be created.
        Directory.CreateDirectory(temp.File("2026-09-19.log"));

        log.Info("this must not crash the program");
        log.DeleteOldLogs();
    }

    [Fact]
    public void Log_NeverThrows_WhenTheLogFolderDoesNotExist()
    {
        var log = new LogWriter(@"C:\does\not\exist\anywhere\logs");

        log.DeleteOldLogs(); // nothing to delete: no error
    }

    // ---------------------------------------------------------------- history

    private static HistoryEntry Entry(int number, bool success = true)
    {
        return new HistoryEntry { Time = new DateTime(2026, 9, 19, 8, 0, 0).AddMinutes(number), Project = "P-" + number, Success = success, Message = "msg " + number };
    }

    [Fact]
    public void History_IsEmptyAtFirst()
    {
        var history = new ConversionHistory();

        Assert.Empty(history.GetEntries());
        Assert.Null(history.Latest);
    }

    [Fact]
    public void History_ShowsTheNewestFirst()
    {
        var history = new ConversionHistory();

        history.Add(Entry(1));
        history.Add(Entry(2, success: false));

        Assert.Equal(new[] { "P-2", "P-1" }, history.GetEntries().Select(e => e.Project));
        Assert.Equal("P-2", history.Latest!.Project);
        Assert.False(history.Latest.Success);
    }

    [Fact]
    public void History_KeepsOnlyTheLastTwenty()
    {
        var history = new ConversionHistory();

        for (int i = 1; i <= 25; i++)
        {
            history.Add(Entry(i));
        }

        var entries = history.GetEntries();
        Assert.Equal(20, entries.Count);
        Assert.Equal("P-25", entries.First().Project);
        Assert.Equal("P-6", entries.Last().Project); // 1 to 5 were pushed out
    }

    [Fact]
    public void History_RaisesChanged_AndGivesACopyThatDoesNotChangeLater()
    {
        var history = new ConversionHistory();
        int changes = 0;
        history.Changed += () => changes++;

        history.Add(Entry(1));
        var snapshot = history.GetEntries();
        history.Add(Entry(2));

        Assert.Equal(2, changes);
        Assert.Single(snapshot); // the list the window was given is not modified behind its back
    }

    [Fact]
    public void History_CanBeFilledFromSeveralThreads()
    {
        var history = new ConversionHistory();

        Parallel.For(0, 200, i => history.Add(Entry(i)));

        Assert.Equal(20, history.GetEntries().Count);
    }
}
