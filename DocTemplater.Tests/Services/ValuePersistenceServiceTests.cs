using DocTemplater.Services;
using Xunit;

namespace DocTemplater.Tests.Services;

public class ValuePersistenceServiceTests : IDisposable
{
    private readonly ValuePersistenceService _service;
    private readonly string _testDirectory;

    public ValuePersistenceServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"test_values_{Guid.NewGuid()}");
        _service = new ValuePersistenceService(Path.Combine(_testDirectory, "settings.json"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                // Wait a bit to ensure file handles are released
                Thread.Sleep(100);
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public async Task ConcurrentTemplateSaves_PreserveEverySuccessfulUpdateOnDiskAndInCache()
    {
        await _service.SaveTemplateValuesAsync("Seed.docx", new() { ["Поле"] = "Исходное" });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = Enumerable.Range(0, 16).Select(async index =>
        {
            await start.Task;
            await _service.SaveTemplateValuesAsync($"Template-{index}.docx", new() { ["Поле"] = index });
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(saves);

        var cached = await _service.LoadSettingsAsync();
        var fresh = new ValuePersistenceService(Path.Combine(_testDirectory, "settings.json"));
        var onDisk = await fresh.LoadSettingsAsync();
        Assert.Equal(17, cached.Templates.Count);
        Assert.Equal(17, onDisk.Templates.Count);
        for (var index = 0; index < 16; index++)
        {
            Assert.Equal(index.ToString(), (await fresh.GetSavedValuesAsync($"Template-{index}.docx"))!.FieldValues["Поле"].ToString());
        }
    }

    [Fact]
    public async Task InitialFullSettingsSave_IsOrderedBeforeConcurrentFieldUpdates()
    {
        var initial = new DocTemplater.Domain.AppSettings
        {
            CultureName = "ru-RU",
            Templates = new()
            {
                new() { TemplateFileName = "Seed.docx", FieldValues = new() { ["Поле"] = new string('x', 32768) } }
            }
        };
        var initialize = _service.SaveSettingsAsync(initial);
        var saveValues = _service.SaveTemplateValuesAsync("New.docx", new() { ["Имя"] = "Новое" });
        var saveDirectory = _service.SaveTemplatesDirectoryAsync("New directory");
        await Task.WhenAll(initialize, saveValues, saveDirectory);

        var fresh = new ValuePersistenceService(Path.Combine(_testDirectory, "settings.json"));
        var saved = await fresh.LoadSettingsAsync();
        Assert.Equal(2, saved.Templates.Count);
        Assert.Equal("ru-RU", saved.CultureName);
        Assert.Equal("New directory", saved.TemplatesFolderPath);
        Assert.Equal("Новое", (await fresh.GetSavedValuesAsync("New.docx"))!.FieldValues["Имя"].ToString());
        Assert.Equal(32768, (await fresh.GetSavedValuesAsync("Seed.docx"))!.FieldValues["Поле"].ToString()!.Length);
    }

    [Fact]
    public async Task ConcurrentDirectoryAndTemplateUpdates_PreserveBothChanges()
    {
        await _service.SaveSettingsAsync(new DocTemplater.Domain.AppSettings { CultureName = "ru-RU" });
        var newDirectory = Path.Combine(_testDirectory, "Новый каталог");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = Enumerable.Range(0, 12).Select(async index =>
        {
            await start.Task;
            if (index % 2 == 0)
                await _service.SaveTemplatesDirectoryAsync(newDirectory);
            else
                await _service.SaveTemplateValuesAsync($"Template-{index}.docx", new() { ["Поле"] = index });
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(saves);

        var fresh = new ValuePersistenceService(Path.Combine(_testDirectory, "settings.json"));
        foreach (var service in new[] { _service, fresh })
        {
            var settings = await service.LoadSettingsAsync();
            Assert.Equal(newDirectory, settings.TemplatesFolderPath);
            Assert.Equal("ru-RU", settings.CultureName);
            Assert.Equal(6, settings.Templates.Count);
            foreach (var index in Enumerable.Range(0, 12).Where(index => index % 2 != 0))
                Assert.Equal(index.ToString(), settings.Templates.Single(t => t.TemplateFileName == $"Template-{index}.docx").FieldValues["Поле"].ToString());
        }
    }

    [Fact]
    public async Task PublicReadsAndSaveArguments_AreDetachedFromTheCommittedCache()
    {
        var original = new DocTemplater.Domain.AppSettings { TemplatesFolderPath = "Before" };
        await _service.SaveSettingsAsync(original);
        original.TemplatesFolderPath = "Not saved";
        var date = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(3));
        await _service.SaveTemplateValuesAsync("test.docx", new() { ["Имя"] = "До", ["Количество"] = 3, ["Дата"] = date });

        var snapshot = await _service.LoadSettingsAsync();
        Assert.IsType<string>(snapshot.Templates[0].FieldValues["Имя"]);
        Assert.IsType<int>(snapshot.Templates[0].FieldValues["Количество"]);
        Assert.Equal(date, Assert.IsType<DateTimeOffset>(snapshot.Templates[0].FieldValues["Дата"]));
        snapshot.TemplatesFolderPath = "Also not saved";
        snapshot.Templates.Clear();
        var savedData = (await _service.GetSavedValuesAsync("test.docx"))!;
        savedData.FieldValues["Имя"] = "Не записано";

        Assert.Equal("Before", (await _service.LoadSettingsAsync()).TemplatesFolderPath);
        Assert.Equal("До", (await _service.GetSavedValuesAsync("test.docx"))!.FieldValues["Имя"].ToString());
        var fresh = new ValuePersistenceService(Path.Combine(_testDirectory, "settings.json"));
        Assert.Equal("Before", (await fresh.LoadSettingsAsync()).TemplatesFolderPath);
        Assert.Equal("До", (await fresh.GetSavedValuesAsync("test.docx"))!.FieldValues["Имя"].ToString());
    }

    [Fact]
    public async Task FailedDirectoryWrite_KeepsCommittedStateAndReleasesLockForRetry()
    {
        await _service.SaveTemplateValuesAsync("test.docx", new() { ["Имя"] = "До" });
        var previousPath = (await _service.LoadSettingsAsync()).TemplatesFolderPath;
        var path = Path.Combine(_testDirectory, "settings.json");
        var previousJson = await File.ReadAllTextAsync(path);
        using (var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => _service.SaveTemplatesDirectoryAsync("New"));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Equal(previousPath, (await _service.LoadSettingsAsync().WaitAsync(TimeSpan.FromSeconds(5))).TemplatesFolderPath);
            Assert.Equal(previousJson, await File.ReadAllTextAsync(path));
        }
        await _service.SaveTemplatesDirectoryAsync("New").WaitAsync(TimeSpan.FromSeconds(5));
        var fresh = new ValuePersistenceService(path);
        Assert.Equal("New", (await fresh.LoadSettingsAsync()).TemplatesFolderPath);
        Assert.Equal("До", (await fresh.GetSavedValuesAsync("test.docx"))!.FieldValues["Имя"].ToString());
    }

    [Fact]
    public async Task FailedWrite_KeepsPreviousFileAndCachedValues_AndCanBeRetried()
    {
        await _service.SaveTemplateValuesAsync("test.docx", new() { ["Имя"] = "До" });
        var path = Path.Combine(_testDirectory, "settings.json");
        var previousJson = await File.ReadAllTextAsync(path);

        using (var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => _service.SaveTemplateValuesAsync(
                "test.docx", new() { ["Имя"] = "После" }));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Equal("До", (await _service.GetSavedValuesAsync("test.docx"))!.FieldValues["Имя"].ToString());
            Assert.Equal(previousJson, await File.ReadAllTextAsync(path));
        }

        Assert.Empty(Directory.GetFiles(_testDirectory, "*.tmp"));
        await _service.SaveTemplateValuesAsync("test.docx", new() { ["Имя"] = "После" });
        var freshService = new ValuePersistenceService(path);
        Assert.Equal("После", (await freshService.GetSavedValuesAsync("test.docx"))!.FieldValues["Имя"].ToString());
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_SavesValuesToFile()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object>
        {
            { "Имя", "Иван" },
            { "Возраст", 30 },
            { "Дата", DateTimeOffset.Now }
        };

        // Act
        await _service.SaveTemplateValuesAsync(templateName, values);

        // Assert
        var expectedFilePath = Path.Combine(_testDirectory, "settings.json");
        Assert.True(File.Exists(expectedFilePath));
    }

    [Fact]
    public async Task GetSavedValuesAsync_ReturnsNullForNonExistentTemplate()
    {
        // Act
        var result = await _service.GetSavedValuesAsync("non_existent.docx");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndGetTemplateValues_RoundTrip_ReturnsCorrectValues()
    {
        // Arrange
        var templateName = "test_template.docx";
        var originalValues = new Dictionary<string, object>
        {
            { "Имя", "Петр" },
            { "Возраст", 25 }
        };

        // Act
        await _service.SaveTemplateValuesAsync(templateName, originalValues);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(templateName, result.TemplateFileName);
        Assert.Equal(2, result.FieldValues.Count);
        Assert.Equal("Петр", result.FieldValues["Имя"].ToString());
        Assert.Equal("25", result.FieldValues["Возраст"].ToString());
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_OverwritesExistingValues()
    {
        // Arrange
        var templateName = "test_template.docx";
        var firstValues = new Dictionary<string, object> { { "Поле", "Первое" } };
        var secondValues = new Dictionary<string, object> { { "Поле", "Второе" } };

        // Act
        await _service.SaveTemplateValuesAsync(templateName, firstValues);
        await _service.SaveTemplateValuesAsync(templateName, secondValues);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Второе", result.FieldValues["Поле"].ToString());
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_WithEmptyValues_SavesEmptyDictionary()
    {
        // Arrange
        var templateName = "empty_template.docx";
        var emptyValues = new Dictionary<string, object>();

        // Act
        await _service.SaveTemplateValuesAsync(templateName, emptyValues);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.FieldValues);
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_WithLastSaveDirectory_SavesDirectory()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object>
        {
            { "Имя", "Иван" }
        };
        var lastSaveDirectory = @"C:\Temp\SavedDocs";

        // Act
        await _service.SaveTemplateValuesAsync(templateName, values, lastSaveDirectory);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(lastSaveDirectory, result.LastSaveDirectory);
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_UpdatesLastSaveDirectory()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object> { { "Поле", "Значение" } };
        var firstDirectory = @"C:\Temp\First";
        var secondDirectory = @"C:\Temp\Second";

        // Act
        await _service.SaveTemplateValuesAsync(templateName, values, firstDirectory);
        await _service.SaveTemplateValuesAsync(templateName, values, secondDirectory);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(secondDirectory, result.LastSaveDirectory);
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_WithNullDirectory_DoesNotOverwriteExisting()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object> { { "Поле", "Значение" } };
        var existingDirectory = @"C:\Temp\Existing";

        // Act
        await _service.SaveTemplateValuesAsync(templateName, values, existingDirectory);
        await _service.SaveTemplateValuesAsync(templateName, values, null);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingDirectory, result.LastSaveDirectory);
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_WithOutputFileNameTemplate_SavesTemplate()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object> { { "Поле", "Значение" } };
        var outputFileNameTemplate = "Акт_{Дата}_{Имя}";

        // Act
        await _service.SaveTemplateValuesAsync(
            templateName,
            values,
            lastSaveDirectory: null,
            outputFileNameTemplate: outputFileNameTemplate);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(outputFileNameTemplate, result.OutputFileNameTemplate);
    }

    [Fact]
    public async Task SaveTemplateValuesAsync_WithNullOutputFileNameTemplate_DoesNotOverwriteExisting()
    {
        // Arrange
        var templateName = "test_template.docx";
        var values = new Dictionary<string, object> { { "Поле", "Значение" } };
        var outputFileNameTemplate = "Акт_{Дата}_{Имя}";

        // Act
        await _service.SaveTemplateValuesAsync(
            templateName,
            values,
            outputFileNameTemplate: outputFileNameTemplate);
        await _service.SaveTemplateValuesAsync(templateName, values);
        var result = await _service.GetSavedValuesAsync(templateName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(outputFileNameTemplate, result.OutputFileNameTemplate);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenLegacySettingsHaveNoOutputFileNameTemplate_LoadsSuccessfully()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "TemplatesFolderPath": "./Templates",
              "DefaultOutputPath": "C:\\Temp",
              "CultureName": "ru-RU",
              "OpenAfterSaveDefault": false,
              "Templates": [
                {
                  "TemplateFileName": "legacy.docx",
                  "FieldValues": {
                    "Поле": "Значение"
                  },
                  "LastUsed": "2026-05-15T00:00:00+03:00",
                  "LastSaveDirectory": "C:\\Temp"
                }
              ]
            }
            """);

        // Act
        var result = await _service.GetSavedValuesAsync("legacy.docx");

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.OutputFileNameTemplate);
        Assert.Equal("Значение", result.FieldValues["Поле"].ToString());
    }
}
