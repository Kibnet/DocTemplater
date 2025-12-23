using System.Globalization;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using Xunit.Abstractions;

namespace DocTemplater.Tests.Services;

/// <summary>
/// Интеграционные тесты для DocumentGenerationService
/// </summary>
public class DocumentGenerationServiceIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly DocumentGenerationService _service;

    public DocumentGenerationServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _service = new DocumentGenerationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task GenerateDocument_WithRealTemplate_ReplacesAllPlaceholders()
    {
        // Arrange
        var templatePath = @"..\..\..\..\DocTemplater\bin\Debug\net9.0\Templates\Дополнительное соглашение.docx";
        
        if (!File.Exists(templatePath))
        {
            _output.WriteLine($"Template not found: {templatePath}");
            return; // Skip test if template doesn't exist
        }

        var outputPath = Path.Combine(_tempDir, "test_output.docx");
        
        var values = new Dictionary<string, PlaceholderValue>
        {
            ["Номер Соглашения"] = new PlaceholderValue { PlaceholderName = "Номер Соглашения", RawValue = 123 },
            ["Дата Подписания"] = new PlaceholderValue { PlaceholderName = "Дата Подписания", RawValue = new DateTimeOffset(2020, 12, 1, 0, 0, 0, TimeSpan.FromHours(3)) },
            ["Дата Начала Периода"] = new PlaceholderValue { PlaceholderName = "Дата Начала Периода", RawValue = new DateTimeOffset(2020, 12, 2, 0, 0, 0, TimeSpan.FromHours(3)) },
            ["Дата Окончания Периода"] = new PlaceholderValue { PlaceholderName = "Дата Окончания Периода", RawValue = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)) }
        };

        // Act
        var result = await _service.GenerateDocumentAsync(templatePath, outputPath, values, "ru-RU");

        // Assert
        Assert.True(result, "Document generation should succeed");
        Assert.True(File.Exists(outputPath), "Output file should exist");

        // Verify placeholders were replaced
        using var doc = WordprocessingDocument.Open(outputPath, false);
        var outputText = doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
        
        _output.WriteLine("=== OUTPUT TEXT ===");
        _output.WriteLine(outputText);

        // Check that placeholders are NOT in the output
        Assert.DoesNotContain("{Номер Соглашения:Integer()}", outputText);
        Assert.DoesNotContain("{Дата Подписания:DateTimeOffset(d MMMM yyyy)}", outputText);
        Assert.DoesNotContain("{Дата Начала Периода:DateTimeOffset(d MMMM yyyy)}", outputText);
        Assert.DoesNotContain("{Дата Окончания Периода:DateTimeOffset(d MMMM yyyy)}", outputText);

        // Check that values ARE in the output
        Assert.Contains("123", outputText); // Number
        Assert.Contains("1 декабря 2020", outputText); // Date in Russian format
        Assert.Contains("2 декабря 2020", outputText);
        Assert.Contains("1 января 2020", outputText);
    }

    [Fact]
    public void InspectTemplateTextElements()
    {
        // Arrange
        var templatePath = @"..\..\..\..\DocTemplater\bin\Debug\net9.0\Templates\Дополнительное соглашение.docx";
        
        if (!File.Exists(templatePath))
        {
            _output.WriteLine($"Template not found: {templatePath}");
            return;
        }

        // Act & Assert - Inspect how Word stores the placeholders
        using var doc = WordprocessingDocument.Open(templatePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        
        if (body == null)
        {
            _output.WriteLine("Body is null");
            return;
        }

        _output.WriteLine("=== TEXT ELEMENTS IN TEMPLATE ===");
        var textElements = body.Descendants<Text>().ToList();
        
        for (int i = 0; i < textElements.Count; i++)
        {
            var text = textElements[i];
            if (text.Text.Contains("{") || text.Text.Contains("}") || text.Text.Contains(":"))
            {
                _output.WriteLine($"[{i}] '{text.Text}' (Contains placeholder parts)");
            }
        }

        _output.WriteLine($"\nTotal text elements: {textElements.Count}");
        
        // Check for placeholder patterns
        _output.WriteLine("\n=== SEARCHING FOR COMPLETE PLACEHOLDERS IN INDIVIDUAL ELEMENTS ===");
        var placeholderPattern = @"\{[^}]+:[^}]+\([^)]*\)\}";
        int foundComplete = 0;
        
        foreach (var text in textElements)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text.Text, placeholderPattern);
            if (matches.Count > 0)
            {
                foundComplete++;
                _output.WriteLine($"Found complete placeholder in: '{text.Text}'");
            }
        }
        
        _output.WriteLine($"\nComplete placeholders found in individual Text elements: {foundComplete}");
        
        // Now check in combined text
        var fullText = body.InnerText;
        var fullMatches = System.Text.RegularExpressions.Regex.Matches(fullText, placeholderPattern);
        _output.WriteLine($"Complete placeholders found in combined InnerText: {fullMatches.Count}");
        
        if (foundComplete < fullMatches.Count)
        {
            _output.WriteLine("\n⚠️ PROBLEM IDENTIFIED: Placeholders are split across multiple Text elements!");
            _output.WriteLine("This is why the replacement doesn't work - the regex can't find them in individual elements.");
        }
    }
}
