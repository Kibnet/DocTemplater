using DocTemplater.Domain;
using DocTemplater.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DocTemplater.Tests;

/// <summary>
/// Smoke tests for the DocTemplater application.
/// These tests verify the basic functionality of the application's core services.
/// </summary>
public class SmokeTests
{
    [Fact]
    public async Task Application_Startup_SmokeTest()
    {
        // Arrange
        var templatesFolderPath = Path.Combine(Path.GetTempPath(), "templates_test");
        var templateDiscoveryService = new TemplateDiscoveryService(templatesFolderPath);
        var placeholderExtractionService = new PlaceholderExtractionService();
        var valuePersistenceService = new ValuePersistenceService();

        // Act & Assert - Just verify that the services can be instantiated
        Assert.NotNull(templateDiscoveryService);
        Assert.NotNull(placeholderExtractionService);
        Assert.NotNull(valuePersistenceService);
    }

    [Fact]
    public async Task Service_Integration_SmokeTest()
    {
        // This test verifies that the core services can work together
        
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"smoke_test_{Guid.NewGuid()}");
        var settingsFilePath = Path.Combine(testDirectory, "settings.json");
        var valuePersistenceService = new ValuePersistenceService(settingsFilePath);
        
        var templateName = "test_template.docx";
        var testValues = new Dictionary<string, object>
        {
            { "Имя", "Тестовое имя" },
            { "Возраст", 25 },
            { "ДатаРождения", new DateTimeOffset(1995, 1, 1, 0, 0, 0, TimeSpan.Zero) }
        };

        try
        {
            // Act - Save values
            await valuePersistenceService.SaveTemplateValuesAsync(templateName, testValues);
            
            // Assert - Retrieve and verify values
            var savedValues = await valuePersistenceService.GetSavedValuesAsync(templateName);
            
            Assert.NotNull(savedValues);
            Assert.Equal(templateName, savedValues.TemplateFileName);
            Assert.Equal(3, savedValues.FieldValues.Count);
            Assert.Equal("Тестовое имя", savedValues.FieldValues["Имя"]);
            Assert.Equal("25", savedValues.FieldValues["Возраст"].ToString());
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    [Fact]
    public async Task Full_Workflow_SmokeTest()
    {
        // This test simulates a complete workflow:
        // 1. Create a test document with placeholders
        // 2. Extract placeholders
        // 3. Save values for placeholders
        // 4. Verify everything works together
        
        // Arrange
        var testDirectory = Path.Combine(Path.GetTempPath(), $"workflow_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDirectory);
        
        var documentPath = Path.Combine(testDirectory, "test_document.docx");
        var settingsFilePath = Path.Combine(testDirectory, "settings.json");
        
        var valuePersistenceService = new ValuePersistenceService(settingsFilePath);
        var placeholderExtractionService = new PlaceholderExtractionService();
        
        // Create a test document with placeholders
        CreateTestDocument(documentPath, "{Имя:String(n)} {Возраст:Integer(N0)} {ДатаРождения:DateTimeOffset(d.MM.yyyy)}");

        try
        {
            // Act - Extract placeholders
            var placeholders = await placeholderExtractionService.ExtractPlaceholdersAsync(documentPath);
            
            // Save test values
            var templateName = Path.GetFileName(documentPath);
            var testValues = new Dictionary<string, object>
            {
                { "Имя", "Иван" },
                { "Возраст", 30 },
                { "ДатаРождения", new DateTimeOffset(1990, 5, 15, 0, 0, 0, TimeSpan.Zero) }
            };
            
            await valuePersistenceService.SaveTemplateValuesAsync(templateName, testValues);
            
            // Assert
            Assert.Equal(3, placeholders.Count);
            Assert.Contains(placeholders, p => p.Name == "Имя" && p.DataType == PlaceholderDataType.String);
            Assert.Contains(placeholders, p => p.Name == "Возраст" && p.DataType == PlaceholderDataType.Integer);
            Assert.Contains(placeholders, p => p.Name == "ДатаРождения" && p.DataType == PlaceholderDataType.DateTimeOffset);
            
            var savedValues = await valuePersistenceService.GetSavedValuesAsync(templateName);
            Assert.NotNull(savedValues);
            Assert.Equal(3, savedValues.FieldValues.Count);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    private void CreateTestDocument(string filePath, string content)
    {
        using var wordDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
        var paragraph = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
        var run = paragraph.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
        run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(content));
    }
}