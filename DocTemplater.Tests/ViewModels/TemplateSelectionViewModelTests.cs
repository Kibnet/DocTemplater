using System.Reactive.Threading.Tasks;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using Moq;

namespace DocTemplater.Tests.ViewModels;

public class TemplateSelectionViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DocTemplater-folder-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task OpenDirectory_UsesActivePathWithoutSelectedTemplate_AndAfterChangingDirectory()
    {
        Directory.CreateDirectory(_directory);
        var other = Path.Combine(_directory, "Шаблоны с пробелами");
        Directory.CreateDirectory(other);
        var current = _directory;
        var discovery = new Mock<ITemplateDiscoveryService>();
        discovery.SetupGet(d => d.TemplatesDirectory).Returns(() => current);
        discovery.Setup(d => d.DiscoverTemplatesAsync()).ReturnsAsync(new List<Template>());
        discovery.Setup(d => d.SetTemplatesDirectory(It.IsAny<string>())).Callback<string>(path => current = path);
        var opener = new Mock<IDirectoryOpener>();
        var vm = new TemplateSelectionViewModel(discovery.Object, Mock.Of<IPlaceholderExtractionService>(), directoryOpener: opener.Object);
        await vm.LoadTemplatesCommand.Execute().ToTask();
        Assert.Null(vm.SelectedTemplate);
        Assert.Empty(vm.Templates);
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(_directory), Times.Once);

        vm.TemplatesDirectory = Path.Combine(_directory, "несуществующий черновик");
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(_directory), Times.Exactly(2));
        Assert.Contains(_directory, vm.StatusMessage);

        vm.BrowseForFolderAsync = () => Task.FromResult<string?>(other);
        await vm.ChangeTemplatesDirectoryAsync();
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(other), Times.Once);
        Assert.Equal(other, vm.ActiveTemplatesDirectory);
    }

    [Fact]
    public async Task OpenDirectory_MissingFolderAndShellFailure_ReportErrorsAndStayUsable()
    {
        Directory.CreateDirectory(_directory);
        var discovery = new Mock<ITemplateDiscoveryService>();
        discovery.SetupGet(d => d.TemplatesDirectory).Returns(_directory);
        discovery.Setup(d => d.DiscoverTemplatesAsync()).ReturnsAsync(new List<Template>());
        var opener = new Mock<IDirectoryOpener>();
        opener.Setup(o => o.Open(_directory)).Throws(new InvalidOperationException("shell unavailable"));
        var vm = new TemplateSelectionViewModel(discovery.Object, Mock.Of<IPlaceholderExtractionService>(), directoryOpener: opener.Object);
        await vm.LoadTemplatesCommand.Execute().ToTask();
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        Assert.Contains("Ошибка при открытии папки", vm.StatusMessage);
        Directory.Delete(_directory);
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        Assert.Contains("не существует", vm.StatusMessage);
        opener.Verify(o => o.Open(It.IsAny<string>()), Times.Once);
        vm.IsLoading = true;
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DirectoryChanges_UseAtomicFieldUpdateInsteadOfSavingAFullSettingsSnapshot()
    {
        Directory.CreateDirectory(_directory);
        var manual = Path.Combine(_directory, "Ручной путь");
        var picked = Path.Combine(_directory, "Выбранный путь");
        Directory.CreateDirectory(manual);
        Directory.CreateDirectory(picked);
        var active = _directory;
        var discovery = new Mock<ITemplateDiscoveryService>();
        discovery.SetupGet(d => d.TemplatesDirectory).Returns(() => active);
        discovery.Setup(d => d.SetTemplatesDirectory(It.IsAny<string>())).Callback<string>(path => active = path);
        discovery.Setup(d => d.DiscoverTemplatesAsync()).ReturnsAsync(new List<Template>());
        var persistence = new Mock<IValuePersistenceService>();
        persistence.Setup(p => p.LoadSettingsAsync()).ReturnsAsync(new AppSettings { TemplatesFolderPath = _directory });
        persistence.Setup(p => p.SaveTemplatesDirectoryAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var vm = new TemplateSelectionViewModel(discovery.Object, Mock.Of<IPlaceholderExtractionService>(), persistence.Object);
        vm.TemplatesDirectory = manual;
        vm.BrowseForFolderAsync = () => Task.FromResult<string?>(picked);
        await vm.ChangeTemplatesDirectoryAsync();

        persistence.Verify(p => p.SaveTemplatesDirectoryAsync(manual), Times.Once);
        persistence.Verify(p => p.SaveTemplatesDirectoryAsync(picked), Times.Once);
        persistence.Verify(p => p.SaveSettingsAsync(It.IsAny<AppSettings>()), Times.Never);
        Assert.Equal(picked, vm.ActiveTemplatesDirectory);
    }

    [Fact]
    public async Task OpenDirectory_IsUnavailableWhileFolderPickerIsPending_AndPickerFailureIsReported()
    {
        Directory.CreateDirectory(_directory);
        var discovery = new Mock<ITemplateDiscoveryService>();
        discovery.SetupGet(d => d.TemplatesDirectory).Returns(_directory);
        discovery.Setup(d => d.DiscoverTemplatesAsync()).ReturnsAsync(new List<Template>());
        var opener = new Mock<IDirectoryOpener>();
        var vm = new TemplateSelectionViewModel(discovery.Object, Mock.Of<IPlaceholderExtractionService>(), directoryOpener: opener.Object);
        await vm.LoadTemplatesCommand.Execute().ToTask();
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.BrowseForFolderAsync = () => picker.Task;
        var changing = vm.ChangeTemplatesDirectoryAsync();
        Assert.True(vm.IsChangingDirectory);
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(It.IsAny<string>()), Times.Never);
        picker.SetException(new IOException("picker unavailable"));
        await changing;
        Assert.False(vm.IsChangingDirectory);
        Assert.Contains("Ошибка при изменении директории", vm.StatusMessage);
        await vm.OpenTemplatesDirectoryCommand.Execute().ToTask();
        opener.Verify(o => o.Open(_directory), Times.Once);
    }

    [Fact]
    public void Discovery_NormalizesRelativePaths_AndReportsChangedDirectory()
    {
        Directory.CreateDirectory(_directory);
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, _directory);
        using var discovery = new TemplateDiscoveryService(relative);
        Assert.Equal(_directory, discovery.TemplatesDirectory);
        var other = Path.Combine(_directory, "другой");
        discovery.SetTemplatesDirectory(other);
        Assert.Equal(other, discovery.TemplatesDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
