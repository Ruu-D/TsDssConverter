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
            CncExtension = ".pgmx",
            CncPathPrefixInXml = @"\\server\share",
            CsvEncoding = "windows-1252",
        };

        var converterSettings = settings.ToConverterSettings();

        Assert.Equal(@"D:\Export\", converterSettings.TopSolidExportPath);
        Assert.True(converterSettings.FlipX);
        Assert.True(converterSettings.FlipY);
        Assert.Equal(".pgmx", converterSettings.CncExtension);
        Assert.Equal(@"\\server\share", converterSettings.CncPathPrefixInXml);
        Assert.Equal("windows-1252", converterSettings.CsvEncoding);
    }
}
