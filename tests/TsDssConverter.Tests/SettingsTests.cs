using TsDssConverter.Core;

namespace TsDssConverter.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_AreTheOnesInTheSpecification()
    {
        var settings = new AppSettings();

        Assert.Equal(@"Z:\TopSolid\Export\", settings.TopSolidExportPath);
        Assert.Equal(@"Z:\Duivestein\Batch", settings.BatchFolder);
        Assert.Equal(@"Z:\Duivestein\Label", settings.LabelFolder);
        Assert.False(settings.FlipX);
        Assert.False(settings.FlipY);
        Assert.False(settings.PlanRotated);
        Assert.Equal(".xcs", settings.CncExtension);
        Assert.Equal("", settings.CncPathPrefixInXml);
        Assert.Equal("utf-8", settings.CsvEncoding);
        Assert.Equal(30, settings.RescanSeconds);
        Assert.True(settings.RequireTriggerFile);
    }

    [Fact]
    public void FirstStart_NoFile_GivesDefaultsWithoutProblems()
    {
        using var folder = new TempFolder();

        var result = SettingsStore.Load(folder.File("settings.json"));

        Assert.False(result.FileExisted);
        Assert.Empty(result.Problems);
        Assert.Equal(30, result.Settings.RescanSeconds);
    }

    [Fact]
    public void SaveThenLoad_GivesTheSameSettings()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        var settings = new AppSettings
        {
            TopSolidExportPath = @"D:\Export\",
            BatchFolder = @"D:\Batch",
            LabelFolder = @"D:\Label",
            FlipX = true,
            FlipY = true,
            PlanRotated = true,
            CncExtension = ".pgmx",
            CncPathPrefixInXml = @"\\server\share",
            CsvEncoding = "windows-1252",
            RescanSeconds = 60,
            RequireTriggerFile = false,
        };

        SettingsStore.Save(path, settings);
        var result = SettingsStore.Load(path);

        Assert.True(result.FileExisted);
        Assert.Empty(result.Problems);
        Assert.Equal(@"D:\Export\", result.Settings.TopSolidExportPath);
        Assert.Equal(@"D:\Batch", result.Settings.BatchFolder);
        Assert.Equal(@"D:\Label", result.Settings.LabelFolder);
        Assert.True(result.Settings.FlipX);
        Assert.True(result.Settings.FlipY);
        Assert.True(result.Settings.PlanRotated);
        Assert.Equal(".pgmx", result.Settings.CncExtension);
        Assert.Equal(@"\\server\share", result.Settings.CncPathPrefixInXml);
        Assert.Equal("windows-1252", result.Settings.CsvEncoding);
        Assert.Equal(60, result.Settings.RescanSeconds);
        Assert.False(result.Settings.RequireTriggerFile);
        Assert.False(File.Exists(path + ".tmp")); // no leftover temporary file
    }

    [Fact]
    public void SavedFile_UsesTheSettingNamesFromTheSpecification_AndIsEditableByHand()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");

        SettingsStore.Save(path, new AppSettings());
        string json = File.ReadAllText(path);

        foreach (string name in new[] { "CncExtension", "CncPathPrefixInXml", "CsvEncoding", "RescanSeconds", "RequireTriggerFile" })
        {
            Assert.Contains("\"" + name + "\"", json);
        }

        Assert.Contains(Environment.NewLine, json); // indented, one setting per line
    }

    [Fact]
    public void HandEditedFile_WithComments_TrailingCommaAndUnknownSetting_IsAccepted()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        File.WriteAllText(path, """
            {
              // the customer wants more time between scans
              "RescanSeconds": 45,
              "SomethingFromANewerVersion": true,
            }
            """);

        var result = SettingsStore.Load(path);

        Assert.Empty(result.Problems);
        Assert.Equal(45, result.Settings.RescanSeconds);
        Assert.Equal(@"Z:\TopSolid\Export\", result.Settings.TopSolidExportPath); // not in the file: default
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{ \"RescanSeconds\": \"thirty\" }")] // wrong type
    [InlineData("")]
    [InlineData("null")]
    public void BrokenFile_GivesDefaultsAndAProblem_AndIsNotOverwritten(string content)
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        File.WriteAllText(path, content);

        var result = SettingsStore.Load(path);

        Assert.True(result.FileExisted);
        Assert.Contains(result.Problems, p => p.Contains("settings.json kon niet gelezen worden"));
        Assert.Equal(30, result.Settings.RescanSeconds);
        Assert.Equal(content, File.ReadAllText(path)); // the user's file is left alone
    }

    [Fact]
    public void Normalize_RepairsValuesThatWouldBreakTheProgram()
    {
        var settings = new AppSettings
        {
            TopSolidExportPath = "   ",
            BatchFolder = "",
            CncExtension = "xcs",       // no dot
            CsvEncoding = "no-such-encoding",
            RescanSeconds = 1,          // far too often for a network drive
        };

        var notes = settings.Normalize();

        Assert.Equal(@"Z:\TopSolid\Export\", settings.TopSolidExportPath);
        Assert.Equal(@"Z:\Duivestein\Batch", settings.BatchFolder);
        Assert.Equal(".xcs", settings.CncExtension);
        Assert.Equal("utf-8", settings.CsvEncoding);
        Assert.Equal(30, settings.RescanSeconds);
        Assert.Contains(notes, n => n.Contains("TopSolidExportPath"));
        Assert.Contains(notes, n => n.Contains("CsvEncoding"));
        Assert.Contains(notes, n => n.Contains("RescanSeconds"));
    }

    [Fact]
    public void Normalize_LeavesGoodSettingsAlone()
    {
        var settings = new AppSettings { RescanSeconds = 60, CncExtension = ".pgmx", CsvEncoding = "windows-1252" };

        var notes = settings.Normalize();

        Assert.Empty(notes);
        Assert.Equal(60, settings.RescanSeconds);
        Assert.Equal(".pgmx", settings.CncExtension);
    }

    [Fact]
    public void ToConverterSettings_CopiesWhatTheConversionNeeds()
    {
        var settings = new AppSettings
        {
            TopSolidExportPath = @"D:\Export\",
            FlipX = true,
            FlipY = true,
            PlanRotated = true,
            CncExtension = ".pgmx",
            CncPathPrefixInXml = @"\\server\share",
            CsvEncoding = "windows-1252",
        };

        var converterSettings = settings.ToConverterSettings();

        Assert.Equal(@"D:\Export\", converterSettings.TopSolidExportPath);
        Assert.True(converterSettings.FlipX);
        Assert.True(converterSettings.FlipY);
        Assert.True(converterSettings.PlanRotated);
        Assert.Equal(".pgmx", converterSettings.CncExtension);
        Assert.Equal(@"\\server\share", converterSettings.CncPathPrefixInXml);
        Assert.Equal("windows-1252", converterSettings.CsvEncoding);
    }

    // ---------------------------------------------------------------- settings.json edited by hand while the program runs

    // The settings the window shows; every other setting is "advanced" (only in settings.json).
    private static readonly string[] WindowSettings =
        { "TopSolidExportPath", "BatchFolder", "LabelFolder", "FlipX", "FlipY", "PlanRotated", "Language", "FoldersFolded" };

    private static readonly string[] AdvancedSettings =
        { "CncExtension", "CncPathPrefixInXml", "CsvEncoding", "RescanSeconds", "RequireTriggerFile" };

    [Fact]
    public void EverySetting_IsEitherShownInTheWindowOrAdvanced()
    {
        // A new setting must be put in one of the two lists above, and in AppSettings.CopyWindowSettingsFrom if the window
        // shows it: otherwise Save would quietly throw away a value edited by hand, or never save the window's value.
        var names = typeof(AppSettings).GetProperties().Select(property => property.Name).OrderBy(name => name);

        Assert.Equal(WindowSettings.Concat(AdvancedSettings).OrderBy(name => name), names);
    }

    [Fact]
    public void SaveWindowSettings_TakesWhatTheWindowShows_AndKeepsWhatWasEditedByHandInTheFile()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");

        // The program started with the defaults; then someone edited the advanced settings (and a folder) in Notepad.
        var inUse = new AppSettings();
        SettingsStore.Save(path, inUse);
        File.WriteAllText(path, """
            {
              "TopSolidExportPath": "D:\\EditedByHand\\",
              "CncExtension": ".pgmx",
              "CncPathPrefixInXml": "\\\\server\\share\\",
              "CsvEncoding": "windows-1252",
              "RescanSeconds": 60,
              "RequireTriggerFile": false
            }
            """);

        // The window (which still had the old values in use) is saved with new values of its own.
        AppSettings fromWindow = inUse.Clone();
        fromWindow.TopSolidExportPath = @"E:\FromWindow\";
        fromWindow.BatchFolder = @"E:\Batch";
        fromWindow.LabelFolder = @"E:\Label";
        fromWindow.FlipX = true;
        fromWindow.FlipY = true;
        fromWindow.PlanRotated = true;
        fromWindow.Language = "fr";

        SettingsLoadResult saved = SettingsStore.SaveWindowSettings(path, fromWindow);
        AppSettings onDisk = SettingsStore.Load(path).Settings;

        foreach (AppSettings settings in new[] { saved.Settings, onDisk })
        {
            // The window wins for what it shows ...
            Assert.Equal(@"E:\FromWindow\", settings.TopSolidExportPath);
            Assert.Equal(@"E:\Batch", settings.BatchFolder);
            Assert.Equal(@"E:\Label", settings.LabelFolder);
            Assert.True(settings.FlipX);
            Assert.True(settings.FlipY);
            Assert.True(settings.PlanRotated);
            Assert.Equal("fr", settings.Language);

            // ... the file for what is only in the file.
            Assert.Equal(".pgmx", settings.CncExtension);
            Assert.Equal(@"\\server\share\", settings.CncPathPrefixInXml);
            Assert.Equal("windows-1252", settings.CsvEncoding);
            Assert.Equal(60, settings.RescanSeconds);
            Assert.False(settings.RequireTriggerFile);
        }
    }

    [Fact]
    public void SaveWindowSettings_AFileWithAMistake_IsNotOverwritten()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        const string broken = "{ \"RescanSeconds\": +60 }"; // "+60" is not valid json
        File.WriteAllText(path, broken);

        var error = Assert.Throws<IOException>(() => SettingsStore.SaveWindowSettings(path, new AppSettings()));

        Assert.Contains("niet overschreven", error.Message); // the window shows this message
        Assert.Equal(broken, File.ReadAllText(path));
    }

    [Fact]
    public void SaveWindowSettings_WithoutAFile_WritesTheSettingsInUse()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        var fromWindow = new AppSettings { BatchFolder = @"E:\Batch", RescanSeconds = 90 };

        SettingsStore.SaveWindowSettings(path, fromWindow);

        AppSettings onDisk = SettingsStore.Load(path).Settings;
        Assert.Equal(@"E:\Batch", onDisk.BatchFolder);
        Assert.Equal(90, onDisk.RescanSeconds); // the advanced value in use is kept, not the default
    }

    [Fact]
    public void SaveChange_WritesOnlyThatOneChange_AndKeepsEverythingElseOfTheFile()
    {
        // Folding the folders panel or choosing a language is saved at once (2.0.0); values typed in the window but not
        // saved, and values edited by hand in the file, must not be touched by that.
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        File.WriteAllText(path, """{ "TopSolidExportPath": "D:\\EditedByHand\\", "RescanSeconds": 60, "Language": "nl" }""");
        var inUse = new AppSettings { TopSolidExportPath = @"E:\TypedButNotSaved\" };

        SettingsLoadResult result = SettingsStore.SaveChange(path, inUse, settings => settings.FoldersFolded = true);
        SettingsStore.SaveChange(path, inUse, settings => settings.Language = "fr");

        AppSettings onDisk = SettingsStore.Load(path).Settings;
        Assert.True(result.Settings.FoldersFolded);
        Assert.True(onDisk.FoldersFolded);
        Assert.Equal("fr", onDisk.Language);
        Assert.Equal(@"D:\EditedByHand\", onDisk.TopSolidExportPath); // not the value of the window
        Assert.Equal(60, onDisk.RescanSeconds);
    }

    [Fact]
    public void SaveChange_AFileWithAMistake_IsNotOverwritten_AndWithoutAFileTheSettingsInUseAreWritten()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        const string broken = "{ \"RescanSeconds\": +60 }";
        File.WriteAllText(path, broken);

        Assert.Throws<IOException>(() => SettingsStore.SaveChange(path, new AppSettings(), settings => settings.FoldersFolded = true));
        Assert.Equal(broken, File.ReadAllText(path));

        File.Delete(path);
        SettingsStore.SaveChange(path, new AppSettings { RescanSeconds = 90 }, settings => settings.FoldersFolded = true);
        AppSettings onDisk = SettingsStore.Load(path).Settings;
        Assert.True(onDisk.FoldersFolded);
        Assert.Equal(90, onDisk.RescanSeconds);
    }

    [Fact]
    public void Checker_GivesTheFileOnlyWhenItWasChangedOutsideTheProgram()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        SettingsStore.Save(path, new AppSettings());
        var checker = new SettingsFileChecker(path);

        Assert.Null(checker.ReadIfChanged()); // nothing changed

        File.WriteAllText(path, "{ \"RescanSeconds\": 60 }");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10)); // surely another time than before

        SettingsLoadResult? changed = checker.ReadIfChanged();
        Assert.NotNull(changed);
        Assert.Equal("", changed.ReadError);
        Assert.Equal(60, changed.Settings.RescanSeconds);
        Assert.Null(checker.ReadIfChanged()); // only once

        // The program saves the file itself and says so: that is not a change from outside.
        SettingsStore.Save(path, changed.Settings);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(20));
        checker.Remember();
        Assert.Null(checker.ReadIfChanged());
    }

    [Fact]
    public void Checker_AFileWithAMistakeIsGivenOnce_AFileInUseIsTriedAgainLater()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        SettingsStore.Save(path, new AppSettings());
        var checker = new SettingsFileChecker(path);

        File.WriteAllText(path, "{ \"RescanSeconds\": +60 }");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10));
        SettingsLoadResult? broken = checker.ReadIfChanged();
        Assert.NotNull(broken);
        Assert.NotEqual("", broken.ReadError);
        Assert.False(broken.ReadErrorIsTemporary);
        Assert.Null(checker.ReadIfChanged()); // reported once, not every few seconds

        File.WriteAllText(path, "{ \"RescanSeconds\": 60 }");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(20));
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // e.g. Notepad is still writing it
        {
            Assert.Null(checker.ReadIfChanged());
        }

        Assert.Equal(60, checker.ReadIfChanged()!.Settings.RescanSeconds); // the next look finds it
    }
}
