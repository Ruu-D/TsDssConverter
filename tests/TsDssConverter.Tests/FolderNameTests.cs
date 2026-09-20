using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>The folders _Verwerkt and _Fout inside the export folder are named in the language of the app.</summary>
public class FolderNameTests
{
    [Theory]
    [InlineData(AppLanguage.Dutch, "_Verwerkt", "_Fout")]
    [InlineData(AppLanguage.French, "_Effectuee", "_Erreur")]
    [InlineData(AppLanguage.English, "_Converted", "_Error")]
    public void TheNamesFollowTheLanguage(AppLanguage language, string done, string error)
    {
        using var scope = new LanguageScope(language);

        Assert.Equal(done, Messages.DoneFolderName);
        Assert.Equal(error, Messages.ErrorFolderName);
        Assert.Equal(done, ProjectProcessor.DoneFolderName);
        Assert.Equal(error, ProjectProcessor.ErrorFolderName);
        Assert.Contains(error, Messages.ErrorFolderNamesInAllLanguages); // "Retry failed" searches all three
    }

    [Theory]
    [InlineData(AppLanguage.Dutch, "_Verwerkt")]
    [InlineData(AppLanguage.French, "_Effectuee")]
    [InlineData(AppLanguage.English, "_Converted")]
    public void AConvertedProject_GoesToTheDoneFolderOfTheLanguage(AppLanguage language, string done)
    {
        using var scope = new LanguageScope(language);
        using var world = new ExportFixture();
        world.AddProject();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.True(outcome.Success, outcome.Message);
        Assert.Equal(world.StampFolder(done), outcome.MovedTo);
        Assert.True(File.Exists(Path.Combine(world.StampFolder(done), "DAAN_ROGIERS-P2026.09-LI.xlsx")));
    }

    [Theory]
    [InlineData(AppLanguage.Dutch, "_Fout")]
    [InlineData(AppLanguage.French, "_Erreur")]
    [InlineData(AppLanguage.English, "_Error")]
    public void ARefusedProject_GoesToTheErrorFolderOfTheLanguage_WithItsReport(AppLanguage language, string error)
    {
        using var scope = new LanguageScope(language);
        using var world = new ExportFixture("TopSolidMaterial;DssMaterial;Thickness;Grain\r\n");   // no materials: every job is refused
        world.AddProject();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.Equal(world.StampFolder(error), outcome.MovedTo);
        Assert.True(File.Exists(Path.Combine(world.StampFolder(error), "fout.txt")));
    }

    [Fact]
    public void TheFoldersOfAnotherLanguage_AreNeverScannedAsNewProjects()
    {
        // The user switches the language: the old _Verwerkt folder stays, and it must not be looked at again.
        using var world = new ExportFixture();
        foreach (string folder in new[] { "_Verwerkt", "_Effectuee", "_Converted", "_Fout", "_Erreur", "_Error" })
        {
            Directory.CreateDirectory(Path.Combine(world.Export, folder, "old"));
            File.WriteAllText(Path.Combine(world.Export, folder, "old", "Oud-LI.xlsx"), "x");
            File.WriteAllText(Path.Combine(world.Export, folder, "old", "Oud-TR.xlsx"), "x");
        }

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        Assert.Empty(scan.Ready);
        Assert.False(scan.IsWaiting);
    }
}