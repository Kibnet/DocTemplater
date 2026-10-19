using System.Reactive.Threading.Tasks;
using System.Reactive.Linq;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using Moq;

namespace DocTemplater.Tests.ViewModels;

public class SavedValuesEditingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DocTemplater-edit-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");
    private static Template Template => new()
    {
        FileName = "Договор",
        FilePath = Path.Combine(Path.GetTempPath(), "Договор.docx"),
        Placeholders = new()
        {
            new() { Name = "Имя", DataType = PlaceholderDataType.String },
            new() { Name = "Количество", DataType = PlaceholderDataType.Integer },
            new() { Name = "Дата", DataType = PlaceholderDataType.DateTimeOffset }
        }
    };

    [Fact]
    public async Task SaveValues_RoundTripWithoutDocument_PreservesMetadataHiddenKeysAndOtherTemplates()
    {
        var service = new ValuePersistenceService(SettingsPath);
        var previousDate = DateTimeOffset.Now.AddDays(-1);
        await service.SaveSettingsAsync(new AppSettings
        {
            CultureName = "ru-RU",
            Templates = new()
            {
                new() { TemplateFileName = "Другой.docx", FieldValues = new() { ["Имя"] = "Другое" } },
                new()
                {
                    TemplateFileName = "Договор.docx", LastUsed = previousDate,
                    LastSaveDirectory = _directory, OutputFileNameTemplate = "Акт_{Имя}",
                    FieldValues = new() { ["Имя"] = "Раньше", ["Скрытое поле"] = "Оставить" }
                }
            }
        });
        var generator = new Mock<IDocumentGenerationService>(MockBehavior.Strict);
        var vm = await CreateLoadedAsync(service, generator.Object);
        vm.OutputDirectory = Path.Combine(_directory, "несуществующий");
        vm.OutputFileNameTemplate = "";
        vm.OpenAfterSave = true;
        vm.BrowseForSaveFileAsync = _ => throw new Exception("Не нужен диалог");
        vm.ConfirmFileOverwriteAsync = _ => throw new Exception("Не нужна перезапись");
        var navigated = false;
        vm.DocumentSaved += (_, _) => navigated = true;
        Fill(vm);

        await vm.SaveValuesCommand.Execute().ToTask();

        Assert.Equal("Значения сохранены", vm.StatusMessage);
        Assert.False(navigated);
        generator.VerifyNoOtherCalls();
        Assert.Empty(Directory.GetFiles(_directory, "*.docx"));
        var fresh = new ValuePersistenceService(SettingsPath);
        var saved = (await fresh.GetSavedValuesAsync("Договор.docx"))!;
        Assert.Equal(_directory, saved.LastSaveDirectory);
        Assert.Equal("Акт_{Имя}", saved.OutputFileNameTemplate);
        Assert.Equal("Оставить", saved.FieldValues["Скрытое поле"].ToString());
        Assert.True(saved.LastUsed > previousDate);
        Assert.Equal("Другое", (await fresh.GetSavedValuesAsync("Другой.docx"))!.FieldValues["Имя"].ToString());
        Assert.Equal("ru-RU", (await fresh.LoadSettingsAsync()).CultureName);
        var reopened = await CreateLoadedAsync(fresh, generator.Object);
        Assert.Equal("Анна", reopened.Fields[0].ValueText);
        Assert.Equal(12, reopened.Fields[1].IntegerValue);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(3)), reopened.Fields[2].DateValue);
    }

    [Fact]
    public async Task EditingAfterSuccessfulSave_UpdatesStatusWithoutSavingAgain()
    {
        var persistence = new Mock<IValuePersistenceService>();
        persistence.Setup(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null))
            .Returns(Task.CompletedTask);
        var vm = await CreateLoadedAsync(persistence.Object);
        Fill(vm);

        await vm.SaveValuesCommand.Execute().ToTask();
        Assert.Equal("Значения сохранены", vm.StatusMessage);
        vm.Fields[0].ValueText = "Другое имя";
        Assert.Equal("Есть несохранённые изменения", vm.StatusMessage);

        await vm.SaveValuesCommand.Execute().ToTask();
        vm.Fields[1].IntegerValue = 14;
        Assert.Equal("Есть несохранённые изменения", vm.StatusMessage);

        await vm.SaveValuesCommand.Execute().ToTask();
        vm.Fields[2].DateValue = vm.Fields[2].DateValue!.Value.AddDays(1);
        Assert.Equal("Есть несохранённые изменения", vm.StatusMessage);

        await vm.SaveValuesCommand.Execute().ToTask();
        await vm.ClearCommand.Execute().ToTask();
        Assert.Equal("Есть несохранённые изменения", vm.StatusMessage);
        persistence.Verify(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null), Times.Exactly(4));
    }

    [Fact]
    public async Task SaveValues_UntouchedEmptyFields_AreValidatedAndNotWritten()
    {
        var persistence = new Mock<IValuePersistenceService>();
        var vm = await CreateLoadedAsync(persistence.Object);
        await vm.SaveValuesCommand.Execute().ToTask();
        Assert.Contains("Исправьте ошибки в 3 полях", vm.StatusMessage);
        Assert.All(vm.Fields, field => Assert.False(field.IsValid));
        persistence.Verify(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SaveValues_WriteFailureRetainsEditsAndPreviousData_ThenRetrySucceeds()
    {
        var service = new ValuePersistenceService(SettingsPath);
        await service.SaveTemplateValuesAsync("Договор.docx", new() { ["Имя"] = "До" });
        var previousJson = await File.ReadAllTextAsync(SettingsPath);
        var vm = await CreateLoadedAsync(service);
        Fill(vm);
        using (var lockedFile = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await vm.SaveValuesCommand.Execute().ToTask();
            Assert.StartsWith("Ошибка сохранения значений:", vm.StatusMessage);
            Assert.False(vm.IsBusy);
            Assert.Equal("Анна", vm.Fields[0].ValueText);
            Assert.Equal(previousJson, await File.ReadAllTextAsync(SettingsPath));
            Assert.Equal("До", (await service.GetSavedValuesAsync("Договор.docx"))!.FieldValues["Имя"].ToString());
        }
        await vm.SaveValuesCommand.Execute().ToTask();
        Assert.Equal("Значения сохранены", vm.StatusMessage);
    }

    [Fact]
    public async Task SaveValues_InProgress_DisablesOtherActionsAndRejectsConcurrentGeneration()
    {
        var write = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var persistence = new Mock<IValuePersistenceService>();
        persistence.Setup(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null))
            .Returns(write.Task);
        var generator = new Mock<IDocumentGenerationService>(MockBehavior.Strict);
        var vm = await CreateLoadedAsync(persistence.Object, generator.Object);
        Fill(vm);
        var saving = vm.SaveValuesCommand.Execute().ToTask();
        try
        {
            Assert.True(vm.IsBusy);
            Assert.False(vm.BackCommand.CanExecute.FirstAsync().Wait());
            Assert.False(vm.ClearCommand.CanExecute.FirstAsync().Wait());
            Assert.False(vm.SaveCommand.CanExecute.FirstAsync().Wait());
            await vm.SaveCommand.Execute().ToTask();
            await vm.SaveValuesCommand.Execute().ToTask();
            persistence.Verify(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null), Times.Once);
            generator.VerifyNoOtherCalls();
        }
        finally { write.TrySetResult(); await saving; }
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoadingSavedValues_DisablesSavingUntilFieldsHaveLoaded()
    {
        var loading = new TaskCompletionSource<SavedTemplateData?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var persistence = new Mock<IValuePersistenceService>();
        persistence.Setup(p => p.GetSavedValuesAsync(It.IsAny<string>())).Returns(loading.Task);
        var vm = new DataInputViewModel(Template, Mock.Of<IDocumentGenerationService>(), persistence.Object);
        Assert.True(vm.IsBusy);
        await vm.SaveValuesCommand.Execute().ToTask();
        persistence.Verify(p => p.SaveTemplateValuesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null), Times.Never);
        loading.SetResult(null);
        await WaitUntilLoadedAsync(vm);
        Assert.Equal(3, vm.Fields.Count);
    }

    private static void Fill(DataInputViewModel vm)
    {
        vm.Fields[0].ValueText = "Анна";
        vm.Fields[1].IntegerValue = 12;
        vm.Fields[2].DateValue = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(3));
    }

    private static async Task<DataInputViewModel> CreateLoadedAsync(IValuePersistenceService persistence,
        IDocumentGenerationService? generator = null)
    {
        var vm = new DataInputViewModel(Template, generator ?? Mock.Of<IDocumentGenerationService>(), persistence);
        await WaitUntilLoadedAsync(vm);
        return vm;
    }

    private static async Task WaitUntilLoadedAsync(DataInputViewModel vm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (vm.IsBusy) await Task.Delay(5, timeout.Token);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
