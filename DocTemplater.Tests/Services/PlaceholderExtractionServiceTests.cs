using DocTemplater.Domain;
using DocTemplater.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace DocTemplater.Tests.Services;

public class PlaceholderExtractionServiceTests : IDisposable
{
    private readonly PlaceholderExtractionService _service;
    private readonly string _testFilePath;

    public PlaceholderExtractionServiceTests()
    {
        _service = new PlaceholderExtractionService();
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.docx");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithStringPlaceholder_ReturnsCorrectPlaceholder()
    {
        // Arrange
        CreateDocxWithText("{Имя:String()}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Single(placeholders);
        Assert.Equal("Имя", placeholders[0].Name);
        Assert.Equal(PlaceholderDataType.String, placeholders[0].DataType);
        Assert.Single(placeholders[0].FormatPatterns);
        Assert.Empty(placeholders[0].FormatPatterns[0]);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithIntegerPlaceholder_ReturnsCorrectPlaceholder()
    {
        // Arrange
        CreateDocxWithText("{Количество:Integer(N0)}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Single(placeholders);
        Assert.Equal("Количество", placeholders[0].Name);
        Assert.Equal(PlaceholderDataType.Integer, placeholders[0].DataType);
        Assert.Single(placeholders[0].FormatPatterns);
        Assert.Equal("N0", placeholders[0].FormatPatterns[0]);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithDateTimePlaceholder_ReturnsCorrectPlaceholder()
    {
        // Arrange
        CreateDocxWithText("{ДатаНачала:DateTimeOffset(d MMMM yyyy)}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Single(placeholders);
        Assert.Equal("ДатаНачала", placeholders[0].Name);
        Assert.Equal(PlaceholderDataType.DateTimeOffset, placeholders[0].DataType);
        Assert.Single(placeholders[0].FormatPatterns);
        Assert.Equal("d MMMM yyyy", placeholders[0].FormatPatterns[0]);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithMultiplePlaceholders_ReturnsAllPlaceholders()
    {
        // Arrange
        CreateDocxWithText("{Имя:String()} {Возраст:Integer()} {Дата:DateTimeOffset(d.MM.yyyy)}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Equal(3, placeholders.Count);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithDuplicatePlaceholders_ReturnsMergedPlaceholder()
    {
        // Arrange
        CreateDocxWithText("{Имя:String()} и еще раз {Имя:String()}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Single(placeholders);
        Assert.Equal("Имя", placeholders[0].Name);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithSameNameDifferentFormats_MergesFormats()
    {
        // Arrange
        CreateDocxWithText("{Дата:DateTimeOffset(d.MM.yyyy)} {Дата:DateTimeOffset(d MMMM yyyy)}");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Single(placeholders);
        Assert.Equal(2, placeholders[0].FormatPatterns.Count);
        Assert.Contains("d.MM.yyyy", placeholders[0].FormatPatterns);
        Assert.Contains("d MMMM yyyy", placeholders[0].FormatPatterns);
    }

    [Fact]
    public async Task ExtractPlaceholdersAsync_WithNoPlaceholders_ReturnsEmptyList()
    {
        // Arrange
        CreateDocxWithText("Обычный текст без плейсхолдеров");

        // Act
        var placeholders = await _service.ExtractPlaceholdersAsync(_testFilePath);

        // Assert
        Assert.Empty(placeholders);
    }

    private void CreateDocxWithText(string text)
    {
        using var wordDocument = WordprocessingDocument.Create(_testFilePath, WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        var paragraph = body.AppendChild(new Paragraph());
        var run = paragraph.AppendChild(new Run());
        run.AppendChild(new Text(text));
    }
}
