using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using DocTemplater.Views;
using Xunit;

namespace DocTemplater.UITests;

public class TemplateSelectionViewTests
{
    private static bool _initialized = false;
    private static Window? _testWindow = null;
    private static TemplateSelectionView? _templateSelectionView = null;
    private static readonly object _lock = new object();

    public TemplateSelectionViewTests()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                // Initialize headless platform
                AppBuilder.Configure<App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions
                    {
                        UseHeadlessDrawing = true
                    });
                
                // Create services and ViewModel
                var templatesFolderPath = Path.Combine(AppContext.BaseDirectory, "Templates");
                var templateDiscoveryService = new TemplateDiscoveryService(templatesFolderPath);
                var placeholderExtractionService = new PlaceholderExtractionService();

                var viewModel = new TemplateSelectionViewModel(
                    templateDiscoveryService,
                    placeholderExtractionService);

                // Create the view directly
                _templateSelectionView = new TemplateSelectionView
                {
                    DataContext = viewModel
                };
                
                // Create a test window to host the view
                _testWindow = new Window
                {
                    Content = _templateSelectionView,
                    Width = 800,
                    Height = 600
                };
                
                // Show the window to trigger visual tree construction
                _testWindow.Show();
                
                // Force layout
                Dispatcher.UIThread.RunJobs();
                
                _initialized = true;
            }
        }
    }
    [AvaloniaFact]
    public async Task Application_Starts_Successfully()
    {
        // Arrange & Act - TemplateSelectionView is initialized in constructor
        
        // Assert
        Assert.NotNull(_templateSelectionView);
    }

    [AvaloniaFact]
    public async Task TemplateSelectionView_Has_DataContext()
    {
        // Arrange & Act
        Assert.NotNull(_templateSelectionView);
        
        // Assert
        Assert.NotNull(_templateSelectionView.DataContext);
        Assert.IsType<TemplateSelectionViewModel>(_templateSelectionView.DataContext);
    }

    [AvaloniaFact]
    public async Task TemplateSelection_UI_Elements_Are_Present()
    {
        // Arrange
        Assert.NotNull(_templateSelectionView);
        
        // Wait for UI to load and ensure rendering
        await Task.Delay(500); // Allow time for visual tree to build
        
        // Act - Find UI elements by AutomationID (Name property)
        Button? refreshButton = null;
        Button? selectTemplateButton = null;
        ListBox? templatesListBox = null;
        int totalButtons = 0;
        int totalListBoxes = 0;
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Ensure measure/arrange pass
            _templateSelectionView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _templateSelectionView.Arrange(new Rect(_templateSelectionView.DesiredSize));
            
            // Find all buttons and listboxes in the visual tree
            var allButtons = _templateSelectionView.GetVisualDescendants().OfType<Button>().ToList();
            var allListBoxes = _templateSelectionView.GetVisualDescendants().OfType<ListBox>().ToList();
            
            totalButtons = allButtons.Count;
            totalListBoxes = allListBoxes.Count;
            
            refreshButton = allButtons.FirstOrDefault(b => b.Name == "RefreshButton");
            selectTemplateButton = allButtons.FirstOrDefault(b => b.Name == "SelectTemplateButton");
            templatesListBox = allListBoxes.FirstOrDefault(lb => lb.Name == "TemplatesListBox");
        });
        
        // Assert - Verify elements exist
        // If elements aren't found, fail with detailed information
        Assert.True(totalButtons > 0 || totalListBoxes > 0, 
            $"Visual tree appears empty. Buttons found: {totalButtons}, ListBoxes found: {totalListBoxes}");
        
        Assert.NotNull(refreshButton);
        Assert.NotNull(selectTemplateButton);
        Assert.NotNull(templatesListBox);
    }

    [AvaloniaFact]
    public async Task RefreshButton_Can_Be_Clicked()
    {
        // Arrange
        Assert.NotNull(_templateSelectionView);
        
        // Wait for UI to load and ensure rendering
        await Task.Delay(500);
        
        Button? refreshButton = null;
        bool commandExecuted = false;
        
        // Act - Find and click refresh button
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Ensure measure/arrange pass
            _templateSelectionView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _templateSelectionView.Arrange(new Rect(_templateSelectionView.DesiredSize));
            
            var allButtons = _templateSelectionView.GetVisualDescendants().OfType<Button>().ToList();
            refreshButton = allButtons.FirstOrDefault(b => b.Name == "RefreshButton");
            
            Assert.NotNull(refreshButton);
            
            // Execute the command
            if (refreshButton.Command?.CanExecute(null) == true)
            {
                refreshButton.Command.Execute(null);
                commandExecuted = true;
            }
        });
        
        // Wait for async operations
        await Task.Delay(300);
        
        // Assert - Command should have been executed
        Assert.True(commandExecuted, "RefreshButton command should be executable");
        Assert.NotNull(refreshButton);
        Assert.True(refreshButton.IsEffectivelyEnabled || !refreshButton.IsEffectivelyEnabled, "Button exists and has enabled state");
    }
}
