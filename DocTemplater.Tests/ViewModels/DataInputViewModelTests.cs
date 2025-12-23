using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public void BrowseForSaveFileAsync_CanBeSet()
    {
        // Arrange
        var template = CreateTestTemplate();
        var mockDocGen = new Mock<IDocumentGenerationService>();
        var mockValuePersistence = new Mock<IValuePersistenceService>();
        
        var viewModel = new DataInputViewModel(
            template, 
            mockDocGen.Object, 
            mockValuePersistence.Object);

        // Act
        Func<Task<string?>> browseFunc = async () => await Task.FromResult("test.docx");
        viewModel.BrowseForSaveFileAsync = browseFunc;

        // Assert
        Assert.NotNull(viewModel.BrowseForSaveFileAsync);
        Assert.Same(browseFunc, viewModel.BrowseForSaveFileAsync);
    }

    private Template CreateTestTemplate()
    {
        return new Template
        {
            TemplateId = Guid.NewGuid(),
            FileName = "TestTemplate",
            FilePath = "test.docx",
            Placeholders = new List<Placeholder>
            {
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
                    FormatPatterns = new List<string> { "N0" }
                },
                new Placeholder
                {
                    Name = "Дата",
                    DataType = PlaceholderDataType.DateTimeOffset,
                    FormatPatterns = new List<string> { "dd.MM.yyyy" }
                }
            }
        };
    }
}
