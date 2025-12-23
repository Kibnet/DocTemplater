using System;
using System.IO;
using System.Threading.Tasks;
using DocTemplater.Services;
using Xunit;

namespace DocTemplater.Tests.Services;

/// <summary>
/// Тесты для FileSystemWatcher функциональности в TemplateDiscoveryService
/// </summary>
public class TemplateDiscoveryServiceFileWatcherTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TemplateDiscoveryService _service;

    public TemplateDiscoveryServiceFileWatcherTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"test_templates_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new TemplateDiscoveryService(_testDirectory);
    }

    public void Dispose()
    {
        _service.Dispose();
        
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Thread.Sleep(100);
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Игнорируем ошибки очистки
        }
    }

    [Fact]
    public async Task TemplatesChanged_RaisedWhenFileAdded()
    {
        // Arrange
        bool eventRaised = false;
        _service.TemplatesChanged += (s, e) => eventRaised = true;

        // Act
        var testFile = Path.Combine(_testDirectory, "new_template.docx");
        await File.WriteAllTextAsync(testFile, "test content");
        
        // Ждем события FileSystemWatcher
        await Task.Delay(1000);

        // Assert
        Assert.True(eventRaised, "Событие TemplatesChanged должно быть вызвано при добавлении нового файла");
    }

    [Fact]
    public async Task TemplatesChanged_RaisedWhenFileDeleted()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "delete_template.docx");
        await File.WriteAllTextAsync(testFile, "test content");
        await Task.Delay(500);

        bool eventRaised = false;
        _service.TemplatesChanged += (s, e) => eventRaised = true;

        // Act
        File.Delete(testFile);
        await Task.Delay(1000);

        // Assert
        Assert.True(eventRaised, "Событие TemplatesChanged должно быть вызвано при удалении файла");
    }

    [Fact]
    public async Task TemplatesChanged_NotRaisedForTempFiles()
    {
        // Arrange
        bool eventRaised = false;
        _service.TemplatesChanged += (s, e) => eventRaised = true;

        // Act - создание временного файла Word (начинается с ~$)
        var tempFile = Path.Combine(_testDirectory, "~$temp.docx");
        await File.WriteAllTextAsync(tempFile, "test content");
        await Task.Delay(1000);

        // Assert
        Assert.False(eventRaised, "Событие не должно вызываться для временных файлов Word");
    }

    [Fact]
    public void SetTemplatesDirectory_ChangesMonitoredDirectory()
    {
        // Arrange
        var newDirectory = Path.Combine(Path.GetTempPath(), $"test_new_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(newDirectory);

        try
        {
            bool eventRaised = false;
            _service.TemplatesChanged += (s, e) => eventRaised = true;

            // Act
            _service.SetTemplatesDirectory(newDirectory);

            // Assert
            Assert.True(eventRaised, "Событие должно быть вызвано при изменении директории");
        }
        finally
        {
            try
            {
                if (Directory.Exists(newDirectory))
                {
                    Directory.Delete(newDirectory, true);
                }
            }
            catch { }
        }
    }
}
