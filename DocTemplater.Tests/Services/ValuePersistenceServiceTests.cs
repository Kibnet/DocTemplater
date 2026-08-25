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
