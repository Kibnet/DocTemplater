using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using Xunit.Abstractions;

namespace DocTemplater.Tests.Services;

/// <summary>
/// Тесты для проверки содержимого документов
/// </summary>
public class DocumentInspectionTests
{
    private readonly ITestOutputHelper _output;

    public DocumentInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectTemplateFile()
    {
        // Arrange
        var templatePath = @"..\..\..\..\DocTemplater\bin\Debug\net9.0\Templates\Дополнительное соглашение.docx";
        
        if (!File.Exists(templatePath))
        {
            _output.WriteLine($"Template file not found: {templatePath}");
            return;
        }

        // Act
        using var doc = WordprocessingDocument.Open(templatePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        var fullText = body?.InnerText ?? "";
        
        // Output
        _output.WriteLine("=== TEMPLATE FILE CONTENT ===");
        _output.WriteLine(fullText);
        _output.WriteLine("\n=== TEXT ELEMENTS ===");
        
        if (body != null)
        {
            var textElements = body.Descendants<Text>().ToList();
            foreach (var text in textElements)
            {
                _output.WriteLine($"Text: '{text.Text}'");
            }
        }
    }

    [Fact]
    public void InspectOutputFile()
    {
        // Arrange
        var outputPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Дополнительное соглашение_заполненный.docx");
        
        if (!File.Exists(outputPath))
        {
            _output.WriteLine($"Output file not found: {outputPath}");
            return;
        }

        // Act
        using var doc = WordprocessingDocument.Open(outputPath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        var fullText = body?.InnerText ?? "";
        
        // Output
        _output.WriteLine("=== OUTPUT FILE CONTENT ===");
        _output.WriteLine(fullText);
        _output.WriteLine("\n=== TEXT ELEMENTS ===");
        
        if (body != null)
        {
            var textElements = body.Descendants<Text>().ToList();
            foreach (var text in textElements)
            {
                _output.WriteLine($"Text: '{text.Text}'");
            }
        }
    }

    [Fact]
    public void CompareTemplateAndOutput()
    {
        // Arrange
        var templatePath = @"..\..\..\..\DocTemplater\bin\Debug\net9.0\Templates\Дополнительное соглашение.docx";
        var outputPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Дополнительное соглашение_заполненный.docx");
        
        if (!File.Exists(templatePath))
        {
            _output.WriteLine($"Template file not found: {templatePath}");
            return;
        }
        
        if (!File.Exists(outputPath))
        {
            _output.WriteLine($"Output file not found: {outputPath}");
            return;
        }

        // Act - Get template text
        string templateText;
        using (var doc = WordprocessingDocument.Open(templatePath, false))
        {
            templateText = doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
        }

        // Act - Get output text
        string outputText;
        using (var doc = WordprocessingDocument.Open(outputPath, false))
        {
            outputText = doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
        }

        // Output comparison
        _output.WriteLine("=== TEMPLATE TEXT ===");
        _output.WriteLine(templateText);
        _output.WriteLine("\n=== OUTPUT TEXT ===");
        _output.WriteLine(outputText);
        _output.WriteLine("\n=== COMPARISON ===");
        _output.WriteLine($"Template length: {templateText.Length}");
        _output.WriteLine($"Output length: {outputText.Length}");
        _output.WriteLine($"Are they different? {templateText != outputText}");
        
        // Check for placeholders in output
        var placeholderPattern = @"\{[^}]+:[^}]+\([^)]*\)\}";
        var templateMatches = System.Text.RegularExpressions.Regex.Matches(templateText, placeholderPattern);
        var outputMatches = System.Text.RegularExpressions.Regex.Matches(outputText, placeholderPattern);
        
        _output.WriteLine($"\nPlaceholders in template: {templateMatches.Count}");
        _output.WriteLine($"Placeholders in output: {outputMatches.Count}");
        
        if (outputMatches.Count > 0)
        {
            _output.WriteLine("\n⚠️ WARNING: Placeholders still present in output file!");
            foreach (System.Text.RegularExpressions.Match match in outputMatches)
            {
                _output.WriteLine($"  - {match.Value}");
            }
        }
    }
}
