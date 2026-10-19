using System.Text.Json;
using DocTemplater.Domain;
using DocTemplater.Services;

namespace DocTemplater.Tests.Services;

public sealed class ApplicationDataSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DocTemplater-Safety-" + Guid.NewGuid().ToString("N"));
    private string Data => Path.Combine(_root, "data");
    private string Install => Path.Combine(_root, "install");
    private string Settings => Path.Combine(Data, "settings.json");
    public ApplicationDataSafetyTests() { Directory.CreateDirectory(Data); Directory.CreateDirectory(Install); }
    private ApplicationDataSafety Safety(Action? guard = null) => new(Data, new[] { Install }, Install, guard ?? (() => { }));
    private Task Write(AppSettings settings) => File.WriteAllTextAsync(Settings, JsonSerializer.Serialize(settings));

    [Fact]
    public async Task CorruptSettingsBlockEveryPatchAndMigrationWithoutChangingBytes()
    {
        const string broken = "{broken json";
        await File.WriteAllTextAsync(Settings, broken);
        var persistence = new ValuePersistenceService(Settings);
        await Assert.ThrowsAsync<InvalidDataException>(() => Safety().BootstrapAsync(persistence));
        await Assert.ThrowsAsync<InvalidDataException>(() => persistence.SaveTemplatesDirectoryAsync("replacement"));
        await Assert.ThrowsAsync<InvalidDataException>(() => persistence.SaveTemplateValuesAsync("t.docx", new()));
        Assert.Equal(broken, await File.ReadAllTextAsync(Settings));
        Assert.False(Directory.Exists(Path.Combine(Data, "Templates")));
    }

    [Fact]
    public async Task ManagedTemplatesAndGeneratedDocumentsMoveWithHashesAndOriginals()
    {
        var templates = Path.Combine(Install, "Templates");
        Directory.CreateDirectory(Path.Combine(templates, "sub"));
        await File.WriteAllTextAsync(Path.Combine(templates, "sub", "contract.docx"), "user template");
        await File.WriteAllTextAsync(Path.Combine(Install, "result.docx"), "generated document");
        await File.WriteAllTextAsync(Path.Combine(Install, "DocTemplater.exe"), "binary");
        await Write(new AppSettings { TemplatesFolderPath = templates, Templates = new() { new() { TemplateFileName = "t.docx", LastSaveDirectory = Install, FieldValues = new() { ["Имя"] = "сохранено" } } } });
        var result = await Safety().BootstrapAsync(new ValuePersistenceService(Settings));
        Assert.Equal(Path.Combine(Data, "Templates"), result.TemplatesFolderPath);
        Assert.Equal("user template", await File.ReadAllTextAsync(Path.Combine(result.TemplatesFolderPath, "sub", "contract.docx")));
        Assert.True(File.Exists(Path.Combine(templates, "sub", "contract.docx")));
        Assert.Equal("generated document", await File.ReadAllTextAsync(Path.Combine(result.Templates[0].LastSaveDirectory!, "result.docx")));
        Assert.False(File.Exists(Path.Combine(result.Templates[0].LastSaveDirectory!, "DocTemplater.exe")));
        Assert.Single(Directory.GetFiles(Data, "settings.json.before-path-migration-*.bak"));
        Assert.Equal("сохранено", result.Templates[0].FieldValues["Имя"].ToString());
    }

    [Fact]
    public async Task DifferentCollisionNeverOverwritesEitherFileOrSettings()
    {
        var source = Path.Combine(Install, "Templates");
        var target = Path.Combine(Data, "Templates");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "t.docx"), "original");
        await File.WriteAllTextAsync(Path.Combine(target, "t.docx"), "different");
        await Write(new AppSettings { TemplatesFolderPath = source });
        var before = await File.ReadAllTextAsync(Settings);
        await Assert.ThrowsAsync<IOException>(() => Safety().BootstrapAsync(new ValuePersistenceService(Settings)));
        Assert.Equal(before, await File.ReadAllTextAsync(Settings));
        Assert.Equal("different", await File.ReadAllTextAsync(Path.Combine(target, "t.docx")));
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(source, "t.docx")));
    }

    [Fact]
    public async Task RestartDoesNotReimportStaleAdjacentTemplatesAfterUserEditsMigratedCopy()
    {
        var source = Path.Combine(Install, "Templates"); Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "t.docx"), "original");
        await Write(new AppSettings { TemplatesFolderPath = source });
        var result = await Safety().BootstrapAsync(new ValuePersistenceService(Settings));
        await File.WriteAllTextAsync(Path.Combine(result.TemplatesFolderPath, "t.docx"), "user edit after migration");
        await Safety().BootstrapAsync(new ValuePersistenceService(Settings));
        Assert.Equal("user edit after migration", await File.ReadAllTextAsync(Path.Combine(result.TemplatesFolderPath, "t.docx")));
    }

    [Fact]
    public async Task UnregisteredManagedDocumentIsBackedUpAndNewDocumentAfterStartupBlocksApply()
    {
        await Write(new AppSettings { TemplatesFolderPath = Path.Combine(Data, "Templates") });
        var output = Path.Combine(Install, "unregistered.docx");
        await File.WriteAllTextAsync(output, "not in settings");
        var safety = Safety();
        var settings = await safety.BootstrapAsync(new ValuePersistenceService(Settings));
        Assert.Contains(Directory.GetFiles(Path.Combine(Data, "UpdateBackups"), "*.docx", SearchOption.AllDirectories), file => File.ReadAllText(file) == "not in settings");
        safety.EnsureSafeForUpdate(settings, null);
        await File.WriteAllTextAsync(output, "written by legacy process after startup");
        Assert.Throws<IOException>(() => safety.EnsureSafeForUpdate(settings, null));
        Assert.Throws<IOException>(() => safety.EnsureSafeUserDirectory(Install));
    }

    [Fact]
    public async Task FolderRecoveryPreservesAllOriginalTemplateFilesWithNamedManifest()
    {
        var source = Path.Combine(Install, "Templates"); var target = Path.Combine(Data, "Templates");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(source, "t.docx"), "original");
        await File.WriteAllTextAsync(Path.Combine(source, "instructions.txt"), "user notes");
        await File.WriteAllTextAsync(Path.Combine(target, "t.docx"), "different");
        await Write(new AppSettings { TemplatesFolderPath = source });
        var safety = Safety(); var persistence = new ValuePersistenceService(Settings);
        await Assert.ThrowsAsync<IOException>(() => safety.BootstrapAsync(persistence));
        var external = Path.Combine(_root, "chosen"); Directory.CreateDirectory(external);
        await persistence.SaveTemplatesDirectoryAsync(external);
        var result = await safety.BootstrapAsync(persistence, importAdjacentTemplates: false);
        safety.EnsureSafeForUpdate(result, null);
        var manifest = await File.ReadAllTextAsync(Path.Combine(Data, "UpdateBackups", "manifest.json"));
        Assert.Contains("instructions.txt", manifest);
        Assert.Contains(Directory.GetFiles(Path.Combine(Data, "UpdateBackups"), "instructions.txt", SearchOption.AllDirectories), file => File.ReadAllText(file) == "user notes");
    }

    [Fact]
    public async Task UnreadableCustomTemplateTreeRemainsRegisteredThroughFolderRecovery()
    {
        var source = Path.Combine(Install, "MyTemplates"); Directory.CreateDirectory(source);
        var notes = Path.Combine(source, "instructions.txt");
        await File.WriteAllTextAsync(notes, "custom template instructions");
        await Write(new AppSettings { TemplatesFolderPath = source });
        var safety = Safety(); var persistence = new ValuePersistenceService(Settings);
        using (var locked = new FileStream(notes, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => safety.BootstrapAsync(persistence));
            await safety.PreserveCurrentUserDirectoriesAsync(persistence);
            var external = Path.Combine(_root, "external"); Directory.CreateDirectory(external);
            await persistence.SaveTemplatesDirectoryAsync(external);
            await Assert.ThrowsAsync<IOException>(() => safety.BootstrapAsync(persistence, importAdjacentTemplates: false));
        }
        var settings = await safety.BootstrapAsync(persistence, importAdjacentTemplates: false);
        safety.EnsureSafeForUpdate(settings, null);
        Assert.Contains(Directory.GetFiles(Path.Combine(Data, "UpdateBackups"), "instructions.txt", SearchOption.AllDirectories), file => File.ReadAllText(file) == "custom template instructions");
    }

    [Fact]
    public async Task LegacyProcessGuardRunsBeforeAnySettingsWrite()
    {
        await Assert.ThrowsAsync<IOException>(() => Safety(() => throw new IOException("other process")).BootstrapAsync(new ValuePersistenceService(Settings)));
        Assert.False(File.Exists(Settings));
    }

    [Fact]
    public async Task ExplicitMissingExternalFolderIsPreserved()
    {
        var external = Path.Combine(_root, "unavailable");
        await Write(new AppSettings { TemplatesFolderPath = external });
        var result = await Safety().BootstrapAsync(new ValuePersistenceService(Settings));
        Assert.Equal(external, result.TemplatesFolderPath);
    }

    [Fact]
    public void PathBoundariesAndNonexistentDescendantsAreChecked()
    {
        Assert.True(CanonicalPath.IsWithin(Path.Combine(Install, "missing", "child"), Install));
        Assert.False(CanonicalPath.IsWithin(Install + "-external", Install));
        Assert.Throws<IOException>(() => Safety().EnsureSafeForUpdate(new AppSettings { TemplatesFolderPath = Data }, Path.Combine(Install, "new")));
    }

    [Fact]
    public async Task DirectoryJunctionAncestorCannotHideManagedOutputOrCopyCycle()
    {
        if (!OperatingSystem.IsWindows()) return;
        var alias = Path.Combine(_root, "alias");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(alias); start.ArgumentList.Add(Install);
        using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.True(CanonicalPath.IsWithin(Path.Combine(alias, "not-yet-created"), Install));
            Assert.Throws<IOException>(() => Safety().EnsureSafeForUpdate(new AppSettings { TemplatesFolderPath = Data }, Path.Combine(alias, "not-yet-created")));
            var templates = Path.Combine(Install, "Templates"); Directory.CreateDirectory(templates);
            Directory.CreateSymbolicLink(Path.Combine(templates, "cycle"), templates);
            await Write(new AppSettings { TemplatesFolderPath = templates });
            await Assert.ThrowsAsync<IOException>(() => Safety().BootstrapAsync(new ValuePersistenceService(Settings)));
            Directory.Delete(Path.Combine(templates, "cycle"));
        }
        finally { Directory.Delete(alias); }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
