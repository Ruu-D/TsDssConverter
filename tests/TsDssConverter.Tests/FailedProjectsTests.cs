using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>"Retry failed": the files of failed projects go back to the export folder and are converted again.</summary>
public class FailedProjectsTests
{
    private const string Project = "DAAN_ROGIERS-P2026.09";
    private const string NoMaterials = "TopSolidMaterial;DssMaterial;Thickness;Grain\r\n"; // every job is refused: unknown material

    /// <summary>A project that failed because materials.csv was empty: its files are in the error folder now.</summary>
    private static ExportWatcher FailOneProject(ExportFixture world, out List<ProcessOutcome> outcomes)
    {
        world.AddProject();
        var watcher = world.NewWatcher();
        var list = new List<ProcessOutcome>();
        watcher.ConversionFinished += list.Add;
        watcher.RunOnce();
        outcomes = list;

        Assert.False(Assert.Single(list).Success);
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), "fout.txt")));
        return watcher;
    }

    /// <summary>The user fixes the cause: the sample materials.csv replaces the empty one.</summary>
    private static void FixMaterials(ExportFixture world) => File.Copy(TestPaths.MaterialsFile, world.MaterialsFile, overwrite: true);

    [Fact]
    public void AfterFixingTheCause_RetryPutsTheFilesBack_AndTheNextScanConvertsTheProject()
    {
        using var world = new ExportFixture(NoMaterials);
        using var watcher = FailOneProject(world, out var outcomes);
        FixMaterials(world);

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Equal(new[] { Project }, result.MovedBack);
        Assert.Empty(result.Blocked);
        Assert.Null(result.Problem);
        Assert.True(File.Exists(world.InfoPath()));       // all three are back where TopSolid wrote them
        Assert.True(File.Exists(world.PositionPath()));
        Assert.True(File.Exists(world.TriggerPath()));
        Assert.False(Directory.Exists(world.StampFolder("_Fout")));   // fout.txt and the empty folder are gone

        watcher.RunOnce();

        Assert.Equal(2, outcomes.Count);
        Assert.True(outcomes[1].Success, outcomes[1].Message);
        Assert.True(File.Exists(Path.Combine(world.Batch, Project + ".xml")));
    }

    [Fact]
    public void WithoutFailedProjects_ThereIsNothingToRetry()
    {
        using var world = new ExportFixture();

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Empty(result.MovedBack);
        Assert.Empty(result.Blocked);
        Assert.Null(result.Problem);
    }

    [Fact]
    public void AFileWithTheSameNameInTheExportFolder_IsNeverOverwritten_TheProjectIsLeftAlone()
    {
        using var world = new ExportFixture(NoMaterials);
        using var watcher = FailOneProject(world, out _);

        // TopSolid exported the same project again in the meantime.
        File.WriteAllText(world.InfoPath(), "the newer export");

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Empty(result.MovedBack);
        Assert.Equal(new[] { Project }, result.Blocked);
        Assert.Equal("the newer export", File.ReadAllText(world.InfoPath()));                 // untouched
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), Project + "-LI.xlsx"))); // still waiting in the error folder
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), "fout.txt")));
    }

    [Fact]
    public void WhenTheMoveFailsHalfway_NothingStaysHalfMoved_AndTheProblemIsReported()
    {
        using var world = new ExportFixture(NoMaterials);
        using var watcher = FailOneProject(world, out _);

        // A FOLDER with the name of the TR file: LI and LP can move back, the TR file cannot.
        Directory.CreateDirectory(world.TriggerPath());

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Empty(result.MovedBack);
        Assert.NotNull(result.Problem);
        Assert.False(File.Exists(world.InfoPath()));       // LI and LP went back to the error folder again
        Assert.False(File.Exists(world.PositionPath()));
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), Project + "-LI.xlsx")));
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), Project + "-LP.xlsx")));
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), Project + "-TR.xlsx")));
    }

    [Theory]
    [InlineData("_Fout")]
    [InlineData("_Erreur")]
    [InlineData("_Error")]
    public void TheErrorFolderOfEveryLanguage_IsSearched(string errorFolder)
    {
        // A job that failed before the user switched the language must still be found.
        using var world = new ExportFixture();
        string job = Path.Combine(world.Export, errorFolder, "20260919-140000 " + Project);
        Directory.CreateDirectory(job);
        File.WriteAllText(Path.Combine(job, Project + "-LI.xlsx"), "li");
        File.WriteAllText(Path.Combine(job, Project + "-LP.xlsx"), "lp");
        File.WriteAllText(Path.Combine(job, Project + "-TR.xlsx"), "tr");
        File.WriteAllText(Path.Combine(job, "fout.txt"), "reason");

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Equal(new[] { Project }, result.MovedBack);
        Assert.True(File.Exists(world.InfoPath()));
        Assert.False(Directory.Exists(job));
    }

    [Fact]
    public void AFolderThatIsNotAJobFolder_IsIgnored_AndAnExtraFileInAJobFolderKeepsTheFolder()
    {
        using var world = new ExportFixture();

        // Not made by us: no time stamp in front of the name.
        string other = Path.Combine(world.Export, "_Fout", "old");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "Oud-LI.xlsx"), "x");

        // Made by us, but the user added a note: the folder stays, the files go back.
        string job = Path.Combine(world.Export, "_Fout", "20260919-140000 " + Project);
        Directory.CreateDirectory(job);
        File.WriteAllText(Path.Combine(job, Project + "-LI.xlsx"), "li");
        File.WriteAllText(Path.Combine(job, "notes.txt"), "my note");

        RetryResult result = FailedProjects.RetryAll(world.Export);

        Assert.Equal(new[] { Project }, result.MovedBack);
        Assert.True(File.Exists(Path.Combine(other, "Oud-LI.xlsx")));      // untouched
        Assert.False(File.Exists(Path.Combine(world.Export, "Oud-LI.xlsx")));
        Assert.True(File.Exists(Path.Combine(job, "notes.txt")));          // the user's file is never deleted
    }

    [Fact]
    public void AnExportFolderThatIsNotThere_GivesAProblem_NotACrash()
    {
        RetryResult result = FailedProjects.RetryAll(TestPaths.NoExportFolder);

        Assert.Empty(result.MovedBack);
        Assert.NotNull(result.Problem);
    }
}
