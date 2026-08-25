using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using Moq;
using Xunit;

namespace DocTemplater.Tests.ViewModels;

/// <summary>
/// Тесты для DataInputViewModel
/// </summary>
public class DataInputViewModelTests
{
    [Fact]
    public async Task BrowseForSaveFileAsync_CanBeSet()
    {
        // Arrange
        var template = CreateTestTemplate();
        var viewModel = await CreateLoadedViewModelAsync(template);

        // Act
        Func<string, Task<string?>> browseFunc = async (s) => await Task.FromResult("test.docx");
        viewModel.BrowseForSaveFileAsync = browseFunc;

        // Assert
        Assert.NotNull(viewModel.BrowseForSaveFileAsync);
        Assert.Same(browseFunc, viewModel.BrowseForSaveFileAsync);
    }

    [Fact]
    public async Task OutputFileNameTemplate_Default_RendersLegacyName()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate());

        Assert.Equal("TestTemplate_заполненный", viewModel.OutputFileNameTemplate);
        Assert.Equal("TestTemplate_заполненный", viewModel.OutputFileName);
        Assert.EndsWith("TestTemplate_заполненный.docx", viewModel.OutputFilePath);
    }

    [Fact]
    public async Task OutputFilePath_UpdatesWhenTemplatePlaceholderValuesChange()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate());
        var nameField = viewModel.Fields.Single(field => field.Placeholder.Name == "Имя");
        var ageField = viewModel.Fields.Single(field => field.Placeholder.Name == "Возраст");
        var dateField = viewModel.Fields.Single(field => field.Placeholder.Name == "Дата");

        viewModel.OutputDirectory = @"C:\Output";
        viewModel.OutputFileNameTemplate = "{Имя}_{Возраст}_{Дата}";
        nameField.ValueText = "Анна";
        ageField.IntegerValue = 7;
        dateField.DateValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(Path.Combine(@"C:\Output", "Анна_7_15.05.2026.docx"), viewModel.OutputFilePath);

        nameField.ValueText = "Мария";

        Assert.Equal(Path.Combine(@"C:\Output", "Мария_7_15.05.2026.docx"), viewModel.OutputFilePath);
    }

    [Fact]
    public async Task OutputFileNameTemplate_WithMultipleFormats_UsesDeterministicFormat()
    {
        var firstTemplate = CreateTemplateWithPlaceholders(new Placeholder
        {
            Name = "Дата",
            DataType = PlaceholderDataType.DateTimeOffset,
            FormatPatterns = new List<string> { "yyyy", "MM" }
        });
        var secondTemplate = CreateTemplateWithPlaceholders(new Placeholder
        {
            Name = "Дата",
            DataType = PlaceholderDataType.DateTimeOffset,
            FormatPatterns = new List<string> { "MM", "yyyy" }
        });

        var firstViewModel = await CreateLoadedViewModelAsync(firstTemplate);
        var secondViewModel = await CreateLoadedViewModelAsync(secondTemplate);

        firstViewModel.OutputFileNameTemplate = "{Дата}";
        secondViewModel.OutputFileNameTemplate = "{Дата}";
        firstViewModel.Fields.Single().DateValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));
        secondViewModel.Fields.Single().DateValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal("05", firstViewModel.OutputFileName);
        Assert.Equal(firstViewModel.OutputFileName, secondViewModel.OutputFileName);
    }

    [Fact]
    public async Task OutputFileNameTemplate_SanitizesInvalidFileNameCharacters()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate());
        var nameField = viewModel.Fields.Single(field => field.Placeholder.Name == "Имя");

        viewModel.OutputFileNameTemplate = @"A:B/{Имя}";
        nameField.ValueText = "Тест";

        Assert.Equal("A_B_Тест", viewModel.OutputFileName);
    }

    [Fact]
    public async Task OutputFileNameTemplate_DoesNotDuplicateDocxExtension()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate());

        viewModel.OutputFileNameTemplate = "Report.docx";

        Assert.Equal("Report", viewModel.OutputFileName);
        Assert.EndsWith("Report.docx", viewModel.OutputFilePath);
        Assert.False(viewModel.OutputFilePath.EndsWith("Report.docx.docx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OutputFilePath_Setter_UpdatesOutputFileNameTemplate()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate());

        viewModel.OutputFilePath = @"C:\Output\Custom.docx";

        Assert.Equal(@"C:\Output", viewModel.OutputDirectory);
        Assert.Equal("Custom", viewModel.OutputFileNameTemplate);
        Assert.Equal("Custom", viewModel.OutputFileName);
    }

    [Fact]
    public async Task Fields_ReplacingSameField_DoesNotDuplicateValueChangeSubscriptions()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateTemplateWithPlaceholders());
        var field = new PlaceholderFieldViewModel(
            new Placeholder
            {
                Name = "Имя",
                DataType = PlaceholderDataType.String,
                FormatPatterns = new List<string>()
            },
            CultureInfo.GetCultureInfo("ru-RU"));

        viewModel.OutputFileNameTemplate = "{Имя}";
        viewModel.Fields = new List<PlaceholderFieldViewModel> { field };
        viewModel.Fields = new List<PlaceholderFieldViewModel> { field };

        var outputFilePathChangedCount = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DataInputViewModel.OutputFilePath))
            {
                outputFilePathChangedCount++;
            }
        };

        field.ValueText = "Анна";

        Assert.Equal(1, outputFilePathChangedCount);
    }

    [Fact]
    public async Task LoadSavedValuesAsync_WithSavedOutputFileNameTemplate_RestoresTemplate()
    {
        var savedData = new SavedTemplateData
        {
            TemplateFileName = "TestTemplate.docx",
            OutputFileNameTemplate = "Акт_{Имя}",
            FieldValues = new Dictionary<string, object>
            {
                ["Имя"] = "Анна"
            }
        };

        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate(), savedData);

        Assert.Equal("Акт_{Имя}", viewModel.OutputFileNameTemplate);
        Assert.Equal("Акт_Анна", viewModel.OutputFileName);
    }

    [Fact]
    public async Task LoadSavedValuesAsync_WithoutSavedOutputFileNameTemplate_UsesDefaultTemplate()
    {
        var savedData = new SavedTemplateData
        {
            TemplateFileName = "TestTemplate.docx",
            FieldValues = new Dictionary<string, object>()
        };

        var viewModel = await CreateLoadedViewModelAsync(CreateTestTemplate(), savedData);

        Assert.Equal("TestTemplate_заполненный", viewModel.OutputFileNameTemplate);
    }

    [Fact]
    public async Task SaveCommand_PassesOutputFileNameTemplateToPersistence()
    {
        var template = CreateTestTemplate();
        var mockDocGen = new Mock<IDocumentGenerationService>();
        var mockValuePersistence = new Mock<IValuePersistenceService>();
        mockDocGen
            .Setup(service => service.GenerateDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, PlaceholderValue>>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        mockValuePersistence
            .Setup(service => service.GetSavedValuesAsync(It.IsAny<string>()))
            .ReturnsAsync((SavedTemplateData?)null);
        mockValuePersistence
            .Setup(service => service.SaveTemplateValuesAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var viewModel = new DataInputViewModel(
            template,
            mockDocGen.Object,
            mockValuePersistence.Object);
        await viewModel.LoadSavedValuesCommand.Execute().ToTask();
        viewModel.OutputDirectory = Path.GetTempPath();
        viewModel.OutputFileNameTemplate = "Документ_{Имя}";
        viewModel.Fields.Single(field => field.Placeholder.Name == "Имя").ValueText = "Анна";
        viewModel.Fields.Single(field => field.Placeholder.Name == "Возраст").IntegerValue = 7;
        viewModel.Fields.Single(field => field.Placeholder.Name == "Дата").DateValue =
            new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));

        await viewModel.SaveCommand.Execute().ToTask();

        mockValuePersistence.Verify(service => service.SaveTemplateValuesAsync(
            "TestTemplate.docx",
            It.IsAny<Dictionary<string, object>>(),
            Path.GetTempPath(),
            "Документ_{Имя}"), Times.Once);
    }

    private static async Task<DataInputViewModel> CreateLoadedViewModelAsync(
        Template template,
        SavedTemplateData? savedData = null)
    {
        var mockDocGen = new Mock<IDocumentGenerationService>();
        var mockValuePersistence = new Mock<IValuePersistenceService>();
        mockValuePersistence
            .Setup(service => service.GetSavedValuesAsync(It.IsAny<string>()))
            .ReturnsAsync(savedData);
        mockValuePersistence
            .Setup(service => service.SaveTemplateValuesAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var viewModel = new DataInputViewModel(
            template,
            mockDocGen.Object,
            mockValuePersistence.Object);

        await viewModel.LoadSavedValuesCommand.Execute().ToTask();
        return viewModel;
    }

    private static Template CreateTestTemplate()
    {
        return CreateTemplateWithPlaceholders(
            new Placeholder
            {
                Name = "Имя",
                DataType = PlaceholderDataType.String,
                FormatPatterns = new List<string>()
            },
            new Placeholder
            {
                Name = "Возраст",
                DataType = PlaceholderDataType.Integer,
                FormatPatterns = new List<string>()
            },
            new Placeholder
            {
                Name = "Дата",
                DataType = PlaceholderDataType.DateTimeOffset,
                FormatPatterns = new List<string> { "dd.MM.yyyy" }
            });
    }

    private static Template CreateTemplateWithPlaceholders(params Placeholder[] placeholders)
    {
        return new Template
        {
            TemplateId = Guid.NewGuid(),
            FileName = "TestTemplate",
            FilePath = "test.docx",
            Placeholders = placeholders.ToList()
        };
    }
}
