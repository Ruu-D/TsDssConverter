using TsDssConverter.Core;

namespace TsDssConverter.Tests;

/// <summary>A clock that only moves when the test says so, so "wait 2 minutes" does not take 2 minutes.</summary>
public class FakeClock
{
    public DateTime Now { get; private set; } = new DateTime(2026, 9, 19, 14, 0, 0);

    public void Advance(TimeSpan time) => Now += time;
}

/// <summary>
/// A complete little world for one test: an export folder with the TopSolid files, the two Duivestein folders,
/// a materials.csv and settings that point to them. All in a temp folder that is deleted afterwards.
/// </summary>
public sealed class ExportFixture : IDisposable
{
    private const string Project = "Verschuren-P-20";

    private readonly TempFolder _root = new();
    public FakeClock Clock { get; } = new();
    public string Export { get; }
    public string Batch { get; }
    public string Label { get; }
    public string MaterialsFile { get; }
    public AppSettings Settings { get; }

    /// <param name="materialsFile">The materials file to copy. The default is the sample file; a test can give another.</param>
    public ExportFixture(string? materialsContent = null)
    {
        Export = Directory.CreateDirectory(_root.File("export")).FullName;
        Batch = Directory.CreateDirectory(_root.File("batch")).FullName;
        Label = Directory.CreateDirectory(_root.File("label")).FullName;
        MaterialsFile = _root.File("materials.csv");

        if (materialsContent == null)
        {
            File.Copy(TestPaths.MaterialsFile, MaterialsFile);
        }
        else
        {
            File.WriteAllText(MaterialsFile, materialsContent);
        }

        Settings = new AppSettings { TopSolidExportPath = Export, BatchFolder = Batch, LabelFolder = Label, RescanSeconds = 5 };
    }

    public string InfoPath(string project = Project) => Path.Combine(Export, project + "-LI.xlsx");
    public string PositionPath(string project = Project) => Path.Combine(Export, project + "-LP.xlsx");
    public string TriggerPath(string project = Project) => Path.Combine(Export, project + "-TR.xlsx");

    /// <summary>Puts the sample LI, LP (and TR) in the export folder under the name of the given project.</summary>
    public void AddProject(string project = Project, bool info = true, bool position = true, bool trigger = true)
    {
        if (info) File.Copy(TestPaths.InfoFile, InfoPath(project), overwrite: true);
        if (position) File.Copy(TestPaths.PositionFile, PositionPath(project), overwrite: true);
        if (trigger) File.WriteAllText(TriggerPath(project), "trigger"); // the content of TR does not matter
    }

    public ProjectFiles Files(string project = Project) => new()
    {
        Project = project,
        InfoPath = InfoPath(project),
        PositionPath = PositionPath(project),
        TriggerPath = TriggerPath(project),
    };

    public string StampFolder(string subFolder, string project = Project)
    {
        return Path.Combine(Export, subFolder, Clock.Now.ToString("yyyyMMdd-HHmmss") + " " + project);
    }

    public ExportWatcher NewWatcher(Func<bool>? isPaused = null)
    {
        return new ExportWatcher(() => Settings, isPaused ?? (() => false), new LogWriter(_root.File("logs")), MaterialsFile, () => Clock.Now);
    }

    public void Dispose() => _root.Dispose();
}

public class WatcherTests
{
    // ================================================================ the scanner: what is ready?

    [Fact]
    public void Scanner_WithAllThreeFiles_TheProjectIsReady()
    {
        using var world = new ExportFixture();
        world.AddProject();

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        ProjectFiles ready = Assert.Single(scan.Ready);
        Assert.Equal("Verschuren-P-20", ready.Project);   // the project name is everything before -LI / -LP / -TR
        Assert.Equal(world.InfoPath(), ready.InfoPath);
        Assert.False(scan.IsWaiting);
        Assert.Null(scan.FolderProblem);
    }

    [Fact]
    public void Scanner_WithoutTheTriggerFile_NothingHappens_TopSolidIsNotFinished()
    {
        using var world = new ExportFixture();
        world.AddProject(trigger: false);

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        Assert.Empty(scan.Ready);
        Assert.False(scan.IsWaiting); // nothing to wait for: the TR file is what starts everything
    }

    [Fact]
    public void Scanner_IgnoresExcelLockFiles_OtherXlsxFiles_AndTheFinishedFolders()
    {
        using var world = new ExportFixture();
        world.AddProject();
        File.WriteAllText(Path.Combine(world.Export, "~$Verschuren-P-20-LI.xlsx"), "lock");
        File.WriteAllText(Path.Combine(world.Export, "prijslijst.xlsx"), "x");
        File.WriteAllText(Path.Combine(world.Export, "-TR.xlsx"), "x"); // no project name in front
        Directory.CreateDirectory(Path.Combine(world.Export, "_Verwerkt", "old"));
        File.WriteAllText(Path.Combine(world.Export, "_Verwerkt", "old", "Oud-LI.xlsx"), "x");
        File.WriteAllText(Path.Combine(world.Export, "_Verwerkt", "old", "Oud-TR.xlsx"), "x");

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        Assert.Equal("Verschuren-P-20", Assert.Single(scan.Ready).Project);
        Assert.Empty(scan.GaveUp);
    }

    [Fact]
    public void Scanner_FileNamesInAnyCase_AreRecognised()
    {
        using var world = new ExportFixture();
        File.Copy(TestPaths.InfoFile, Path.Combine(world.Export, "Abc-li.XLSX"));
        File.Copy(TestPaths.PositionFile, Path.Combine(world.Export, "Abc-lp.xlsx"));
        File.WriteAllText(Path.Combine(world.Export, "Abc-tr.xlsx"), "x");

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        Assert.Equal("Abc", Assert.Single(scan.Ready).Project);
    }

    [Fact]
    public void Scanner_TwoProjects_AreHandledSeparately()
    {
        using var world = new ExportFixture();
        world.AddProject("Klant-A-1");
        world.AddProject("Klant-B-2", position: false); // B is not complete

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: true);

        Assert.Equal("Klant-A-1", Assert.Single(scan.Ready).Project);
        Assert.True(scan.IsWaiting); // B waits
    }

    [Fact]
    public void Scanner_WaitsForAMissingFile_AndGivesUpAfterTwoMinutes()
    {
        using var world = new ExportFixture();
        world.AddProject(position: false); // TR and LI are there, LP is not
        var scanner = new ExportScanner(() => world.Clock.Now);

        ScanResult first = scanner.Scan(world.Export, requireTriggerFile: true);
        Assert.Empty(first.Ready);
        Assert.Empty(first.GaveUp);
        Assert.True(first.IsWaiting);                    // so the caller scans again in 2 seconds

        world.Clock.Advance(TimeSpan.FromSeconds(119));
        Assert.Empty(scanner.Scan(world.Export, true).GaveUp); // not yet

        world.Clock.Advance(TimeSpan.FromSeconds(1));
        ScanResult last = scanner.Scan(world.Export, requireTriggerFile: true);

        GaveUpProject gaveUp = Assert.Single(last.GaveUp);
        Assert.Contains("LP", gaveUp.Reason);
        Assert.Contains("2", gaveUp.Reason);
        Assert.False(last.IsWaiting);
    }

    [Fact]
    public void Scanner_WhenTheMissingFileArrivesInTime_TheProjectBecomesReady()
    {
        using var world = new ExportFixture();
        world.AddProject(position: false);
        var scanner = new ExportScanner(() => world.Clock.Now);
        scanner.Scan(world.Export, true);

        world.Clock.Advance(TimeSpan.FromSeconds(30));
        File.Copy(TestPaths.PositionFile, world.PositionPath());
        ScanResult scan = scanner.Scan(world.Export, true);

        Assert.Single(scan.Ready);
        Assert.False(scan.IsWaiting);
    }

    [Fact]
    public void Scanner_AFileThatIsStillOpenInAnotherProgram_IsNotReadyUntilItIsClosed()
    {
        using var world = new ExportFixture();
        world.AddProject();
        var scanner = new ExportScanner(() => world.Clock.Now);

        // TopSolid (or Excel) has the LP file open.
        using (var busy = new FileStream(world.PositionPath(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            ScanResult scan = scanner.Scan(world.Export, true);
            Assert.Empty(scan.Ready);
            Assert.True(scan.IsWaiting);
        }

        Assert.Single(scanner.Scan(world.Export, true).Ready);
    }

    [Fact]
    public void Scanner_ALockedFile_GivesUpWithTheNameOfTheFile()
    {
        using var world = new ExportFixture();
        world.AddProject();
        var scanner = new ExportScanner(() => world.Clock.Now);

        using var busy = new FileStream(world.InfoPath(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        scanner.Scan(world.Export, true);
        world.Clock.Advance(TimeSpan.FromMinutes(2));

        GaveUpProject gaveUp = Assert.Single(scanner.Scan(world.Export, true).GaveUp);
        Assert.Contains("Verschuren-P-20-LI.xlsx", gaveUp.Reason);
    }

    [Fact]
    public void Scanner_WithoutTriggerFile_LiAndLpMustStayUnchangedForTenSeconds()
    {
        using var world = new ExportFixture();
        world.AddProject(trigger: false);
        var scanner = new ExportScanner(() => world.Clock.Now);

        ScanResult first = scanner.Scan(world.Export, requireTriggerFile: false);
        Assert.Empty(first.Ready);
        Assert.True(first.IsWaiting);

        world.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Empty(scanner.Scan(world.Export, false).Ready);

        // The file changes: the ten seconds start again.
        File.SetLastWriteTimeUtc(world.InfoPath(), DateTime.UtcNow.AddMinutes(5));
        Assert.Empty(scanner.Scan(world.Export, false).Ready);
        world.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Empty(scanner.Scan(world.Export, false).Ready);

        world.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(scanner.Scan(world.Export, false).Ready);
    }

    [Fact]
    public void Scanner_WithoutTriggerFile_OneFileAloneIsNotEnough_AndNobodyWaitsForIt()
    {
        using var world = new ExportFixture();
        world.AddProject(position: false, trigger: false);

        ScanResult scan = new ExportScanner(() => world.Clock.Now).Scan(world.Export, requireTriggerFile: false);

        Assert.Empty(scan.Ready);
        Assert.False(scan.IsWaiting);
    }

    [Fact]
    public void Scanner_AFolderThatIsNotThere_GivesAProblemText_NotAnException()
    {
        using var world = new ExportFixture();

        ScanResult scan = new ExportScanner().Scan(Path.Combine(world.Export, "bestaat-niet"), requireTriggerFile: true);

        Assert.NotNull(scan.FolderProblem);
        Assert.Contains("bestaat-niet", scan.FolderProblem);
        Assert.Empty(scan.Ready);
    }

    // ================================================================ the processor: convert and put the files away

    [Fact]
    public void Processor_Success_WritesTheBatch_AndMovesLiLpAndTrToVerwerkt_ButNotTheCncFiles()
    {
        using var world = new ExportFixture();
        world.AddProject();
        string cnc = Path.Combine(world.Export, "Verschuren-P-20_White_18_01.xcs");
        File.WriteAllText(cnc, "cnc");

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.True(outcome.Success, outcome.Message);
        Assert.StartsWith("11 platen, 63 labels", outcome.Message);
        Assert.Equal(10, outcome.Warnings.Count);          // 11 CNC files expected, one of them exists
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));
        Assert.Equal(11, Directory.GetFiles(world.Label, "*.csv").Length);

        string done = world.StampFolder("_Verwerkt");
        Assert.Equal(done, outcome.MovedTo);
        Assert.True(File.Exists(Path.Combine(done, "Verschuren-P-20-LI.xlsx")));
        Assert.True(File.Exists(Path.Combine(done, "Verschuren-P-20-LP.xlsx")));
        Assert.True(File.Exists(Path.Combine(done, "Verschuren-P-20-TR.xlsx")));
        Assert.False(File.Exists(world.InfoPath()));       // not in the export folder anymore
        Assert.True(File.Exists(cnc));                     // the XML points to the CNC file: it stays
    }

    [Fact]
    public void Processor_MessageAndFolderName_UseAFixedFormat_NotTheRegionalSettings()
    {
        using var world = new ExportFixture();
        world.AddProject();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.EndsWith(@"_Verwerkt\20260919-140000 Verschuren-P-20", outcome.MovedTo);
    }

    [Fact]
    public void Processor_AnError_MovesTheFilesToFout_WithFoutTxtWithEveryProblem()
    {
        using var world = new ExportFixture("TopSolidMaterial;DssMaterial;Thickness;Grain\r\n"); // an empty materials table
        world.AddProject();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.False(outcome.IsTemporary);
        Assert.Contains("Paars_18", outcome.Message);
        Assert.Empty(Directory.GetFiles(world.Batch));     // nothing written
        Assert.Empty(Directory.GetFiles(world.Label));

        string folder = world.StampFolder("_Fout");
        Assert.Equal(folder, outcome.MovedTo);
        Assert.True(File.Exists(Path.Combine(folder, "Verschuren-P-20-LI.xlsx")));
        Assert.True(File.Exists(Path.Combine(folder, "Verschuren-P-20-TR.xlsx")));
        Assert.False(File.Exists(world.InfoPath()));

        string report = File.ReadAllText(Path.Combine(folder, "fout.txt"));
        Assert.Contains("Projectnaam: Verschuren-P-20", report);
        Assert.Contains("Tijdstip: 2026-09-19 14:00:00", report);
        foreach (string material in new[] { "Paars_18", "White_18", "White_9" })
        {
            Assert.Contains($"Materiaal '{material}' staat niet in materials.csv.", report); // all problems, one per line
        }
        Assert.Contains("Corrigeer het probleem", report);
    }

    [Theory]
    [InlineData(AppLanguage.Dutch, "Wat er mis is:", "staat niet in materials.csv")]
    [InlineData(AppLanguage.French, "Ce qui ne va pas :", "ne figure pas dans materials.csv")]
    [InlineData(AppLanguage.English, "What is wrong:", "is not in materials.csv")]
    public void FoutTxt_IsWrittenInTheSelectedLanguage(AppLanguage language, string heading, string problem)
    {
        using var scope = new LanguageScope(language);
        using var world = new ExportFixture("TopSolidMaterial;DssMaterial;Thickness;Grain\r\n");
        world.AddProject();

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        string report = File.ReadAllText(Path.Combine(outcome.MovedTo!, "fout.txt"));
        Assert.Contains(heading, report);
        Assert.Contains(problem, report);
    }

    [Fact]
    public void Processor_TheDuivesteinFolderIsNotReachable_IsTemporary_TheFilesStayAndNothingIsMoved()
    {
        using var world = new ExportFixture();
        world.AddProject();
        world.Settings.BatchFolder = Path.Combine(world.Batch, "weg");   // like Z: that is not connected

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.True(outcome.IsTemporary);
        Assert.Contains("automatisch opnieuw", outcome.Message);
        Assert.True(File.Exists(world.InfoPath()));
        Assert.True(File.Exists(world.TriggerPath()));
        Assert.False(Directory.Exists(Path.Combine(world.Export, "_Fout")));
        Assert.Empty(Directory.GetFiles(world.Label));                    // the label files were not written either
    }

    [Fact]
    public void Processor_MaterialsFileMissing_IsTemporary_TheFilesAreFine()
    {
        using var world = new ExportFixture();
        world.AddProject();
        File.Delete(world.MaterialsFile);

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.True(outcome.IsTemporary);
        Assert.True(File.Exists(world.InfoPath()));
    }

    [Fact]
    public void Processor_ABatchThatAlreadyExists_IsAnError_AndIsNeverOverwritten()
    {
        using var world = new ExportFixture();
        world.AddProject();
        string existing = Path.Combine(world.Batch, "Verschuren-P-20.xml");
        File.WriteAllText(existing, "the warehouse is running this job");

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.False(outcome.IsTemporary);
        Assert.Contains("Batch bestaat al", outcome.Message);
        Assert.Equal("the warehouse is running this job", File.ReadAllText(existing));
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), "fout.txt")));
    }

    [Fact]
    public void Processor_AFileThatIsNotAnXlsx_IsAnError_NotACrash()
    {
        using var world = new ExportFixture();
        world.AddProject();
        File.WriteAllText(world.InfoPath(), "this is not an xlsx file");

        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);

        Assert.False(outcome.Success);
        Assert.Contains("Onverwachte fout", outcome.Message);
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), "fout.txt")));
    }

    [Fact]
    public void Processor_Reject_IsUsedForAProjectThatGaveUpWaiting()
    {
        using var world = new ExportFixture();
        world.AddProject(position: false);

        var problems = new[] { "Na 2 minuten wachten ... LP-bestand ontbreekt" };
        ProcessOutcome outcome = new ProjectProcessor(world.MaterialsFile).Reject(world.Files(), world.Settings, problems, world.Clock.Now);

        Assert.False(outcome.Success);
        string folder = world.StampFolder("_Fout");
        Assert.True(File.Exists(Path.Combine(folder, "Verschuren-P-20-LI.xlsx")));   // only the files that exist
        Assert.True(File.Exists(Path.Combine(folder, "Verschuren-P-20-TR.xlsx")));
        Assert.Contains("LP-bestand ontbreekt", File.ReadAllText(Path.Combine(folder, "fout.txt")));
    }

    [Fact]
    public void Processor_WhenTheFilesCannotBeMovedAfterTheConversion_ItIsStillASuccess_AndTheMoveIsRetried()
    {
        using var world = new ExportFixture();
        world.AddProject();

        ProcessOutcome outcome;
        FileStream reader = new(world.PositionPath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite); // no FileShare.Delete: blocks the move
        try
        {
            outcome = new ProjectProcessor(world.MaterialsFile).Process(world.Files(), world.Settings, world.Clock.Now);
        }
        finally
        {
            reader.Dispose();
        }

        Assert.True(outcome.Success, outcome.Message);
        Assert.True(outcome.MovePending);
        Assert.Contains(outcome.Warnings, w => w.Contains("_Verwerkt"));
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));   // the batch IS written

        // Later the file is free: only what is left is moved.
        string folder = ProjectProcessor.MoveProcessedFiles(world.Files(), world.Export, world.Clock.Now);
        Assert.True(File.Exists(Path.Combine(folder, "Verschuren-P-20-LP.xlsx")));
        Assert.Empty(world.Files().ExistingFiles());
    }

    // ================================================================ the watcher: one round at a time (RunOnce)

    private sealed class Events
    {
        public List<string> Started { get; } = new();
        public List<ProcessOutcome> Finished { get; } = new();
        public List<string> FolderProblems { get; } = new();
        public int FolderSolved { get; set; }

        public Events(ExportWatcher watcher)
        {
            watcher.ConversionStarted += project => Started.Add(project);
            watcher.ConversionFinished += outcome => Finished.Add(outcome);
            watcher.FolderProblemFound += message => FolderProblems.Add(message);
            watcher.FolderProblemSolved += () => FolderSolved++;
        }
    }

    [Fact]
    public void Watcher_ConvertsACompleteProject_AndReportsStartAndFinish()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject();

        watcher.RunOnce();

        Assert.Equal(new[] { "Verschuren-P-20" }, events.Started);
        ProcessOutcome outcome = Assert.Single(events.Finished);
        Assert.True(outcome.Success, outcome.Message);
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));

        // A second round finds nothing more to do: the files are moved away.
        watcher.RunOnce();
        Assert.Single(events.Finished);
    }

    [Fact]
    public void Watcher_WhenPaused_ScansNothing_ButScanNowForcesIt()
    {
        using var world = new ExportFixture();
        bool paused = true;
        using var watcher = world.NewWatcher(() => paused);
        var events = new Events(watcher);
        world.AddProject();

        watcher.RunOnce();
        Assert.Empty(events.Finished);

        watcher.RunOnce(force: true); // "Nu scannen"
        Assert.Single(events.Finished);
    }

    [Fact]
    public void Watcher_AfterResume_ConvertsWhatArrivedDuringThePause()
    {
        using var world = new ExportFixture();
        bool paused = true;
        using var watcher = world.NewWatcher(() => paused);
        var events = new Events(watcher);
        world.AddProject();
        watcher.RunOnce();

        paused = false;
        watcher.RunOnce();

        Assert.Single(events.Finished);
    }

    [Fact]
    public void Watcher_WaitsQuietlyForMissingFiles_ThenGivesUpWithAnError()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject(position: false);

        watcher.RunOnce();
        world.Clock.Advance(TimeSpan.FromSeconds(60));
        watcher.RunOnce();
        Assert.Empty(events.Finished);                          // still waiting: no error, no popup

        world.Clock.Advance(TimeSpan.FromSeconds(60));
        watcher.RunOnce();

        ProcessOutcome outcome = Assert.Single(events.Finished);
        Assert.False(outcome.Success);
        Assert.Contains("LP", outcome.Message);
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Fout"), "fout.txt")));
        Assert.False(File.Exists(world.TriggerPath()));
    }

    [Fact]
    public void Watcher_ATemporaryProblem_IsReportedOnce_Retried_AndRecovers()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject();
        string realBatchFolder = world.Settings.BatchFolder;
        world.Settings.BatchFolder = Path.Combine(world.Batch, "z-drive-is-weg");

        watcher.RunOnce();
        watcher.RunOnce();
        watcher.RunOnce();

        Assert.Equal(3, events.Finished.Count);                 // it tried three times ...
        Assert.False(events.Finished[0].IsRepeat);              // ... but the user is told only the first time
        Assert.True(events.Finished[1].IsRepeat);
        Assert.True(events.Finished[2].IsRepeat);
        Assert.True(File.Exists(world.InfoPath()));             // the files waited

        world.Settings.BatchFolder = realBatchFolder;            // Z: is back
        watcher.RunOnce();

        Assert.True(events.Finished[3].Success);
        Assert.False(events.Finished[3].IsRepeat);
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));
    }

    [Fact]
    public void Watcher_ADifferentProblemOnTheSameProject_IsReportedAgain()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject();
        world.Settings.BatchFolder = Path.Combine(world.Batch, "weg-1");
        watcher.RunOnce();

        world.Settings.BatchFolder = Path.Combine(world.Batch, "weg-2"); // another text
        watcher.RunOnce();

        Assert.False(events.Finished[0].IsRepeat);
        Assert.False(events.Finished[1].IsRepeat);
    }

    [Fact]
    public void Watcher_AnExportFolderThatIsNotThere_DoesNotCrash_ReportsAfterAMinute_AndRecovers()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        string realExport = world.Settings.TopSolidExportPath;
        world.Settings.TopSolidExportPath = Path.Combine(realExport, "z-drive");   // not there (yet)

        watcher.RunOnce();
        world.Clock.Advance(TimeSpan.FromSeconds(30));
        watcher.RunOnce();
        Assert.Empty(events.FolderProblems);                    // just after startup: give the network drive time

        world.Clock.Advance(TimeSpan.FromSeconds(31));
        watcher.RunOnce();
        watcher.RunOnce();
        Assert.Single(events.FolderProblems);                   // once, not on every scan
        Assert.Contains("z-drive", events.FolderProblems[0]);
        Assert.Equal(0, events.FolderSolved);

        Directory.CreateDirectory(world.Settings.TopSolidExportPath);   // the drive is connected
        watcher.RunOnce();
        Assert.Equal(1, events.FolderSolved);
    }

    [Fact]
    public void Watcher_AFolderThatComesBackBeforeTheGraceTime_NeverBotheredTheUser()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        string realExport = world.Settings.TopSolidExportPath;
        world.Settings.TopSolidExportPath = Path.Combine(realExport, "z-drive");

        watcher.RunOnce();
        Directory.CreateDirectory(world.Settings.TopSolidExportPath);
        world.Clock.Advance(TimeSpan.FromSeconds(10));
        watcher.RunOnce();

        Assert.Empty(events.FolderProblems);
        Assert.Equal(0, events.FolderSolved);
    }

    [Fact]
    public void Watcher_NeverConvertsTwice_WhenTheFilesCouldNotBeMovedYet()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject();

        // A FILE with the name of the target folder: the conversion works, but the files cannot be moved.
        // (A locked file would not do: the scanner does not start a conversion on a locked file.)
        string blocker = Path.Combine(world.Export, "_Verwerkt");
        File.WriteAllText(blocker, "in the way");

        watcher.RunOnce();
        watcher.RunOnce();   // the files are still there, but the batch must not be converted again

        Assert.Single(events.Finished);
        Assert.True(events.Finished[0].Success);
        Assert.True(events.Finished[0].MovePending);

        File.Delete(blocker);
        watcher.RunOnce();       // the way is free now: the move is done, and still no second conversion

        Assert.Single(events.Finished);
        Assert.False(File.Exists(world.InfoPath()));
        Assert.True(File.Exists(Path.Combine(world.StampFolder("_Verwerkt"), "Verschuren-P-20-LP.xlsx")));
    }

    [Fact]
    public void Watcher_WithoutTriggerFile_ConvertsAfterTenQuietSeconds()
    {
        using var world = new ExportFixture();
        world.Settings.RequireTriggerFile = false;
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject(trigger: false);

        watcher.RunOnce();
        Assert.Empty(events.Finished);

        world.Clock.Advance(TimeSpan.FromSeconds(10));
        watcher.RunOnce();

        Assert.True(Assert.Single(events.Finished).Success);
    }

    [Fact]
    public void Watcher_OneProjectWithAnError_DoesNotStopTheNextProject()
    {
        using var world = new ExportFixture();
        using var watcher = world.NewWatcher();
        var events = new Events(watcher);
        world.AddProject("A-Kapot");
        File.WriteAllText(world.InfoPath("A-Kapot"), "not an xlsx");
        world.AddProject("B-Goed");

        watcher.RunOnce();

        Assert.Equal(2, events.Finished.Count);
        Assert.False(events.Finished.Single(o => o.Project == "A-Kapot").Success);
        Assert.True(events.Finished.Single(o => o.Project == "B-Goed").Success);
        Assert.True(File.Exists(Path.Combine(world.Batch, "B-Goed.xml")));
    }

    [Fact]
    public void Watcher_WritesWarningsAndResultsToTheLog()
    {
        using var world = new ExportFixture();
        var log = new LogWriter(Path.Combine(world.Export, "..", "logs2"));
        using var watcher = new ExportWatcher(() => world.Settings, () => false, log, world.MaterialsFile, () => world.Clock.Now);
        world.AddProject();

        watcher.RunOnce();

        string text = File.ReadAllText(log.CurrentLogFile);
        Assert.Contains("Conversie gestart: Verschuren-P-20", text);
        Assert.Contains("Waarschuwing bij Verschuren-P-20: CNC-programma niet gevonden", text);
        Assert.Contains("Bestanden verplaatst naar", text);
    }

    // ================================================================ the real thing: thread + FileSystemWatcher

    [Fact]
    public void Watcher_OnItsOwnThread_ConvertsAProjectThatAppearsWhileItRuns()
    {
        using var world = new ExportFixture();
        using var watcher = new ExportWatcher(() => world.Settings, () => false, new LogWriter(Path.Combine(world.Export, "..", "logs3")), world.MaterialsFile);
        using var finished = new ManualResetEventSlim();
        ProcessOutcome? outcome = null;
        watcher.ConversionFinished += result => { outcome = result; finished.Set(); };

        watcher.Start();
        Thread.Sleep(500);                       // the first scan finds an empty folder
        world.AddProject();                      // the FileSystemWatcher (or the rescan) wakes the thread

        Assert.True(finished.Wait(TimeSpan.FromSeconds(30)), "no conversion within 30 seconds");
        Assert.True(outcome!.Success, outcome.Message);
        Assert.True(File.Exists(Path.Combine(world.Batch, "Verschuren-P-20.xml")));
    }

    [Fact]
    public void Watcher_OnItsOwnThread_ScanNowWorksWhilePaused_AndDisposeStopsTheThread()
    {
        using var world = new ExportFixture();
        var watcher = new ExportWatcher(() => world.Settings, () => true, new LogWriter(Path.Combine(world.Export, "..", "logs4")), world.MaterialsFile);
        using var finished = new ManualResetEventSlim();
        watcher.ConversionFinished += result => finished.Set();
        world.AddProject();

        watcher.Start();
        Assert.False(finished.Wait(TimeSpan.FromSeconds(1)), "paused: nothing may be converted");

        watcher.ScanNow();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(30)), "Nu scannen did not convert");

        watcher.Dispose();                       // must return, also when called twice
        watcher.Dispose();
    }
}
